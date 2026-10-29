using HarmonyLib;

namespace NOVR.MotionControls;

internal static class PilotMotionInputPatch
{
    // Hook before the game's FilterInputs step, retaining flight assists and network replication.
    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    private static class Axes
    {
        [HarmonyPostfix]
        private static void Postfix(Pilot ___pilot, ControlInputs ___controlInputs, float ___pilotStrength)
        {
            var motion = MotionFlightController.Instance;
            if (motion == null || !motion.SampleFor(___pilot)) return;
            if (___pilotStrength < 0.2f)
            {
                motion.ResetGrips();
                return;
            }
            if (!motion.StickHeld) return;
            ___controlInputs.pitch = motion.Pitch;
            ___controlInputs.roll = motion.Roll;
            if (motion.HasYaw) ___controlInputs.yaw = motion.Yaw;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
    private static class Throttle
    {
        [HarmonyPostfix]
        private static void Postfix(Pilot ___pilot, ControlInputs ___controlInputs, bool ___collective,
            float ___pilotStrength, ref float ___simulatedThrottle)
        {
            var motion = MotionFlightController.Instance;
            motion?.ObserveThrottle(___pilot, ___controlInputs.throttle);
            if (motion == null || !motion.SampleFor(___pilot)) return;
            if (___pilotStrength < 0.2f)
            {
                motion.ResetGrips();
                return;
            }
            if (!motion.ThrottleHeld) return;
            ___controlInputs.throttle = motion.Throttle;
            // Seed the native accumulator so letting go keeps thrust instead of snapping back.
            ___simulatedThrottle = FlightControlMath.ToSimulatedThrottle(motion.Throttle,
                PlayerSettings.throttleUseNegative, ___collective && PlayerSettings.invertCollective);
            motion.ObserveThrottle(___pilot, motion.Throttle);
        }
    }
}
