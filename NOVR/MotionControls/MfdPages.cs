namespace NOVR.MotionControls;

internal enum MfdPage { Weapons, Map, Flight, EngineFuel, Systems, Tactical, Sensor }

internal static class MfdPages
{
    internal const int Count = 7;
    internal static string Title(MfdPage page) => page switch
    {
        MfdPage.Weapons => "WEAPONS / CM", MfdPage.Map => "MAP", MfdPage.Flight => "FLIGHT",
        MfdPage.EngineFuel => "ENGINE / FUEL", MfdPage.Systems => "SYSTEMS", MfdPage.Tactical => "TACTICAL",
        MfdPage.Sensor => "SENSOR", _ => "DISPLAY"
    };
    // Native widgets have one owner. Selecting the other display's page swaps
    // them, so neither display goes blank and no native map/UI is duplicated.
    internal static (MfdPage left, MfdPage right) Cycle(MfdPage left, MfdPage right, bool leftDisplay, int direction)
    {
        var current = leftDisplay ? left : right;
        var next = (MfdPage)(((int)current + (direction < 0 ? Count - 1 : 1)) % Count);
        if (leftDisplay) return (next, next == right ? current : right);
        return (next == left ? current : left, next);
    }
}
