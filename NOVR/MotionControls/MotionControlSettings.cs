using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.MotionControls;

public sealed class MotionControlSettings
{
    public readonly ConfigEntry<bool> Enabled;
    public readonly ConfigEntry<bool> LeftHanded;
    public readonly ConfigEntry<Vector3> StickPosition;
    public readonly ConfigEntry<Vector3> ThrottlePosition;
    public readonly ConfigEntry<float> StickAngle;
    public readonly ConfigEntry<float> Deadzone;
    public readonly ConfigEntry<float> ResponseExponent;
    public readonly ConfigEntry<float> ThrottleTravel;
    public readonly ConfigEntry<float> GrabRadius;
    public readonly ConfigEntry<bool> ThumbstickYaw, TwistYaw;
    public readonly ConfigEntry<float> YawAngle, HapticStrength, InteractionVolume, SleeveThickness;
    public readonly ConfigEntry<bool> MilDetent, HelmetNightVision, RiggedGloves, PilotArms;
    public readonly ConfigEntry<Vector3> GloveOffset, GloveRotation;
    public readonly ConfigEntry<bool> StickTapGrip, ThrottleTapGrip;

    public MotionControlSettings(ConfigFile config)
    {
        const string section = "Motion Controls";
        Enabled = config.Bind(section, "Enabled", false, "Experimental virtual stick and throttle. F7 toggles in flight. Requires tracked OpenXR controllers.");
        LeftHanded = config.Bind(section, "Left Handed", false, "Use the left hand for the stick and the right for the throttle. Default positions are mirrored.");
        StickPosition = config.Bind(section, "Stick Position", new Vector3(0.22f, -0.36f, 0.32f), "Neutral grip position in cockpit meters, relative to the recentered eye position. F8 places controls at your hands.");
        ThrottlePosition = config.Bind(section, "Throttle Position", new Vector3(-0.24f, -0.36f, 0.32f), "Center of throttle travel in cockpit meters. F8 or holding both thumbstick clicks places controls at your hands.");
        StickAngle = Float(config, "Stick Angle", 25f, 5f, 60f, "Degrees of wrist tilt for full pitch or roll.");
        Deadzone = Float(config, "Stick Deadzone", 0.06f, 0f, 0.4f, "Fraction of stick travel ignored around center.");
        ResponseExponent = Float(config, "Stick Response", 1.5f, 1f, 3f, "Response curve; higher values make small movements less sensitive.");
        ThrottleTravel = Float(config, "Throttle Travel", 0.22f, 0.05f, 0.6f, "Meters from minimum to maximum command: lift the collective in helicopters; slide the throttle forward in other aircraft.");
        RiggedGloves = config.Bind(section, "Rigged Gloves", true, "Show rigged gloves using the installed pilot material. Grip curls three fingers and thumb; trigger curls the index. Uses controller input, not optical finger tracking.");
        SleeveThickness = Float(config, "Pilot Sleeve Thickness", .82f, .6f, 1.2f, "Visual sleeve width relative to the native pilot. Changes thickness around the arm bones, preserving length, reach and controller positions.");
        PilotArms = config.Bind(section, "Pilot Sleeves", true, "Render-only two-bone arm posing using installed pilot sleeve geometry. Sleeves hide beyond their reach; tracked gloves remain visible.");
        GloveOffset = config.Bind(section, "Glove Wrist Offset", new Vector3(0,-.015f,-.055f), "Visual wrist offset from the runtime grip pose in meters. X mirrors for the right hand. Does not change interaction hit positions.");
        GloveRotation = config.Bind(section, "Glove Wrist Rotation", new Vector3(25,0,0), "Visual left glove rotation in degrees; Y/Z mirror for the right hand. Does not change controller pointers or flight inputs.");
        HelmetNightVision = config.Bind(section, "Helmet Night Vision Gesture", true, "Raise either free hand to the forehead/brow and make a fresh trigger press to toggle native night vision. A short pulse marks the gesture area. Disabled in menus, map and layout editing.");
        MilDetent = config.Bind(section, "MIL Throttle Detent", true, "Afterburning jets only: a 1.6 cm shelf at the installed engine's military-power boundary. Push through for afterburner; no timer or extra button.");
        InteractionVolume = Float(config, "Interaction Sound Volume", .25f, 0f, 1f, "Volume of spatial button, grip, detent, guard and helmet cues. Zero mutes them without disabling haptics. Uses the aircraft audio mixer when available.");
        HapticStrength = Float(config, "Interaction Haptic Strength", 1f, 0f, 2f, "Strength of brief grab, release, button, lever and detent feedback. Zero disables these cues.");
        var comfortUpgrade = config.Bind(section, "Frame Comfort Defaults Applied", false, "Tracks the one-time shorter default throttle travel and neutral-wrist visual adjustment.");
        if (!comfortUpgrade.Value)
        {
            if (Mathf.Abs(ThrottleTravel.Value - .25f) < .0001f) ThrottleTravel.Value = .22f;
            if (GloveRotation.Value == Vector3.zero) GloveRotation.Value = new Vector3(25,0,0);
            comfortUpgrade.Value = true;
        }
        GrabRadius = Float(config, "Grab Radius", 0.12f, 0.03f, 0.3f, "Maximum hand distance from a handle when pressing grip.");
        StickTapGrip = config.Bind(section, "Stick Tap Grip", false, "Tap grip briefly to latch the stick; squeeze again to release. Holding longer than 0.3 seconds releases normally. Moving more than 40 cm from the handle, tracking loss or menus releases the latch.");
        ThrottleTapGrip = config.Bind(section, "Throttle Tap Grip", false, "Optional tap-to-latch throttle grip, with the same release behavior as Stick Tap Grip.");
        TwistYaw = config.Bind(section, "Stick Twist Yaw", true, "Twist the held or latched stick left/right for native rudder and nosewheel steering. The orientation when grabbed is neutral. Disable both yaw sources to keep pedals in control.");
        YawAngle = Float(config, "Stick Yaw Angle", 30f, 5f, 60f, "Degrees of stick twist for full rudder/nosewheel input. Uses the stick deadzone and response curve.");
        ThumbstickYaw = config.Bind(section, "Thumbstick Yaw", true, "Also allow the horizontal stick-hand thumbstick for yaw while holding the virtual stick. Adds to twist yaw and clamps to full input. Disable both yaw sources to keep pedals in control.");
    }

    private static ConfigEntry<float> Float(ConfigFile config, string name, float value, float min, float max, string description) =>
        config.Bind("Motion Controls", name, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
}
