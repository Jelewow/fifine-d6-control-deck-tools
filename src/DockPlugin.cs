using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FifineTools;

namespace FifineMicMute
{
    internal sealed class DockPlugin
    {
        private const string ActionUuid = "local.fifine.micmute.toggle";
        private const int PollIntervalMilliseconds = 500;

        private readonly StreamDockConnection connection;
        private readonly IMicrophone microphone;
        private readonly Dictionary<MicrophoneState, string> images;
        private readonly Dictionary<string, VisibleKey> visibleKeys = new Dictionary<string, VisibleKey>();

        internal DockPlugin(StreamDockConnection connection, IMicrophone microphone, string pluginDirectory)
        {
            this.connection = connection;
            this.microphone = microphone;
            string imageDirectory = Path.Combine(pluginDirectory, "images");
            images = new Dictionary<MicrophoneState, string>
            {
                { MicrophoneState.Unavailable, LoadImage(imageDirectory, "unavailable.png") },
                { MicrophoneState.Unmuted, LoadImage(imageDirectory, "unmuted.png") },
                { MicrophoneState.Muted, LoadImage(imageDirectory, "muted.png") }
            };
        }

        internal async Task RunAsync()
        {
            Task<DockEvent> nextMessage = connection.ReceiveAsync();
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

                DockEvent message = await nextMessage;
                if (message == null)
                {
                    return;
                }

                await HandleEventAsync(message);
                nextMessage = connection.ReceiveAsync();
                if (visibleKeys.Count == 0)
                {
                    nextPoll = null;
                }
            }
        }

        private async Task HandleEventAsync(DockEvent message)
        {
            string context = message.Context;
            if (message.Action != ActionUuid || string.IsNullOrEmpty(context))
            {
                return;
            }

            switch (message.Name)
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

                await connection.SendAsync(new
                {
                    @event = "setImage",
                    context = key.Key,
                    payload = new { image = images[state], target = 0 }
                });
                key.Value.LastState = state;
            }
        }

        private static string LoadImage(string imageDirectory, string filename)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(imageDirectory, filename));
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private sealed class VisibleKey
        {
            internal MicrophoneState? LastState;
        }
    }
}
