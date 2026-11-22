#if UNITY_EDITOR_WIN
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FifineTools.UnityControls
{
    internal sealed class LocalPipe : IDisposable
    {
        private const uint PipeAccessDuplex = 0x00000003;
        private const uint PipeNoWait = 0x00000001;
        private const uint PipeReadModeMessage = 0x00000002;
        private const uint PipeTypeMessage = 0x00000004;
        private const uint PipeRejectRemoteClients = 0x00000008;
        private const uint TokenQuery = 0x0008;
        private const int TokenUser = 1;
        private const int ErrorInsufficientBuffer = 122;
        private const int ErrorPipeBusy = 231;
        private const int ErrorNoData = 232;
        private const int ErrorPipeConnected = 535;
        private const int ErrorPipeListening = 536;

        private readonly SafeFileHandle handle;

        private LocalPipe(SafeFileHandle handle)
        {
            this.handle = handle;
        }

        internal static LocalPipe CreateServer(string name)
        {
            // The DACL grants access to the editor's Windows account only.
            string dacl = "D:P(A;;GA;;;" + GetProcessUserSid() + ")";
            IntPtr descriptor;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(dacl, 1, out descriptor, IntPtr.Zero))
            {
                throw new Win32Exception();
            }

            try
            {
                var security = new SecurityAttributes
                {
                    Length = Marshal.SizeOf<SecurityAttributes>(),
                    Descriptor = descriptor
                };
                SafeFileHandle handle = CreateNamedPipeW(@"\\.\pipe\" + name, PipeAccessDuplex,
                    PipeNoWait | PipeTypeMessage | PipeReadModeMessage | PipeRejectRemoteClients,
                    1, 1024, 1024, 0, ref security);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    if (error == ErrorPipeBusy)
                    {
                        throw new IOException("The previous Unity pipe is still closing.");
                    }
                    throw new Win32Exception(error);
                }
                return new LocalPipe(handle);
            }
            finally
            {
                LocalFree(descriptor);
            }

        }

        internal bool TryConnect()
        {
            // In nonblocking mode a successful call means the pipe started listening.
            if (ConnectNamedPipe(handle, IntPtr.Zero))
            {
                return false;
            }
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorPipeConnected)
            {
                return true;
            }
            if (error == ErrorPipeListening)
            {
                return false;
            }
            throw new IOException("The Unity pipe connection closed.", new Win32Exception(error));
        }

        internal bool TryReadRequest(byte[] packet)
        {
            int count;
            if (ReadFile(handle, packet, packet.Length, out count, IntPtr.Zero))
            {
                if (count != packet.Length)
                {
                    throw new IOException("The Unity command has an invalid size.");
                }
                return true;
            }
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNoData)
            {
                return false;
            }
            throw new IOException("The Unity pipe could not read a command.", new Win32Exception(error));
        }

        internal bool TryReply(byte result)
        {
            int count;
            return WriteFile(handle, new[] { result }, 1, out count, IntPtr.Zero) && count == 1;
        }

        public void Dispose()
        {
            handle.Dispose();
        }

        private static string GetProcessUserSid()
        {
            // Unity's bundled Mono does not implement WindowsIdentity.User.
            SafeFileHandle token;
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token))
            {
                throw new Win32Exception();
            }
            using (token)
            {
                int size;
                GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out size);
                int error = Marshal.GetLastWin32Error();
                if (error != ErrorInsufficientBuffer)
                {
                    throw new Win32Exception(error);
                }

                IntPtr user = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, TokenUser, user, size, out size))
                    {
                        throw new Win32Exception();
                    }
                    IntPtr sidText;
                    if (!ConvertSidToStringSidW(Marshal.ReadIntPtr(user), out sidText))
                    {
                        throw new Win32Exception();
                    }
                    try
                    {
                        return Marshal.PtrToStringUni(sidText);
                    }
                    finally
                    {
                        LocalFree(sidText);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(user);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            internal int Length;
            internal IntPtr Descriptor;
            internal int InheritHandle;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision,
            out IntPtr descriptor, IntPtr size);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateNamedPipeW(string name, uint openMode, uint pipeMode,
            uint maxInstances, uint outputBufferSize, uint inputBufferSize, uint timeout, ref SecurityAttributes security);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConnectNamedPipe(SafeFileHandle pipe, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadFile(SafeFileHandle pipe, [Out] byte[] bytes, int size, out int count, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteFile(SafeFileHandle pipe, byte[] bytes, int size, out int count, IntPtr overlapped);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out SafeFileHandle token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(SafeFileHandle token, int informationClass, IntPtr buffer,
            int bufferSize, out int resultSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);
    }
}
#endif
