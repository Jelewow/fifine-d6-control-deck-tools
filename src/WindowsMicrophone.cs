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
