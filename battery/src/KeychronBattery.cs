using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace FifineTools.Battery
{
    internal static class KeychronBattery
    {
        private const string WiredId = "#vid_3434&pid_0b10&";
        private const string ReceiverId = "#vid_3434&pid_d030&";
        private const int ReportSize = 33; // Windows adds a report ID byte to the 32-byte QMK payload.
        private const byte ReadFeatures = 0xa2;
        private const byte ReadBattery = 0xa4;
        private const byte BatteryFeatureHighByte = 0x04; // FEATURE_BATTERY is bit 10.
        private const byte Q1HeModel = 2;
        private const byte UsbTransport = 0x01;
        private const byte RadioTransport = 0x04;
        private const int CableTimeoutMs = 1500;
        private const int ReceiverTimeoutMs = 3200;

        internal static async Task<BatteryReading?> ReadAsync()
        {
            var receivers = new List<string>();
            foreach (string path in HidDevice.Interfaces())
            {
                if (path.IndexOf(WiredId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    BatteryReading? reading = await TryReadAsync(path, UsbTransport);
                    if (reading.HasValue)
                    {
                        return reading;
                    }
                }
                else if (path.IndexOf(ReceiverId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    receivers.Add(path);
                }
            }

            foreach (string path in receivers)
            {
                BatteryReading? reading = await TryReadAsync(path, RadioTransport);
                if (reading.HasValue)
                {
                    return reading;
                }
            }
            return null;
        }

        private static async Task<BatteryReading?> TryReadAsync(string path, byte transport)
        {
            bool wired = transport == UsbTransport;
            FileAccess access = wired ? FileAccess.ReadWrite : FileAccess.Read;
            try
            {
                using (SafeFileHandle device = HidDevice.Open(path, access))
                {
                    if (!HidDevice.IsCollection(device, 0xff60, 0x61, ReportSize))
                    {
                        return null;
                    }
                    // The radio firmware sends a report approximately every two seconds while connected.
                    using (var timeout = new CancellationTokenSource(wired ? CableTimeoutMs : ReceiverTimeoutMs))
                    using (var stream = new FileStream(device, access, 1, true))
                    {
                        return await ReadReportsAsync(stream, wired, timeout.Token);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (OperationCanceledException) { }
            return null;
        }

        internal static async Task<BatteryReading?> ReadReportsAsync(Stream stream, bool wired, CancellationToken token)
        {
            var reply = new byte[ReportSize];
            if (wired)
            {
                // Only firmware that advertises battery support gets the custom request.
                await WriteCommandAsync(stream, ReadFeatures, token);
                if (!await ReadReportAsync(stream, reply, ReadFeatures, token) ||
                    (reply[4] & BatteryFeatureHighByte) == 0)
                {
                    return null;
                }
                await WriteCommandAsync(stream, ReadBattery, token);
            }

            byte transport = wired ? UsbTransport : RadioTransport;
            while (await ReadReportAsync(stream, reply, ReadBattery, token))
            {
                // The generic Keychron Link receiver can carry reports from multiple models.
                int voltage = reply[3] | (reply[4] << 8);
                if (reply[2] <= 100 && voltage > 0 && reply[5] <= 2 && reply[6] == transport && reply[7] == Q1HeModel)
                {
                    return new BatteryReading(reply[2], reply[5] == 1);
                }
                if (wired)
                {
                    break;
                }
            }
            return null;
        }

        private static Task WriteCommandAsync(Stream stream, byte command, CancellationToken token)
        {
            var request = new byte[ReportSize];
            request[1] = command;
            return stream.WriteAsync(request, 0, request.Length, token);
        }

        private static async Task<bool> ReadReportAsync(Stream stream, byte[] reply, byte command, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                int count = await stream.ReadAsync(reply, 0, reply.Length, token);
                if (count == 0)
                {
                    break;
                }
                if (count == ReportSize && reply[0] == 0 && reply[1] == command)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
