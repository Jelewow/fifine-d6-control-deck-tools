using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FifineTools;

namespace FifineTools.Battery
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return StreamDockConnection.Run(args, connection => new BatteryPlugin(connection).RunAsync());
        }
    }

    internal sealed class BatteryPlugin
    {
        private const string MouseAction = "local.fifine.battery.aurora";
        private const string KeyboardAction = "local.fifine.battery.keychron";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

        private readonly StreamDockConnection connection;
        private readonly BatteryCache cache;
        private readonly Dictionary<BatteryDevice, int?> lastKnown = new Dictionary<BatteryDevice, int?>();
        private readonly Dictionary<string, VisibleKey> keys = new Dictionary<string, VisibleKey>();

        internal BatteryPlugin(StreamDockConnection connection)
        {
            this.connection = connection;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            cache = new BatteryCache(Path.Combine(local, "FifineTools", "Battery"));
        }

        internal async Task RunAsync()
        {
            Task<DockEvent> message = connection.ReceiveAsync();
            Task timer = null;
            while (true)
            {
                if (keys.Count > 0)
                {
                    timer = timer ?? Task.Delay(RefreshInterval);
                    if (await Task.WhenAny(message, timer) == timer)
                    {
                        await RefreshAsync(BatteryDevice.Keyboard);
                        await RefreshAsync(BatteryDevice.Mouse);
                        timer = null;
                        continue;
                    }
                }

                DockEvent received = await message;
                if (received == null)
                {
                    return;
                }
                await HandleAsync(received);
                message = connection.ReceiveAsync();
                if (keys.Count == 0)
                {
                    timer = null;
                }
            }
        }

        private async Task HandleAsync(DockEvent message)
        {
            if (string.IsNullOrEmpty(message.Context))
            {
                return;
            }
            BatteryDevice device;
            switch (message.Action)
            {
                case MouseAction: device = BatteryDevice.Mouse; break;
                case KeyboardAction: device = BatteryDevice.Keyboard; break;
                default: return;
            }

            switch (message.Name)
            {
                case "willAppear":
                    keys[message.Context] = new VisibleKey(device);
                    await RefreshAsync(device);
                    break;
                case "keyDown":
                    if (keys.ContainsKey(message.Context))
                    {
                        await RefreshAsync(device);
                    }
                    break;
                case "willDisappear":
                    keys.Remove(message.Context);
                    break;
            }
        }

        private async Task RefreshAsync(BatteryDevice device)
        {
            var visible = keys.Where(key => key.Value.Device == device).ToArray();
            if (visible.Length == 0)
            {
                return;
            }

            int? saved;
            if (!lastKnown.TryGetValue(device, out saved))
            {
                saved = cache.Read(device);
                lastKnown[device] = saved;
            }
            BatteryReading? reading = device == BatteryDevice.Mouse
                ? await AuroraBattery.ReadAsync()
                : ReadKeyboard();
            if (reading.HasValue && reading.Value.Percent != saved)
            {
                cache.Save(device, reading.Value.Percent);
                lastKnown[device] = reading.Value.Percent;
            }

            var display = new BatteryDisplay(reading, saved);
            string image = null;
            foreach (var key in visible)
            {
                if (Nullable.Equals(key.Value.Displayed, (BatteryDisplay?)display))
                {
                    continue;
                }
                image = image ?? "data:image/png;base64," + Convert.ToBase64String(BatteryIcon.Render(device, display));
                await connection.SendAsync(new { @event = "setImage", context = key.Key, payload = new { image, target = 0 } });
                key.Value.Displayed = display;
            }
        }

        private static BatteryReading? ReadKeyboard()
        {
            int? percent = WindowsDevices.ReadKeychronBattery();
            return percent.HasValue ? new BatteryReading(percent.Value, false) : (BatteryReading?)null;
        }

        private sealed class VisibleKey
        {
            internal readonly BatteryDevice Device;
            internal BatteryDisplay? Displayed;

            internal VisibleKey(BatteryDevice device)
            {
                Device = device;
            }
        }
    }
}
