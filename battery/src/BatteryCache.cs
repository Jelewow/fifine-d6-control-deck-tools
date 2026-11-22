using System;
using System.Globalization;
using System.IO;

namespace FifineTools.Battery
{
    internal sealed class BatteryCache
    {
        private readonly string directory;

        internal BatteryCache(string directory)
        {
            this.directory = directory;
        }

        internal int? Read(BatteryDevice device)
        {
            try
            {
                int value;
                if (int.TryParse(File.ReadAllText(CachePath(device)), NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                    value <= 100)
                {
                    return value;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        internal void Save(BatteryDevice device, int percent)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = CachePath(device);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, percent.ToString(CultureInfo.InvariantCulture));
                    if (File.Exists(path))
                    {
                        File.Replace(temporary, path, null);
                    }
                    else
                    {
                        File.Move(temporary, path);
                    }
                }
                finally
                {
                    File.Delete(temporary);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private string CachePath(BatteryDevice device)
        {
            return Path.Combine(directory, device == BatteryDevice.Mouse ? "aurora.txt" : "keychron-q1-he.txt");
        }
    }
}
