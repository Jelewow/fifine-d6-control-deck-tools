using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FifineMicMute
{
    internal sealed class DockPlugin
    {
        private const string ActionUuid = "local.fifine.micmute.toggle";
        private const int PollIntervalMilliseconds = 500;
        private const int MaxMessageBytes = 1024 * 1024;

        private readonly WebSocket socket;
        private readonly IMicrophone microphone;
        private readonly Dictionary<MicrophoneState, string> images;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly byte[] receiveBuffer = new byte[8192];
        private readonly Dictionary<string, VisibleKey> visibleKeys = new Dictionary<string, VisibleKey>();

        internal DockPlugin(WebSocket socket, IMicrophone microphone, string pluginDirectory)
        {
            this.socket = socket;
            this.microphone = microphone;
            string imageDirectory = Path.Combine(pluginDirectory, "images");
            images = new Dictionary<MicrophoneState, string>
            {
                { MicrophoneState.Unavailable, LoadImage(imageDirectory, "unavailable.png") },
                { MicrophoneState.Unmuted, LoadImage(imageDirectory, "unmuted.png") },
                { MicrophoneState.Muted, LoadImage(imageDirectory, "muted.png") }
            };
        }

        internal async Task RunAsync(string pluginUuid, string registerEvent)
        {
            await SendAsync(new { @event = registerEvent, uuid = pluginUuid });

            Task<string> nextMessage = ReceiveAsync();
            Task nextPoll = null;
            while (true)
            {
                if (visibleKeys.Count > 0)
                {
                    nextPoll = nextPoll ?? Task.Delay(PollIntervalMilliseconds);
                    if (await Task.WhenAny(nextMessage, nextPoll) == nextPoll)
                    {
                        await UpdateVisibleKeysAsync(microphone.Read());
                        nextPoll = null;
                        continue;
                    }
                }

                string message = await nextMessage;
                if (message == null)
                {
                    return;
                }

                await HandleEventAsync(message);
                nextMessage = ReceiveAsync();
                if (visibleKeys.Count == 0)
                {
                    nextPoll = null;
                }
            }
        }

        private async Task HandleEventAsync(string message)
        {
            Dictionary<string, object> fields;
            try
            {
                fields = json.Deserialize<Dictionary<string, object>>(message);
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (fields == null || GetString(fields, "action") != ActionUuid)
            {
                return;
            }

            string context = GetString(fields, "context");
            if (string.IsNullOrEmpty(context))
            {
                return;
            }

            switch (GetString(fields, "event"))
            {
                case "willAppear":
                    visibleKeys[context] = new VisibleKey();
                    await UpdateVisibleKeysAsync(microphone.Read());
                    break;
                case "keyDown":
                    await UpdateVisibleKeysAsync(microphone.Toggle());
                    break;
                case "willDisappear":
                    visibleKeys.Remove(context);
                    break;
            }
        }

        private async Task UpdateVisibleKeysAsync(MicrophoneState state)
        {
            foreach (var key in visibleKeys)
            {
                if (key.Value.LastState == state)
                {
                    continue;
                }

                await SendAsync(new
                {
                    @event = "setImage",
                    context = key.Key,
                    payload = new { image = images[state], target = 0 }
                });
                key.Value.LastState = state;
            }
        }

        private Task SendAsync(object message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json.Serialize(message));
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task<string> ReceiveAsync()
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

        private static string LoadImage(string imageDirectory, string filename)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(imageDirectory, filename));
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private static string GetString(Dictionary<string, object> fields, string key)
        {
            object value;
            return fields.TryGetValue(key, out value) ? value as string : null;
        }

        private sealed class VisibleKey
        {
            internal MicrophoneState? LastState;
        }
    }
}
