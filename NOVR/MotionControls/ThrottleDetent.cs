using System;

namespace NOVR.MotionControls;

// Extra physical travel creates a stable MIL shelf without a timer or stolen
// button. The inverse mapping preserves the command on release/regrab.
internal static class ThrottleDetent
{
    internal const float TravelMeters = .016f;
    internal static float Input(float command, float mil, float width) =>
        command < mil ? command : command > mil ? command + width : mil + width * .5f;
    internal static float Output(float input, float mil, float width) =>
        !FlightControlMath.IsFinite(input) ? mil : Math.Max(0, Math.Min(1,
            input < mil ? input : input <= mil + width ? mil : input - width));
}
