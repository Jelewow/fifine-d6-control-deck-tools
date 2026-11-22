#if UNITY_EDITOR_WIN
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace FifineTools.UnityControls
{
    [InitializeOnLoad]
    internal static class UnityControlServer
    {
        private static readonly string PipeName;
        private static readonly byte[] Packet = new byte[UnityCommandProtocol.RequestSize];
        private static LocalPipe pipe;
        private static bool connected;
        private static double readDeadline;
        private static double retryAfter;

        static UnityControlServer()
        {
            using (Process editor = Process.GetCurrentProcess())
            {
                PipeName = UnityCommandProtocol.PipeName(editor.Id);
            }
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        private static void Poll()
        {
            try
            {
                if (pipe == null)
                {
                    if (EditorApplication.timeSinceStartup < retryAfter)
                    {
                        return;
                    }
                    pipe = LocalPipe.CreateServer(PipeName);
                }
                if (!connected)
                {
                    if (!pipe.TryConnect())
                    {
                        return;
                    }
                    connected = true;
                    readDeadline = EditorApplication.timeSinceStartup + UnityCommandProtocol.TimeoutMilliseconds / 1000.0;
                }
                if (!pipe.TryReadRequest(Packet))
                {
                    if (EditorApplication.timeSinceStartup >= readDeadline)
                    {
                        CloseConnection();
                    }
                    return;
                }

                UnityCommand command;
                bool accepted = UnityCommandProtocol.TryReadRequest(Packet, out command) && CanExecute(command);
                // Send the reply before Play Mode can trigger an assembly reload.
                bool replied = pipe.TryReply(accepted ? UnityCommandProtocol.Accepted : UnityCommandProtocol.Rejected);
                CloseConnection();
                if (replied && accepted)
                {
                    Execute(command);
                }
            }
            catch (IOException)
            {
                CloseConnection();
                retryAfter = EditorApplication.timeSinceStartup + 0.1;
            }
            catch (Win32Exception error)
            {
                Stop();
                UnityEngine.Debug.LogWarning("FIFINE Unity Controls could not open its named pipe: " + error.Message);
            }
        }

        private static bool CanExecute(UnityCommand command)
        {
            bool playing = EditorApplication.isPlaying;
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                playing == EditorApplication.isPlayingOrWillChangePlaymode &&
                (command == UnityCommand.TogglePlay || playing);
        }

        private static void Execute(UnityCommand command)
        {
            switch (command)
            {
                case UnityCommand.TogglePlay:
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;
                    break;
                case UnityCommand.TogglePause:
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    break;
                case UnityCommand.Step:
                    EditorApplication.Step();
                    break;
            }
        }

        private static void Stop()
        {
            EditorApplication.update -= Poll;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
            CloseConnection();
        }

        private static void CloseConnection()
        {
            if (pipe != null)
            {
                pipe.Dispose();
            }
            pipe = null;
            connected = false;
        }
    }
}
#endif
