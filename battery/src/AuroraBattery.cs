using System;
using System.Collections.Generic;
using System.IO;
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

        internal static async Task<BatteryReading?> ReadAsync()
        {
            var receivers = new List<string>();
            var wired = new List<string>();
            foreach (string path in HidDevice.Interfaces())
            {
                if (path.IndexOf("#vid_3554&", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                using (SafeFileHandle device = HidDevice.Open(path))
                {
                    if (!HidDevice.IsCollection(device, 0xff02, 2, ReportSize))
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

        private static bool IsWiredAurora(SafeFileHandle device)
        {
            string product = HidDevice.ProductName(device);
            return string.Equals(product, "IO Aurora", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(product, "IO Aurora Mouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(product, "IO Aurora Wired", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<BatteryReading?> QueryAsync(string path)
        {
            try
            {
                using (SafeFileHandle device = HidDevice.Open(path, FileAccess.ReadWrite))
                {
                    if (!HidDevice.IsCollection(device, 0xff02, 2, ReportSize))
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
    }
}
