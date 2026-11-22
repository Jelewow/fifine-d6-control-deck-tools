using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FifineTools.Battery
{
    internal static class HidDevice
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint Overlapped = 0x40000000;
        private const uint ShareReadWrite = 3;
        private const uint OpenExisting = 3;
        private const int CapsSuccess = 0x00110000;

        internal static IEnumerable<string> Interfaces()
        {
            Guid hidClass;
            HidD_GetHidGuid(out hidClass);
            return WindowsDevices.HidInterfaces(hidClass);
        }

        internal static SafeFileHandle Open(string path, FileAccess? access = null)
        {
            uint desiredAccess = 0;
            if (access.HasValue)
            {
                desiredAccess = GenericRead;
                if (access.Value == FileAccess.ReadWrite)
                {
                    desiredAccess |= GenericWrite;
                }
            }
            return CreateFileW(path, desiredAccess, ShareReadWrite, IntPtr.Zero, OpenExisting,
                access.HasValue ? Overlapped : 0, IntPtr.Zero);
        }

        internal static bool IsCollection(SafeFileHandle device, ushort usagePage, ushort usage, int reportSize)
        {
            IntPtr data;
            if (device.IsInvalid || !HidD_GetPreparsedData(device, out data))
            {
                return false;
            }
            try
            {
                HidCaps caps;
                return HidP_GetCaps(data, out caps) == CapsSuccess && caps.UsagePage == usagePage &&
                    caps.Usage == usage && caps.InputLength == reportSize && caps.OutputLength == reportSize;
            }
            finally
            {
                HidD_FreePreparsedData(data);
            }
        }

        internal static string ProductName(SafeFileHandle device)
        {
            var name = new StringBuilder(128);
            return HidD_GetProductString(device, name, name.Capacity * sizeof(char)) ? name.ToString().Trim() : null;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct HidCaps
        {
            [FieldOffset(0)] internal ushort Usage;
            [FieldOffset(2)] internal ushort UsagePage;
            [FieldOffset(4)] internal ushort InputLength;
            [FieldOffset(6)] internal ushort OutputLength;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr data);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_FreePreparsedData(IntPtr data);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_GetProductString(SafeFileHandle device, [Out] StringBuilder text, int bytes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);
    }
}
