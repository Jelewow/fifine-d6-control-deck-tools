using System;
using System.Globalization;

namespace FifineTools.UnityControls
{
    internal enum UnityCommand : byte
    {
        TogglePlay = 1,
        TogglePause = 2,
        Step = 3
    }

    internal static class UnityCommandProtocol
    {
        internal const byte Rejected = 0;
        internal const byte Accepted = 1;
        internal const int TimeoutMilliseconds = 2000;
        internal const int RequestSize = 1 + sizeof(long);

        internal static string PipeName(int processId)
        {
            return "FifineTools.Unity." + processId.ToString(CultureInfo.InvariantCulture);
        }

        internal static byte[] CreateRequest(UnityCommand command)
        {
            var request = new byte[RequestSize];
            request[0] = (byte)command;
            long expires = DateTime.UtcNow.AddMilliseconds(TimeoutMilliseconds).Ticks;
            BitConverter.GetBytes(expires).CopyTo(request, 1);
            return request;
        }

        internal static bool TryReadRequest(byte[] request, out UnityCommand command)
        {
            command = (UnityCommand)request[0];
            long now = DateTime.UtcNow.Ticks;
            long expires = BitConverter.ToInt64(request, 1);
            return expires >= now && expires <= now + TimeoutMilliseconds * TimeSpan.TicksPerMillisecond &&
                command >= UnityCommand.TogglePlay && command <= UnityCommand.Step;
        }
    }
}
