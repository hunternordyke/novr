using BepInEx.Configuration;

namespace NOVR.MotionControls;

public sealed class ControllerBindings
{
    public readonly ConfigEntry<bool> PointerEnabled, CombatEnabled;
    public readonly ConfigEntry<ControllerButton> Fire, Countermeasures, NextWeapon, SelectTarget, NextCountermeasure, Map, WristMenu, ClearTargets, UnlockTarget, Brake;
    public readonly ConfigEntry<float> PointerResponse;
    public readonly ConfigEntry<bool> RightBumperZoom, SelectTargetOnStick;
    public readonly ConfigEntry<float> ViewZoomFactor;
    public ControllerBindings(ConfigFile config)
    {
        const string section = "VR Controller Bindings";
        PointerEnabled = config.Bind(section, "Controller Pointer", true, "Point at menus and the map with the stick hand; trigger clicks and thumbstick scrolls. Mouse remains available when controllers are untracked.");
        CombatEnabled = config.Bind(section, "Combat Controls", true, "Enable controller weapon and targeting actions while gripping virtual flight controls.");
        PointerResponse = config.Bind(section, "Pointer Response", 24f, new ConfigDescription("Laser aim smoothing. 0 disables smoothing. Higher follows faster.", new AcceptableValueRange<float>(0f, 60f)));
        RightBumperZoom = config.Bind(section, "Right Bumper View Zoom", true, "Hold the physical right bumper to show a magnified inspection window, with either flight grip held or released. Reserved for zoom even in left-handed mode. Requires a controller with a shoulder button.");
        ViewZoomFactor = config.Bind(section, "View Zoom Factor", 1.7f, new ConfigDescription("Magnification of the fixed-depth inspection window while right bumper is held. Headset projection is unchanged; release hides the window.", new AcceptableValueRange<float>(1.1f, 3f)));
        Fire = config.Bind(section, "Fire - stick hand", ControllerButton.Trigger, "Hold while gripping the virtual stick to fire.");
        Countermeasures = config.Bind(section, "Countermeasures - throttle hand", ControllerButton.Primary, "Throttle action button: Frame D-pad down or Touch X. Hold while gripping to deploy selected countermeasures.");
        NextWeapon = config.Bind(section, "Next Weapon - stick hand", ControllerButton.Primary, "Stick action button: A in the right-handed layout. Release while gripping to cycle.");
        // Keep the old key so existing custom button assignments survive the hand change.
        SelectTarget = config.Bind(section, "Select Target - throttle hand", ControllerButton.StickClick, "Target-select button. Uses the stick hand when Select Target On Stick Hand is enabled; otherwise the throttle hand. Press and release while gripping to select under the head-directed designator.");
        NextCountermeasure = config.Bind(section, "Next Countermeasure - throttle hand", ControllerButton.Secondary, "Frame D-pad right, or Y on Touch-compatible controllers.");
        Map = config.Bind(section, "Map - stick hand", ControllerButton.OutsideOrSecondary, "Frame B, or B on Touch-compatible controllers. Opens/closes the tactical map.");
        WristMenu = config.Bind(section, "Wrist Menu - throttle hand", ControllerButton.Menu, "Frame View, or the Touch left menu button. F9 is a keyboard fallback.");
        ClearTargets = config.Bind(section, "Clear Targets - stick hand", ControllerButton.Top, "Frame Y. Also available in the wrist panel on other controllers.");
        UnlockTarget = config.Bind(section, "Unlock Last Target - stick hand", ControllerButton.SecondaryOrNone, "Frame X while gripping the stick removes the most recently selected target. Wrist Flight actions provides the same action on controllers without a spare button.");
        var brakeDefault = ControllerButton.Trigger;
        // Do not give a new action a button already used by a custom setup.
        if (ThrottleBindingProfiles.SharesButton(brakeDefault, true, Countermeasures.Value, SelectTarget.Value, NextCountermeasure.Value, WristMenu.Value) ||
            ThrottleBindingProfiles.SharesButton(brakeDefault, false, Countermeasures.Value, SelectTarget.Value, NextCountermeasure.Value, WristMenu.Value))
            brakeDefault = ControllerButton.None;
        Brake = config.Bind(section, "Brake - throttle hand", brakeDefault,
            "Hold throttle trigger while gripping the throttle to use the game's wheel/air-brake input. Other layouts are available in the wrist Button bindings page.");
        // Only move the recognized stock VTOL layout automatically. Older throttle
        // presets, custom layouts and an explicitly saved hand choice are retained.
        var stockVtol = Fire.Value == ControllerButton.Trigger && NextWeapon.Value == ControllerButton.Primary &&
            ClearTargets.Value == ControllerButton.Top && UnlockTarget.Value == ControllerButton.SecondaryOrNone &&
            Map.Value == ControllerButton.OutsideOrSecondary && WristMenu.Value == ControllerButton.Menu &&
            Brake.Value == ControllerButton.Trigger && Countermeasures.Value == ControllerButton.Primary &&
            SelectTarget.Value == ControllerButton.StickClick && NextCountermeasure.Value == ControllerButton.Secondary;
        SelectTargetOnStick = config.Bind(section, "Select Target On Stick Hand", stockVtol,
            "VTOL-style target selection uses the stick hand (right by default), while gripping or latched. Disable to keep a custom throttle-hand selection binding.");
        var upgraded = config.Bind(section, "VTOL Style Upgrade Applied", false, "One-time upgrade of the previous stock button layout; custom layouts are preserved.");
        if (!upgraded.Value)
        {
            if (Fire.Value == ControllerButton.Trigger && ClearTargets.Value == ControllerButton.Top &&
                Map.Value == ControllerButton.OutsideOrSecondary && WristMenu.Value == ControllerButton.Menu &&
                UnlockTarget.Value == ControllerButton.SecondaryOrNone && NextWeapon.Value == ControllerButton.ShoulderOrPrimary &&
                ThrottleBindingProfiles.Identify(ThrottleButtons) == ThrottleBindingLayout.CountermeasureTrigger)
                ApplyVtolStyle();
            upgraded.Value = true;
        }
    }

