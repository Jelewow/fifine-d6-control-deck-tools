using System;
using System.Runtime.InteropServices;

namespace FifineMicMute
{
    internal enum MicrophoneState
    {
        Unavailable,
        Unmuted,
        Muted
    }

    internal interface IMicrophone
    {
        MicrophoneState Read();
        MicrophoneState Toggle();
    }

    internal sealed class WindowsMicrophone : IMicrophone
    {
        public MicrophoneState Read()
        {
            return AccessMicrophone(false);
        }

        public MicrophoneState Toggle()
        {
            return AccessMicrophone(true);
        }

        private static MicrophoneState AccessMicrophone(bool toggle)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            object endpoint = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                if (enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console, out device) < 0)
                {
                    return MicrophoneState.Unavailable;
                }

                Guid endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
                if (device.Activate(ref endpointVolumeId, ClassContext.InProcessServer, IntPtr.Zero, out endpoint) < 0)
                {
                    return MicrophoneState.Unavailable;
                }
                var volume = (IAudioEndpointVolume)endpoint;

                bool muted;
                if (volume.GetMute(out muted) < 0)
                {
                    return MicrophoneState.Unavailable;
                }

                if (toggle && (volume.SetMute(!muted, IntPtr.Zero) < 0 || volume.GetMute(out muted) < 0))
                {
                    return MicrophoneState.Unavailable;
                }

                return muted ? MicrophoneState.Muted : MicrophoneState.Unmuted;
            }
            catch (COMException)
            {
                // The audio service can be unavailable during startup or after resume.
                return MicrophoneState.Unavailable;
            }
            catch (UnauthorizedAccessException)
            {
                return MicrophoneState.Unavailable;
            }
            finally
            {
                if (endpoint != null) Marshal.ReleaseComObject(endpoint);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        private enum DataFlow { Capture = 1 }
        private enum Role { Console = 0 }
        private enum ClassContext { InProcessServer = 1 }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(DataFlow flow, uint stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(DataFlow flow, Role role, out IMMDevice endpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, ClassContext classContext, IntPtr activationParameters,
                [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            // Keep the order defined in endpointvolume.h, including the slots before SetMute.
            [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr context);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, IntPtr context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        }
    }
}
