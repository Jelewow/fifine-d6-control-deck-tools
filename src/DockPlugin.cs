using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FifineTools;

namespace FifineMicMute
{
    internal sealed class DockPlugin
    {
        private const string MicrophoneAction = "local.fifine.micmute.toggle";
        private const string PlaybackAction = "local.fifine.micmute.output";
        private const int PollIntervalMilliseconds = 500;

        private readonly StreamDockConnection connection;
        private readonly IMicrophone microphone;
        private readonly WindowsPlayback playback;
        private readonly Dictionary<MicrophoneState, string> microphoneImages;
        private readonly Dictionary<PlaybackState, string> playbackImages;
        private readonly Dictionary<string, VisibleKey> visibleKeys = new Dictionary<string, VisibleKey>();

        internal DockPlugin(StreamDockConnection connection, IMicrophone microphone, WindowsPlayback playback,
            string pluginDirectory)
        {
            this.connection = connection;
            this.microphone = microphone;
            this.playback = playback;
            string imageDirectory = Path.Combine(pluginDirectory, "images");
            microphoneImages = new Dictionary<MicrophoneState, string>
            {
                { MicrophoneState.Unavailable, LoadImage(imageDirectory, "unavailable.png") },
                { MicrophoneState.Unmuted, LoadImage(imageDirectory, "unmuted.png") },
                { MicrophoneState.Muted, LoadImage(imageDirectory, "muted.png") }
            };
            playbackImages = new Dictionary<PlaybackState, string>
            {
                { PlaybackState.Unavailable, LoadImage(imageDirectory, "output-unavailable.png") },
                { PlaybackState.Other, LoadImage(imageDirectory, "output-other.png") },
                { PlaybackState.Simgot, LoadImage(imageDirectory, "output-simgot.png") },
                { PlaybackState.Arzopa, LoadImage(imageDirectory, "output-arzopa.png") }
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
                        if (visibleKeys.Values.Any(key => key.Action == MicrophoneAction))
                        {
                            await UpdateAsync(MicrophoneAction);
                        }
                        if (visibleKeys.Values.Any(key => key.Action == PlaybackAction))
                        {
                            await UpdateAsync(PlaybackAction);
                        }
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
            if ((message.Action != MicrophoneAction && message.Action != PlaybackAction) || string.IsNullOrEmpty(context))
            {
                return;
            }

            switch (message.Name)
            {
                case "willAppear":
                    visibleKeys[context] = new VisibleKey(message.Action);
                    await UpdateAsync(message.Action);
                    break;
                case "keyDown":
                    await UpdateAsync(message.Action, toggle: true);
                    break;
                case "willDisappear":
                    visibleKeys.Remove(context);
                    break;
            }
        }

        private async Task UpdateAsync(string action, bool toggle = false)
        {
            string image = action == MicrophoneAction
                ? microphoneImages[toggle ? microphone.Toggle() : microphone.Read()]
                : playbackImages[toggle ? playback.Toggle() : playback.Read()];
            foreach (var key in visibleKeys)
            {
                if (key.Value.Action != action || key.Value.LastImage == image)
                {
                    continue;
                }

                await connection.SendAsync(new
                {
                    @event = "setImage",
                    context = key.Key,
                    payload = new { image, target = 0 }
                });
                key.Value.LastImage = image;
            }
        }

        private static string LoadImage(string imageDirectory, string filename)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(imageDirectory, filename));
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private sealed class VisibleKey
        {
            internal readonly string Action;
            internal string LastImage;

            internal VisibleKey(string action)
            {
                Action = action;
            }
        }
    }
}
