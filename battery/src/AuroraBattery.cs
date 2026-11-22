using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace FifineTools.Battery
{
    internal static class AuroraBattery
    {
        private const int ReportSize = 17;
        private const byte ReportId = 0x08;
        private const byte ReadBatteryCommand = 0x04;
        private const byte ChecksumTarget = 0x55;
        private const uint ReadWriteAccess = 0xc0000000;
        private const uint Overlapped = 0x40000000;

        internal static async Task<BatteryReading?> ReadAsync()
        {
            Guid hidClass;
            HidD_GetHidGuid(out hidClass);
            var receivers = new List<string>();
            var wired = new List<string>();
            foreach (string path in WindowsDevices.HidInterfaces(hidClass))
            {
                if (path.IndexOf("#vid_3554&", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                using (SafeFileHandle device = Open(path, 0, 0))
                {
                    if (device.IsInvalid || !IsBatteryCollection(device))
                    {
                        continue;
                    }
                    if (path.IndexOf("#vid_3554&pid_fb08&", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        receivers.Add(path);
                    }
                    else if (IsWiredAurora(device))
                    {
                        wired.Add(path);
                    }
                }
            }

            // The direct connection takes precedence when the receiver remains plugged in.
            wired.AddRange(receivers);
            foreach (string path in wired)
            {
                BatteryReading? reading = await QueryAsync(path);
                if (reading.HasValue)
                {
                    return reading;
                }
            }
            return null;
        }

        private static SafeFileHandle Open(string path, uint access, uint flags)
        {
            return CreateFileW(path, access, 3, IntPtr.Zero, 3, flags, IntPtr.Zero);
        }

        private static bool IsWiredAurora(SafeFileHandle device)
        {
            var name = new StringBuilder(128);
            if (!HidD_GetProductString(device, name, name.Capacity * sizeof(char)))
            {
                return false;
            }
            string product = name.ToString().Trim();
            return product.Equals("IO Aurora", StringComparison.OrdinalIgnoreCase) ||
                product.Equals("IO Aurora Mouse", StringComparison.OrdinalIgnoreCase) ||
                product.Equals("IO Aurora Wired", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBatteryCollection(SafeFileHandle device)
        {
            IntPtr data;
            if (!HidD_GetPreparsedData(device, out data))
            {
                return false;
            }
            try
            {
                HidCaps caps;
                return HidP_GetCaps(data, out caps) == 0x00110000 && caps.UsagePage == 0xff02 &&
                    caps.Usage == 2 && caps.InputLength == ReportSize && caps.OutputLength == ReportSize;
            }
            finally
            {
                HidD_FreePreparsedData(data);
            }
        }

        private static async Task<BatteryReading?> QueryAsync(string path)
        {
            try
            {
                using (SafeFileHandle device = Open(path, ReadWriteAccess, Overlapped))
                {
                    if (device.IsInvalid || !IsBatteryCollection(device))
                    {
                        return null;
                    }
                    using (var timeout = new CancellationTokenSource(1500))
                    using (var stream = new FileStream(device, FileAccess.ReadWrite, 1, true))
                    {
                        // One Compx battery query on the vendor collection.
                        var request = new byte[ReportSize];
                        request[0] = ReportId;
                        request[1] = ReadBatteryCommand;
                        request[ReportSize - 1] = ChecksumTarget - ReportId - ReadBatteryCommand;
                        await stream.WriteAsync(request, 0, request.Length, timeout.Token);

                        var reply = new byte[ReportSize];
                        while (!timeout.IsCancellationRequested)
                        {
                            int count = await stream.ReadAsync(reply, 0, reply.Length, timeout.Token);
                            if (count == 0)
                            {
                                break;
                            }
                            BatteryReading? reading = ReadReply(reply, count);
                            if (reading.HasValue)
                            {
                                return reading;
                            }
                        }
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (OperationCanceledException) { }
            return null;
        }

        internal static BatteryReading? ReadReply(byte[] reply, int count)
        {
            if (count != ReportSize || reply[0] != ReportId || reply[1] != ReadBatteryCommand || reply[6] > 100)
            {
                return null;
            }
            int voltage = (reply[8] << 8) | reply[9];
            if (reply[6] == 0 && voltage == 0)
            {
                return null;
            }
            int checksum = 0;
            for (int index = 0; index < ReportSize; index++)
            {
                checksum += reply[index];
            }
            return (checksum & 0xff) == ChecksumTarget ? new BatteryReading(reply[6], reply[7] == 1) : (BatteryReading?)null;
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
