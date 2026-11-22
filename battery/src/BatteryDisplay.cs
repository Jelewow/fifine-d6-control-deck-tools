namespace FifineTools.Battery
{
    internal enum BatteryDevice
    {
        Mouse,
        Keyboard
    }

    internal struct BatteryReading
    {
        internal readonly int Percent;
        internal readonly bool Charging;

        internal BatteryReading(int percent, bool charging)
        {
            Percent = percent;
            Charging = charging;
        }
    }

    internal struct BatteryDisplay
    {
        internal readonly int? Percent;
        internal readonly bool LastKnown;
        internal readonly bool Charging;

        internal BatteryDisplay(BatteryReading? reading, int? lastKnown)
        {
            Percent = reading.HasValue ? reading.Value.Percent : lastKnown;
            LastKnown = !reading.HasValue && lastKnown.HasValue;
            Charging = reading.HasValue && reading.Value.Charging;
        }
    }
}
