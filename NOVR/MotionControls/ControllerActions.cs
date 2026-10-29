using HarmonyLib;
using UnityEngine;
using NOVR.VrUi;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-1050)]
internal sealed class ControllerActions : MonoBehaviour
{
    internal static ControllerActions? Instance { get; private set; }
    private readonly ControllerButtonGate _fire = new(), _countermeasures = new(), _weapon = new(), _target = new(), _nextCountermeasure = new(), _map = new(), _clear = new();
    private string? _queuedAction;
    private int _queuedFrame;
    private Aircraft? _queuedAircraft;
    private void Awake() => Instance = this;

    internal void QueueWristAction(string action)
    {
        QueuePanelAction(action);
        WristMenu.Close();
    }

    internal void QueuePanelAction(string action)
    {
        if (action != "Gear" && action != "Radar" && action != "Toggle Engine" && action != "Flight Assist" &&
            action != "Next Weapon" && action != "Next Countermeasure" && action != "Eject") return;
        if (!GameManager.GetLocalAircraft(out var aircraft)) return;
        _queuedAction = action;
        _queuedFrame = Time.frameCount + 1;
        _queuedAircraft = aircraft;
    }

    internal bool Queued(string action) => _queuedAction == action && _queuedFrame == Time.frameCount &&
        TrackedControllers.Available && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
        !CursorManager.GetFlag(CursorFlags.GameMenu) && (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
        !DynamicMap.mapMaximized && !WristMenu.IsOpen &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
        CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.GetLocalAircraft(out var aircraft) && aircraft == _queuedAircraft;

    private void Update()
    {
        var config = ModConfiguration.Instance;
        var bindings = config.ControllerBindings;
        var stick = TrackedControllers.Read(config.MotionControls.LeftHanded.Value);
        var throttle = TrackedControllers.Read(!config.MotionControls.LeftHanded.Value);
        var controls = MotionFlightController.Instance;
        var flight = bindings.CombatEnabled.Value && controls != null && controls.FlightActive && !Cursor.visible && !WristMenu.IsOpen;
        var stickActive = flight && stick.Valid && controls!.StickHeld;
        var throttleActive = flight && throttle.Valid && controls!.ThrottleHeld;
        _fire.Update(stickActive, ControllerBindings.Read(stick, bindings.Fire.Value));
        _countermeasures.Update(throttleActive, ControllerBindings.Read(throttle, bindings.Countermeasures.Value));
        _weapon.Update(stickActive, ControllerBindings.Read(stick, bindings.NextWeapon.Value));
        _target.Update(throttleActive, ControllerBindings.Read(throttle, bindings.SelectTarget.Value));
        _nextCountermeasure.Update(throttleActive, ControllerBindings.Read(throttle, bindings.NextCountermeasure.Value));
        _clear.Update(stickActive, ControllerBindings.Read(stick, bindings.ClearTargets.Value));
        if (_clear.Released) SceneSingleton<CombatHUD>.i?.DeselectAll();

        // Map and wrist access also work before motion flight controls are enabled.
        var cockpit = Time.timeScale > 0 && CameraStateManager.cameraMode == CameraMode.cockpit &&
                      GameManager.GetLocalAircraft(out _) && !RadialMenuMain.IsInUse() &&
                      (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);
        _map.Update(bindings.PointerEnabled.Value && stick.Valid && cockpit && !WristMenu.IsOpen, ControllerBindings.Read(stick, bindings.Map.Value));
        if (_map.Pressed) ToggleMap();
    }

    internal static void ToggleMap()
    {
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null) return;
        WristMenu.Close();
        MotionFlightController.Instance?.ResetGrips();
        if (DynamicMap.mapMaximized) map.Minimize(); else map.Maximize();
    }

    internal bool Held(string action) => TrackedControllers.Available && !WristMenu.IsOpen && !Cursor.visible && action switch
    {
        "Fire" => _fire.Held,
        "Countermeasures" => _countermeasures.Held,
        _ => false
    };

    internal bool Released(string action) => TrackedControllers.Available && !WristMenu.IsOpen && !Cursor.visible && action switch
    {
        "Next Weapon" => _weapon.Released,
        "Select" => _target.Released,
        "Next Countermeasure" => _nextCountermeasure.Released,
        _ => false
    };

    private void OnDisable()
    {
        _queuedAction = null;
        foreach (var gate in new[] { _fire, _countermeasures, _weapon, _target, _nextCountermeasure, _map, _clear }) gate.Reset();
    }
    private void OnDestroy() { OnDisable(); if (Instance == this) Instance = null; }
}

// Feed the game's existing action processing so weapon safety, G-LOC, radar
// altitude checks and multiplayer commands continue to be applied by the game.
[HarmonyPatch(typeof(Rewired.Player), "GetButton", new[] { typeof(string) })]
internal static class ControllerHeldActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput) __result |= ControllerActions.Instance?.Held(__0) == true;
    }
}

[HarmonyPatch(typeof(Rewired.Player), "GetButtonTimedPressUp", new[] { typeof(string), typeof(float), typeof(float) })]
internal static class ControllerReleasedActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput)
            __result |= ControllerActions.Instance?.Released(__0) == true || ControllerActions.Instance?.Queued(__0) == true;
    }
}

[HarmonyPatch(typeof(Rewired.Player), "GetButtonDown", new[] { typeof(string) })]
internal static class ControllerPressedActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput && (__0 == "Gear" || __0 == "Eject"))
            __result |= ControllerActions.Instance?.Queued(__0) == true;
    }
}
