using System.Threading.Tasks;
using FifineTools;

namespace FifineTools.UnityControls
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return StreamDockConnection.Run(args, HandleEventsAsync);
        }

        private static async Task HandleEventsAsync(StreamDockConnection connection)
        {
            DockEvent message;
            while ((message = await connection.ReceiveAsync()) != null)
            {
                if (message.Name != "keyDown" || string.IsNullOrEmpty(message.Context))
                {
                    continue;
                }

                UnityCommand command;
                switch (message.Action)
                {
                    case "local.fifine.unitycontrols.play":
                        command = UnityCommand.TogglePlay;
                        break;
                    case "local.fifine.unitycontrols.pause":
                        command = UnityCommand.TogglePause;
                        break;
                    case "local.fifine.unitycontrols.step":
                        command = UnityCommand.Step;
                        break;
                    default:
                        continue;
                }

                bool accepted = await UnityClient.TryExecuteAsync(command);
                await connection.SendAsync(new
                {
                    @event = accepted ? "showOk" : "showAlert",
                    context = message.Context
                });
            }
        }
    }
}
