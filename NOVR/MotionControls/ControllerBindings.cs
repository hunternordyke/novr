using BepInEx.Configuration;

namespace NOVR.MotionControls;

public sealed class ControllerBindings
{
    public readonly ConfigEntry<bool> PointerEnabled, CombatEnabled;
    public readonly ConfigEntry<ControllerButton> Fire, Countermeasures, NextWeapon, SelectTarget, NextCountermeasure, Map, WristMenu, ClearTargets;
    public readonly ConfigEntry<float> PointerResponse;
    public ControllerBindings(ConfigFile config)
    {
        const string section = "VR Controller Bindings";
        PointerEnabled = config.Bind(section, "Controller Pointer", true, "Point at menus and the map with the stick hand; trigger clicks and thumbstick scrolls. Mouse remains available when controllers are untracked.");
        CombatEnabled = config.Bind(section, "Combat Controls", true, "Enable controller weapon and targeting actions while gripping virtual flight controls.");
        PointerResponse = config.Bind(section, "Pointer Response", 24f, new ConfigDescription("Laser aim smoothing. 0 disables smoothing. Higher follows faster.", new AcceptableValueRange<float>(0f, 60f)));
        Fire = config.Bind(section, "Fire - stick hand", ControllerButton.Trigger, "Hold while gripping the virtual stick to fire.");
        Countermeasures = config.Bind(section, "Countermeasures - throttle hand", ControllerButton.Trigger, "Hold while gripping the throttle to deploy selected countermeasures.");
        NextWeapon = config.Bind(section, "Next Weapon - stick hand", ControllerButton.ShoulderOrPrimary, "Frame shoulder, or A on Touch-compatible controllers. Release to cycle.");
        SelectTarget = config.Bind(section, "Select Target - throttle hand", ControllerButton.ShoulderOrPrimary, "Frame shoulder, or X on Touch-compatible controllers. Selects under the existing head-directed target designator.");
        NextCountermeasure = config.Bind(section, "Next Countermeasure - throttle hand", ControllerButton.Secondary, "Frame D-pad right, or Y on Touch-compatible controllers.");
        Map = config.Bind(section, "Map - stick hand", ControllerButton.OutsideOrSecondary, "Frame B, or B on Touch-compatible controllers. Opens/closes the tactical map.");
        WristMenu = config.Bind(section, "Wrist Menu - throttle hand", ControllerButton.Menu, "Frame View, or the Touch left menu button. F9 is a keyboard fallback.");
        ClearTargets = config.Bind(section, "Clear Targets - stick hand", ControllerButton.Top, "Frame Y. Also available in the wrist panel on other controllers.");
    }

    internal static float Read(ControllerSample hand, ControllerButton button) => button switch
    {
        ControllerButton.Trigger => hand.Trigger,
        ControllerButton.Primary => hand.Primary ? 1 : 0,
        ControllerButton.Secondary => hand.Secondary ? 1 : 0,
        ControllerButton.Menu => hand.Menu ? 1 : 0,
        ControllerButton.Shoulder => hand.Shoulder ? 1 : 0,
        ControllerButton.Top => hand.Top ? 1 : 0,
        ControllerButton.Outside => hand.Outside ? 1 : 0,
        ControllerButton.StickClick => hand.StickClick ? 1 : 0,
        ControllerButton.ShoulderOrPrimary => (hand.NativeFrame ? hand.Shoulder : hand.Primary) ? 1 : 0,
        ControllerButton.OutsideOrSecondary => (hand.NativeFrame ? hand.Outside : hand.Secondary) ? 1 : 0,
        _ => 0
    };
}
