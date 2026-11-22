using System;
using System.Runtime.InteropServices;

namespace FifineMicMute
{
    internal enum PlaybackState
    {
        Unavailable,
        Other,
        Simgot,
        Arzopa
    }

    internal sealed class WindowsPlayback
    {
        private const uint ActiveEndpoints = 1;
        private const ushort StringProperty = 31;
        private static readonly PropertyKey FriendlyName = new PropertyKey
        {
            Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            Id = 14
        };

        internal PlaybackState Read()
        {
            return AccessPlayback(false);
        }

        internal PlaybackState Toggle()
        {
            return AccessPlayback(true);
        }

        private static PlaybackState AccessPlayback(bool toggle)
        {
            IMMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                string currentId;
                PlaybackState current = ReadDefault(enumerator, out currentId);
                if (!toggle)
                {
                    return current;
                }

                string targetId = FindTarget(enumerator, current);
                if (targetId != null && !string.Equals(targetId, currentId, StringComparison.OrdinalIgnoreCase))
                {
                    SetDefault(enumerator, targetId, currentId);
                    return ReadDefault(enumerator, out currentId);
                }
                return current;
            }
            catch (COMException)
            {
                return PlaybackState.Unavailable;
            }
            catch (UnauthorizedAccessException)
            {
                return PlaybackState.Unavailable;
            }
            finally
            {
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        private static PlaybackState ReadDefault(IMMDeviceEnumerator enumerator, out string id)
        {
            id = null;
            IMMDevice endpoint = null;
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out endpoint) < 0)
                {
                    return PlaybackState.Unavailable;
                }
                id = DeviceId(endpoint);
                return Identify(DeviceName(endpoint));
            }
            finally
            {
                if (endpoint != null) Marshal.ReleaseComObject(endpoint);
            }
        }

        private static string FindTarget(IMMDeviceEnumerator enumerator, PlaybackState current)
        {
            string simgot = null;
            string arzopa = null;
            IMMDeviceCollection endpoints = null;
            try
            {
                uint count;
                if (enumerator.EnumAudioEndpoints(DataFlow.Render, ActiveEndpoints, out endpoints) < 0 ||
                    endpoints.GetCount(out count) < 0)
                {
                    return null;
                }

                for (uint index = 0; index < count && (simgot == null || arzopa == null); index++)
                {
                    IMMDevice endpoint = null;
                    try
                    {
                        if (endpoints.Item(index, out endpoint) < 0)
                        {
                            continue;
                        }

                        PlaybackState match = Identify(DeviceName(endpoint));
                        if (match == PlaybackState.Simgot && simgot == null)
                        {
                            simgot = DeviceId(endpoint);
                        }
                        else if (match == PlaybackState.Arzopa && arzopa == null)
                        {
                            arzopa = DeviceId(endpoint);
                        }
                    }
                    finally
                    {
                        if (endpoint != null) Marshal.ReleaseComObject(endpoint);
                    }
                }
            }
            finally
            {
                if (endpoints != null) Marshal.ReleaseComObject(endpoints);
            }

            switch (current)
            {
                case PlaybackState.Simgot: return arzopa;
                case PlaybackState.Arzopa: return simgot;
                default: return simgot ?? arzopa;
            }
        }

        private static PlaybackState Identify(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return PlaybackState.Unavailable;
            }
            if (deviceName.IndexOf("SIMGOT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PlaybackState.Simgot;
            }
            if (deviceName.IndexOf("ARZOPA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PlaybackState.Arzopa;
            }
            return PlaybackState.Other;
        }

        private static string DeviceId(IMMDevice endpoint)
        {
            string id;
            return endpoint.GetId(out id) >= 0 ? id : null;
        }

        private static string DeviceName(IMMDevice endpoint)
        {
            IPropertyStore properties = null;
            try
            {
                if (endpoint.OpenPropertyStore(0, out properties) < 0)
                {
                    return null;
                }

                PropertyKey key = FriendlyName;
                PropertyValue value;
                if (properties.GetValue(ref key, out value) < 0)
                {
                    return null;
                }
                try
                {
                    return value.Type == StringProperty ? Marshal.PtrToStringUni(value.Pointer) : null;
                }
                finally
                {
                    PropVariantClear(ref value);
                }
            }
            finally
            {
                if (properties != null) Marshal.ReleaseComObject(properties);
            }
        }

        private static void SetDefault(IMMDeviceEnumerator enumerator, string targetId, string previousId)
        {
            var policy = (IPolicyConfig)new PolicyConfigClient();
            try
            {
                if (policy.SetDefaultEndpoint(targetId, Role.Multimedia) >= 0 &&
                    policy.SetDefaultEndpoint(targetId, Role.Console) < 0 && previousId != null)
                {
                    string actualId;
                    ReadDefault(enumerator, out actualId);
                    if (string.Equals(actualId, targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        policy.SetDefaultEndpoint(previousId, Role.Multimedia);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(policy);
            }
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropertyValue value);

        [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        private class PolicyConfigClient { }

        [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            // Preserve the COM slots before SetDefaultEndpoint. The plugin calls only that method.
            [PreserveSig] int GetMixFormat();
            [PreserveSig] int GetDeviceFormat();
            [PreserveSig] int ResetDeviceFormat();
            [PreserveSig] int SetDeviceFormat();
            [PreserveSig] int GetProcessingPeriod();
            [PreserveSig] int SetProcessingPeriod();
            [PreserveSig] int GetShareMode();
            [PreserveSig] int SetShareMode();
            [PreserveSig] int GetPropertyValue();
            [PreserveSig] int SetPropertyValue();
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string endpointId, Role role);
        }
    }
}
