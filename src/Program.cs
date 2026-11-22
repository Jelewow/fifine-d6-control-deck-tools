using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FifineMicMute
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int port;
            string pluginUuid = GetOption(args, "-pluginUUID");
            string registerEvent = GetOption(args, "-registerEvent");
            if (!int.TryParse(GetOption(args, "-port"), out port) || port < 1 || port > 65535 ||
                string.IsNullOrWhiteSpace(pluginUuid) || string.IsNullOrWhiteSpace(registerEvent))
            {
                Console.Error.WriteLine("Launch MicMute.exe from FIFINE Control Deck.");
                return 2;
            }

            try
            {
                RunAsync(port, pluginUuid, registerEvent).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        private static async Task RunAsync(int port, string pluginUuid, string registerEvent)
        {
            using (var socket = new ClientWebSocket())
            {
                socket.Options.Proxy = null;
                var address = new UriBuilder("ws", "127.0.0.1", port).Uri;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await socket.ConnectAsync(address, timeout.Token);
                }

                var plugin = new DockPlugin(socket, new WindowsMicrophone(), AppDomain.CurrentDomain.BaseDirectory);
                await plugin.RunAsync(pluginUuid, registerEvent);

                if (socket.State == WebSocketState.CloseReceived)
                {
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
                    }
                }
            }
        }

        private static string GetOption(string[] args, string option)
        {
            int index = Array.IndexOf(args, option);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
