using System.ComponentModel;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR;

public class ModConfiguration
{
    public static ModConfiguration Instance;
    

    public readonly ConfigFile Config;
    public readonly MotionControls.MotionControlSettings MotionControls;
    public readonly ConfigEntry<float> TargetDesignatorOvershoot;
    public readonly ConfigEntry<bool> EnableNativeMenuUi;
    public readonly ConfigEntry<float> NativeMenuScale;
    public readonly ConfigEntry<float> NativeMenuDistance;
    public readonly ConfigEntry<float> NativeMenuHeightOffset;
    public readonly ConfigEntry<float> HmdFollowResponse;
    public readonly MotionControls.ControllerBindings ControllerBindings;
    public readonly ConfigEntry<float> HudPanelScale, HudPanelDistance;
    public readonly ConfigEntry<bool> HudContrast, HudDeclutter;

    public ModConfiguration(ConfigFile config)
    {
        Instance = this;

        Config = config;
        MotionControls = new MotionControls.MotionControlSettings(config);
        ControllerBindings = new MotionControls.ControllerBindings(config);
        HudPanelScale = config.Bind("VR HUD", "Status Panel Scale", 1.15f,
            new ConfigDescription("Size of cockpit status panels; aiming markers keep their angular alignment.", new AcceptableValueRange<float>(0.8f, 1.6f)));
        HudPanelDistance = config.Bind("VR HUD", "Status Panel Distance", 4f,
            new ConfigDescription("Status panel depth in meters. Angular size stays constant as depth changes.", new AcceptableValueRange<float>(2f, 10f)));
        HudContrast = config.Bind("VR HUD", "Status Panel Backplates", true, "Dark backplates behind status panels for readability over bright terrain.");
        HudDeclutter = config.Bind("VR HUD", "Declutter Status Panels", false, "Fade the secondary lower-left status panel. Weapon, countermeasure and aiming information remain visible.");
        HmdFollowResponse = config.Bind("VR HUD", "Head Follow Response", 10f,
            new ConfigDescription("Response of head-following status text. Higher values follow faster; 0 locks directly to the head. World target markers stay aligned independently.",
                new AcceptableValueRange<float>(0f, 30f)));
        TargetDesignatorOvershoot = config.Bind(
            "General",
            "Target Designator Overshoot",
            1.2f,
            "How much the target designator should multiply rotation to make for easier high off boresight target designation. Set to 1.0 to disable");

        EnableNativeMenuUi = config.Bind(
            "Experimental",
            "Enable Native Menu UI",
            true,
            "Use NOVR's native VR menu UI for non-flight menus. Disable to fall back to the existing patched game UI.");

        NativeMenuScale = config.Bind(
            "Experimental",
            "Native Menu Scale",
            1.25f,
            "Size multiplier for NOVR's native VR menu UI. Values from 0.75 to 2.0 are supported.");

        NativeMenuDistance = config.Bind(
            "Experimental",
            "Native Menu Distance",
            3.0f,
            "Distance in meters from the headset when NOVR's native VR menu UI is opened or recentered. Values from 1.5 to 6.0 are supported.");

        NativeMenuHeightOffset = config.Bind(
            "Experimental",
            "Native Menu Height Offset",
            0.0f,
            "Vertical offset in meters applied when NOVR's native VR menu UI is opened or recentered. Values from -0.25 to 1.0 are supported.");
    }
}
