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
    public readonly ConfigEntry<bool> ThumbstickYaw;
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
        ThrottleTravel = Float(config, "Throttle Travel", 0.25f, 0.05f, 0.6f, "Meters from minimum to maximum command: lift the collective in helicopters; slide the throttle forward in other aircraft.");
        GrabRadius = Float(config, "Grab Radius", 0.12f, 0.03f, 0.3f, "Maximum hand distance from a handle when pressing grip.");
        StickTapGrip = config.Bind(section, "Stick Tap Grip", false, "Tap grip briefly to latch the stick; squeeze again to release. Holding longer than 0.3 seconds releases normally. Moving more than 40 cm from the handle, tracking loss or menus releases the latch.");
        ThrottleTapGrip = config.Bind(section, "Throttle Tap Grip", false, "Optional tap-to-latch throttle grip, with the same release behavior as Stick Tap Grip.");
        ThumbstickYaw = config.Bind(section, "Thumbstick Yaw", true, "Use horizontal stick-hand thumbstick for yaw while holding the virtual stick. Disable to keep pedals in control.");
    }

    private static ConfigEntry<float> Float(ConfigFile config, string name, float value, float min, float max, string description) =>
        config.Bind("Motion Controls", name, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
}
