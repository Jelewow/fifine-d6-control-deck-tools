using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FifineTools.Battery
{
    internal static class WindowsDevices
    {
        private const uint BufferTooSmall = 0x1a;
        private const uint PresentEnumerator = 0x101;
        private const uint PresentInterfaces = 0;
        private const uint Disconnected = 0x02000000;
        private const uint ByteProperty = 0x03;
        private const uint StringProperty = 0x12;

        private static readonly DevicePropertyKey BluetoothBattery =
            new DevicePropertyKey("104ea319-6ee2-4701-bd47-8ddbf425bbe5", 2);
        private static readonly DevicePropertyKey FriendlyName =
            new DevicePropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0", 14);
        private static readonly DevicePropertyKey DisplayName =
            new DevicePropertyKey("b725f130-47ef-101a-a5f1-02608c9eebac", 10);

        internal static IEnumerable<string> HidInterfaces(Guid hidClass)
        {
            // The device list can grow between querying its size and reading it.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                uint length;
                if (CM_Get_Device_Interface_List_SizeW(out length, ref hidClass, null, PresentInterfaces) != 0 ||
                    length <= 1)
                {
                    break;
                }
                var list = new char[length];
                uint result = CM_Get_Device_Interface_ListW(ref hidClass, null, list, length, PresentInterfaces);
                if (result == 0)
                {
                    return ReadMultiString(list);
                }
                if (result != BufferTooSmall)
                {
                    break;
                }
            }
            return new string[0];
        }

        internal static int? ReadKeychronBattery()
        {
            foreach (string enumerator in new[] { "BTHLE", "BTHENUM" })
            {
                foreach (string id in DeviceIds(enumerator))
                {
                    uint device;
                    uint status;
                    uint problem;
                    if (CM_Locate_DevNodeW(out device, id, 0) != 0 ||
                        CM_Get_DevNode_Status(out status, out problem, device, 0) != 0 || (status & Disconnected) != 0)
                    {
                        continue;
                    }

                    string name = ReadName(device, FriendlyName) ?? ReadName(device, DisplayName);
                    if (!string.Equals(name, "Keychron Q1 HE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = new byte[1];
                    var property = BluetoothBattery;
                    uint size = 1;
                    uint type;
                    if (CM_Get_DevNode_PropertyW(device, ref property, out type, value, ref size, 0) == 0 &&
                        type == ByteProperty && size == 1 && value[0] <= 100)
                    {
                        return value[0];
                    }
                }
            }
            return null;
        }

        private static IEnumerable<string> DeviceIds(string enumerator)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                uint length;
                if (CM_Get_Device_ID_List_SizeW(out length, enumerator, PresentEnumerator) != 0 || length <= 1)
                {
                    break;
                }
                var list = new char[length];
                uint result = CM_Get_Device_ID_ListW(enumerator, list, length, PresentEnumerator);
                if (result == 0)
                {
                    return ReadMultiString(list);
                }
                if (result != BufferTooSmall)
                {
                    break;
                }
            }
            return new string[0];
        }

        private static IEnumerable<string> ReadMultiString(char[] buffer)
        {
            return new string(buffer).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string ReadName(uint device, DevicePropertyKey property)
        {
            var buffer = new byte[512];
            uint size = (uint)buffer.Length;
            uint type;
            if (CM_Get_DevNode_PropertyW(device, ref property, out type, buffer, ref size, 0) != 0 ||
                type != StringProperty)
            {
                return null;
            }
            return Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0').Trim();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DevicePropertyKey
        {
            private Guid format;
            private uint id;

            internal DevicePropertyKey(string format, uint id)
            {
                this.format = new Guid(format);
                this.id = id;
            }
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Get_Device_Interface_List_SizeW(out uint length, ref Guid type, string id, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Get_Device_Interface_ListW(ref Guid type, string id, [Out] char[] buffer,
            uint length, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string enumerator, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Get_Device_ID_ListW(string enumerator, [Out] char[] buffer, uint length, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Locate_DevNodeW(out uint device, string id, uint flags);

        [DllImport("cfgmgr32.dll", ExactSpelling = true)]
        private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint device, uint flags);

        [DllImport("cfgmgr32.dll", ExactSpelling = true)]
        private static extern uint CM_Get_DevNode_PropertyW(uint device, ref DevicePropertyKey property, out uint type,
            [Out] byte[] buffer, ref uint bytes, uint flags);
    }
}
