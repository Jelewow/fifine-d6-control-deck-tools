using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FifineTools.UnityControls
{
    internal static class UnityClient
    {
        internal static async Task<bool> TryExecuteAsync(UnityCommand command)
        {
            int? editor = FindEditor();
            if (!editor.HasValue)
            {
                return false;
            }

            using (var pipe = new NamedPipeClientStream(".", UnityCommandProtocol.PipeName(editor.Value),
                PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                try
                {
                    await pipe.ConnectAsync(350);
                    using (var timeout = new CancellationTokenSource(UnityCommandProtocol.TimeoutMilliseconds))
                    using (timeout.Token.Register(pipe.Dispose))
                    {
                        byte[] request = UnityCommandProtocol.CreateRequest(command);
                        await pipe.WriteAsync(request, 0, request.Length, timeout.Token);
                        var response = new byte[1];
                        return await pipe.ReadAsync(response, 0, response.Length, timeout.Token) == 1 &&
                            response[0] == UnityCommandProtocol.Accepted;
                    }
                }
                catch (TimeoutException) { return false; }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
                catch (OperationCanceledException) { return false; }
                catch (ObjectDisposedException) { return false; }
            }
        }

        private static int? FindEditor()
        {
            int session;
            using (Process current = Process.GetCurrentProcess())
            {
                session = current.SessionId;
            }

            uint foreground;
            GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
            int? focusedEditor = null;
            int? onlyEditor = null;
            int editors = 0;
            foreach (Process process in Process.GetProcessesByName("Unity"))
            {
                using (process)
                {
                    try
                    {
                        if (process.SessionId != session || process.HasExited)
                        {
                            continue;
                        }

                        onlyEditor = process.Id;
                        editors++;
                        if (process.Id == foreground)
                        {
                            focusedEditor = process.Id;
                        }
                    }
                    catch (InvalidOperationException) { } // The editor just exited.
                    catch (Win32Exception) { }
                }
            }

            return focusedEditor ?? (editors == 1 ? onlyEditor : null);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
