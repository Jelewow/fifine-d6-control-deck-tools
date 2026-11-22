using System;
using FifineTools;

namespace FifineMicMute
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return StreamDockConnection.Run(args, connection =>
                new DockPlugin(connection, new WindowsMicrophone(), new WindowsPlayback(),
                    AppDomain.CurrentDomain.BaseDirectory).RunAsync());
        }
    }
}
