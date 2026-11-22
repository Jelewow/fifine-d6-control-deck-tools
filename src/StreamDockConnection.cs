using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FifineTools
{
    internal sealed class StreamDockConnection
    {
        private const int MaxMessageBytes = 1024 * 1024;
        private readonly WebSocket socket;
        private readonly JavaScriptSerializer reader = new JavaScriptSerializer();
        private readonly JavaScriptSerializer writer = new JavaScriptSerializer();
        private readonly byte[] receiveBuffer = new byte[8192];

        private StreamDockConnection(WebSocket socket)
        {
            this.socket = socket;
        }

        internal static int Run(string[] args, Func<StreamDockConnection, Task> handleEvents)
        {
            int port;
            string pluginUuid = GetOption(args, "-pluginUUID");
            string registerEvent = GetOption(args, "-registerEvent");
            if (!int.TryParse(GetOption(args, "-port"), out port) || port < 1 || port > 65535 ||
                string.IsNullOrWhiteSpace(pluginUuid) || string.IsNullOrWhiteSpace(registerEvent))
            {
                Console.Error.WriteLine("Launch this plugin from FIFINE Control Deck.");
                return 2;
            }

            try
            {
                ConnectAsync(port, pluginUuid, registerEvent, handleEvents).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        private static async Task ConnectAsync(int port, string pluginUuid, string registerEvent,
            Func<StreamDockConnection, Task> handleEvents)
        {
            using (var socket = new ClientWebSocket())
            {
                socket.Options.Proxy = null;
                var address = new UriBuilder("ws", "127.0.0.1", port).Uri;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await socket.ConnectAsync(address, timeout.Token);
                }

                var connection = new StreamDockConnection(socket);
                await connection.SendAsync(new { @event = registerEvent, uuid = pluginUuid });
                await handleEvents(connection);

                if (socket.State == WebSocketState.CloseReceived)
                {
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
                    }
                }
            }
        }

        internal Task SendAsync(object message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(writer.Serialize(message));
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        internal async Task<DockEvent> ReceiveAsync()
        {
            while (true)
            {
                string message = await ReceiveTextAsync();
                if (message == null)
                {
                    return null;
                }

                Dictionary<string, object> fields;
                try
                {
                    fields = reader.Deserialize<Dictionary<string, object>>(message);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (fields != null)
                {
                    return new DockEvent(GetString(fields, "event"), GetString(fields, "action"), GetString(fields, "context"));
                }
            }
        }

        private async Task<string> ReceiveTextAsync()
        {
            using (var message = new MemoryStream())
            {
                WebSocketReceiveResult frame;
                do
                {
                    frame = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                    if (frame.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    if (message.Length + frame.Count > MaxMessageBytes)
                    {
                        throw new InvalidDataException("Control Deck message exceeds 1 MiB.");
                    }

                    message.Write(receiveBuffer, 0, frame.Count);
                }
                while (!frame.EndOfMessage);

                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }
        }

        private static string GetOption(string[] args, string option)
        {
            int index = Array.IndexOf(args, option);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static string GetString(Dictionary<string, object> fields, string key)
        {
            object value;
            return fields.TryGetValue(key, out value) ? value as string : null;
        }
    }

    internal sealed class DockEvent
    {
        internal readonly string Name;
        internal readonly string Action;
        internal readonly string Context;

        internal DockEvent(string name, string action, string context)
        {
            Name = name;
            Action = action;
            Context = context;
        }
    }
}