    internal ThrottleButtons ThrottleButtons => new(Brake.Value, Countermeasures.Value,
        SelectTargetOnStick.Value ? ControllerButton.None : SelectTarget.Value, NextCountermeasure.Value);
    internal void ApplyVtolStyle()
    {
        Fire.Value = ControllerButton.Trigger; NextWeapon.Value = ControllerButton.Primary;
        UnlockTarget.Value = ControllerButton.SecondaryOrNone; ClearTargets.Value = ControllerButton.Top;
        RightBumperZoom.Value = true;
        ApplyThrottleLayout(ThrottleBindingLayout.VtolStyle);
    }

    internal float ReadFlight(ControllerSample hand, bool left, ControllerButton button) =>
        RightBumperZoom.Value && !left && ControllerButtonNames.Resolve(button, hand.NativeFrame) == ControllerButton.Shoulder
            ? 0 : Read(hand, button);
    internal void ApplyThrottleLayout(ThrottleBindingLayout layout)
    {
        var preset = ThrottleBindingProfiles.Get(layout);
        Brake.Value = preset.Brake; Countermeasures.Value = preset.Countermeasures;
        SelectTargetOnStick.Value = layout == ThrottleBindingLayout.VtolStyle;
        SelectTarget.Value = SelectTargetOnStick.Value ? ControllerButton.StickClick : preset.Target;
        NextCountermeasure.Value = preset.NextCountermeasure;
        ControllerActions.Instance?.ResetButtons();
    }

    internal static float Read(ControllerSample hand, ControllerButton button) => ControllerButtonNames.Resolve(button, hand.NativeFrame) switch
    {
        ControllerButton.Trigger => hand.Trigger,
        ControllerButton.Primary => hand.Primary ? 1 : 0,
        ControllerButton.Secondary => hand.Secondary ? 1 : 0,
        ControllerButton.Menu => hand.Menu ? 1 : 0,
        ControllerButton.Shoulder => hand.Shoulder ? 1 : 0,
        ControllerButton.Top => hand.Top ? 1 : 0,
        ControllerButton.Outside => hand.Outside ? 1 : 0,
        ControllerButton.StickClick => hand.StickClick ? 1 : 0,
        _ => 0
    };
}
