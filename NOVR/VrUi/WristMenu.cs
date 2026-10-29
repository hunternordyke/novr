using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NOVR.VrUi;

[DefaultExecutionOrder(-1100)]
internal sealed class WristMenu : MonoBehaviour
{
    // A dedicated bit leaves every game-owned cursor reason untouched on close.
    private const CursorFlags WristCursorFlag = (CursorFlags)(1 << 28);
    private static WristMenu? _instance;
    private readonly ControllerButtonGate _toggle = new();
    private GameObject? _panel;
    private Text? _status;
    private Font? _font;
    private GameObject? _setupPage, _flightPage, _ejectPage, _consolePage, _controlsPage, _helpPage;
    private readonly List<(Text label, Func<string> text)> _liveLabels = new();
    private Text? _controlsInfo, _bindingHelp;
    private float _nextHelpRefresh;
    private Aircraft? _ejectAircraft;
    private Transform? _buttonParent;
    private bool _anchored;
    internal static bool IsOpen => _instance?._panel?.activeSelf == true;
    internal static bool IsConsoleSetup => IsOpen && _instance?._consolePage?.activeSelf == true;
    private void Awake() => _instance = this;

    private void Update()
    {
        var config = ModConfiguration.Instance;
        var hand = TrackedControllers.Read(!config.MotionControls.LeftHanded.Value);
        var cockpit = CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.GetLocalAircraft(out _) &&
                      Time.timeScale > 0f && !RadialMenuMain.IsInUse() &&
                      (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);
        _toggle.Update(config.ControllerBindings.PointerEnabled.Value && hand.Valid && cockpit,
            ControllerBindings.Read(hand, config.ControllerBindings.WristMenu.Value));
        if (_toggle.Pressed || (cockpit && TrackedControllers.Available && Keyboard.current?.f9Key.wasPressedThisFrame == true))
        {
            if (IsOpen) Close(); else Open();
        }
        if (!IsOpen) return;
        foreach (var item in _liveLabels)
            if (item.label != null && item.label.gameObject.activeInHierarchy) item.label.text = item.text();
        if (_controlsInfo != null && _controlsPage?.activeSelf == true)
        {
            var motion = config.MotionControls;
            _controlsInfo.text = $"Stick travel {motion.StickAngle.Value:0}°  •  Deadzone {motion.Deadzone.Value * 100:0}%\nThrottle travel {motion.ThrottleTravel.Value * 100:0} cm\nTap mode: tap to latch, squeeze again to release.\nLong holds still release normally.\nRelease the handle before using the console.";
        }
        if (_bindingHelp != null && _helpPage?.activeSelf == true && Time.unscaledTime >= _nextHelpRefresh)
        { _bindingHelp.text = BindingsHelp(); _nextHelpRefresh = Time.unscaledTime + .25f; }
        if (_ejectPage?.activeSelf == true &&
            (!GameManager.GetLocalAircraft(out var confirmAircraft) || confirmAircraft != _ejectAircraft))
        {
            Close();
            return;
        }
        if (!cockpit || !hand.Valid || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu))
        {
            Close();
            return;
        }
        var camera = APIBus.CockpitHudCamera;
        if (camera == null) { Close(); return; }
        var trackingRoot = camera.transform.parent;
        var position = hand.Position + CockpitProfiles.SeatOffset + new Vector3(0, 0.19f, 0.16f);
        if (trackingRoot != null) position = trackingRoot.TransformPoint(position);
        // Freeze the panel during a press so the off-hand doesn't drag a button away.
        if (!_anchored || VrUiCursor.I?.PrimaryHeld != true)
        {
            var blend = _anchored ? FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, 14) : 1;
            _panel!.transform.position = Vector3.Lerp(_panel.transform.position, position, blend);
            var forward = _panel.transform.position - camera.transform.position;
            if (forward.sqrMagnitude > 0.01f)
                _panel.transform.rotation = Quaternion.Slerp(_panel.transform.rotation, Quaternion.LookRotation(forward, camera.transform.up), blend);
            _anchored = true;
        }
        if (_status != null)
            _status.text = (CockpitProfiles.Instance?.AircraftName ?? "Cockpit") + "\n" +
                $"Motion controls: {(config.MotionControls.Enabled.Value ? "ON" : "OFF")}  |  HUD {config.HudPanelScale.Value:0.00}x\n" +
                (MotionFlightController.Instance?.Status ?? "Waiting for cockpit");
    }

    private void Open()
    {
        if (DynamicMap.mapMaximized) SceneSingleton<DynamicMap>.i?.Minimize();
        EnsurePanel();
        _panel!.SetActive(true);
        _anchored = false;
        CursorManager.SetFlag(WristCursorFlag, true);
        MotionFlightController.Instance?.ResetGrips();
    }

    internal static void Close()
    {
        if (_instance?._ejectPage?.activeSelf == true) _instance.ShowFlightPage();
        if (_instance != null) _instance._ejectAircraft = null;
        if (_instance?._panel != null) _instance._panel.SetActive(false);
        CursorManager.SetFlag(WristCursorFlag, false);
        MotionFlightController.Instance?.ResetGrips();
    }

    private void ShowFlightPage()
    {
        _ejectAircraft = null;
        _ejectPage?.SetActive(false);
        _consolePage?.SetActive(false);
        _setupPage?.SetActive(false);
        _controlsPage?.SetActive(false); _helpPage?.SetActive(false);
        _flightPage?.SetActive(true);
    }

    private void AskToEject()
    {
        if (!GameManager.GetLocalAircraft(out _ejectAircraft)) return;
        _flightPage!.SetActive(false);
        _ejectPage!.SetActive(true);
    }

    private void ConfirmEject()
    {
        if (GameManager.GetLocalAircraft(out var aircraft) && aircraft == _ejectAircraft)
            ControllerActions.Instance?.QueueWristAction("Eject");
        Close();
    }

    private void EnsurePanel()
    {
        if (_panel != null) return;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _panel = new GameObject("NOVR Wrist Panel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _panel.transform.SetParent(transform, false);
        _panel.transform.localScale = Vector3.one * 0.0009f;
        var rect = (RectTransform)_panel.transform;
        rect.sizeDelta = new Vector2(540, 660);
        var canvas = _panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        var background = _panel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.055f, 1f);
        _status = Label(rect, "", new Vector2(0, 265), new Vector2(510, 105), 22);
        var config = ModConfiguration.Instance;
        _setupPage = new GameObject("Cockpit setup", typeof(RectTransform));
        _setupPage.transform.SetParent(rect, false);
        _buttonParent = _setupPage.transform;
        AddButton("Motion controls", 0, 0, () => config.MotionControls.Enabled.Value = !config.MotionControls.Enabled.Value);
        AddButton("Recenter view", 1, 0, () => { NOVRHeadsetData.CalibrateTranslation(); NOVRHeadsetData.CalibrateRotation(); Close(); });
        AddButton("Place handles (3s)", 0, 1, () => { config.MotionControls.Enabled.Value = true; MotionFlightController.Instance?.SchedulePlacement(); Close(); });
        AddButton("Reset aircraft setup", 1, 1, () => CockpitProfiles.Instance?.ResetProfile());
        AddButton("Seat up", 0, 2, () => CockpitProfiles.Instance?.MoveSeat(Vector3.up * 0.02f));
        AddButton("Seat down", 1, 2, () => CockpitProfiles.Instance?.MoveSeat(Vector3.down * 0.02f));
        AddButton("Seat left", 0, 3, () => CockpitProfiles.Instance?.MoveSeat(Vector3.left * 0.02f));
        AddButton("Seat right", 1, 3, () => CockpitProfiles.Instance?.MoveSeat(Vector3.right * 0.02f));
        AddButton("Seat forward", 0, 4, () => CockpitProfiles.Instance?.MoveSeat(Vector3.forward * 0.02f));
        AddButton("Seat back", 1, 4, () => CockpitProfiles.Instance?.MoveSeat(Vector3.back * 0.02f));
        AddButton("HUD smaller", 0, 5, () => config.HudPanelScale.Value -= 0.05f);
        AddButton("HUD larger", 1, 5, () => config.HudPanelScale.Value += 0.05f);
        AddButton("Toggle backplates", 0, 6, () => config.HudContrast.Value = !config.HudContrast.Value);
        AddButton("Toggle declutter", 1, 6, () => config.HudDeclutter.Value = !config.HudDeclutter.Value);
        AddButton("Flight controls", 0, 7, () => { _setupPage.SetActive(false); _controlsPage!.SetActive(true); });
        AddButton("Controls help", 1, 7, () => { _setupPage.SetActive(false); _helpPage!.SetActive(true); });
        _flightPage = new GameObject("Flight and display", typeof(RectTransform));
        _flightPage.transform.SetParent(rect, false);
        _buttonParent = _flightPage.transform;
        AddButton("Next weapon", 0, 0, () => ControllerActions.Instance?.QueueWristAction("Next Weapon"));
        AddButton("Next countermeasure", 1, 0, () => ControllerActions.Instance?.QueueWristAction("Next Countermeasure"));
        AddButton("Toggle gear", 0, 1, () => ControllerActions.Instance?.QueueWristAction("Gear"));
        AddButton("Toggle radar", 1, 1, () => ControllerActions.Instance?.QueueWristAction("Radar"));
        AddButton("Toggle engine", 0, 2, () => ControllerActions.Instance?.QueueWristAction("Toggle Engine"));
        AddButton("Toggle flight assist", 1, 2, () => ControllerActions.Instance?.QueueWristAction("Flight Assist"));
        AddButton("Clear targets", 0, 3, () => SceneSingleton<CombatHUD>.i?.DeselectAll());
        AddButton("Tactical map", 1, 3, ControllerActions.ToggleMap);
        AddButton("HUD nearer", 0, 4, () => config.HudPanelDistance.Value -= 0.5f);
        AddButton("HUD farther", 1, 4, () => config.HudPanelDistance.Value += 0.5f);
        AddButton("Reset HUD", 0, 5, () => { config.HudPanelScale.Value = 1.15f; config.HudPanelDistance.Value = 4f; config.HudContrast.Value = true; config.HudDeclutter.Value = false; });
        AddButton("Mirror handedness", 1, 5, () => { config.MotionControls.LeftHanded.Value = !config.MotionControls.LeftHanded.Value; Close(); });
        AddButton("Eject...", 0, 6, AskToEject);
        AddButton("Console setup", 1, 6, () => { _flightPage.SetActive(false); _consolePage!.SetActive(true); });
        Label(_flightPage.transform, "Actions close this panel.\nRelease grips before returning to flight.", new Vector2(0, -220), new Vector2(510, 56), 18);
        _flightPage.SetActive(false);
        _ejectPage = new GameObject("Confirm ejection", typeof(RectTransform));
        _ejectPage.transform.SetParent(rect, false);
        _buttonParent = _ejectPage.transform;
        Label(_ejectPage.transform, "Eject from this aircraft?\nThis starts the game's ejection sequence.", new Vector2(0, 65), new Vector2(510, 120), 24);
        AddButton("Cancel", 0, 4, ShowFlightPage);
        AddButton("Confirm eject", 1, 4, ConfirmEject);
        _ejectPage.SetActive(false);
        _consolePage = new GameObject("Universal console setup", typeof(RectTransform));
        _consolePage.transform.SetParent(rect, false);
        _buttonParent = _consolePage.transform;
        AddButton("Show / hide console", 0, 0, () => config.CockpitPanelEnabled.Value = !config.CockpitPanelEnabled.Value);
        AddButton("Reset console", 1, 0, () => { config.CockpitPanelPosition.Value = new Vector3(0, -.32f, .65f); config.CockpitPanelTilt.Value = 20; config.CockpitPanelScale.Value = 1; config.CockpitPanelEnabled.Value = true; });
        AddButton("Panel up", 0, 1, () => MoveConsole(Vector3.up * .02f));
        AddButton("Panel down", 1, 1, () => MoveConsole(Vector3.down * .02f));
        AddButton("Panel left", 0, 2, () => MoveConsole(Vector3.left * .02f));
        AddButton("Panel right", 1, 2, () => MoveConsole(Vector3.right * .02f));
        AddButton("Panel nearer", 0, 3, () => MoveConsole(Vector3.back * .02f));
        AddButton("Panel farther", 1, 3, () => MoveConsole(Vector3.forward * .02f));
        AddButton("Tilt up", 0, 4, () => config.CockpitPanelTilt.Value += 5);
        AddButton("Tilt down", 1, 4, () => config.CockpitPanelTilt.Value -= 5);
        AddButton("Panel smaller", 0, 5, () => config.CockpitPanelScale.Value -= .05f);
        AddButton("Panel larger", 1, 5, () => config.CockpitPanelScale.Value += .05f);
        Label(_consolePage.transform, "One layout and position for every aircraft.\nAdjustments preview on the console.\nF6 also shows/hides it.", new Vector2(0, -193), new Vector2(510, 105), 20);
        _consolePage.SetActive(false);
        _controlsPage = new GameObject("Motion control feel", typeof(RectTransform));
        _controlsPage.transform.SetParent(rect, false); _buttonParent = _controlsPage.transform;
        var motion = config.MotionControls;
        AddLiveButton(() => "Stick grip: " + (motion.StickTapGrip.Value ? "TAP" : "HOLD"), 0, 0,
            () => { motion.StickTapGrip.Value = !motion.StickTapGrip.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddLiveButton(() => "Throttle grip: " + (motion.ThrottleTapGrip.Value ? "TAP" : "HOLD"), 1, 0,
            () => { motion.ThrottleTapGrip.Value = !motion.ThrottleTapGrip.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddButton("Less sensitive", 0, 1, () => motion.StickAngle.Value += 5);
        AddButton("More sensitive", 1, 1, () => motion.StickAngle.Value -= 5);
        AddButton("Deadzone −", 0, 2, () => motion.Deadzone.Value -= .01f);
        AddButton("Deadzone +", 1, 2, () => motion.Deadzone.Value += .01f);
        AddButton("Shorter throttle", 0, 3, () => motion.ThrottleTravel.Value -= .025f);
        AddButton("Longer throttle", 1, 3, () => motion.ThrottleTravel.Value += .025f);
        AddLiveButton(() => "Thumbstick yaw: " + (motion.ThumbstickYaw.Value ? "ON" : "OFF"), 0, 4,
            () => motion.ThumbstickYaw.Value = !motion.ThumbstickYaw.Value);
        AddButton("Reset response", 1, 4, () => { motion.StickAngle.Value = 25; motion.Deadzone.Value = .06f; motion.ResponseExponent.Value = 1.5f; motion.ThrottleTravel.Value = .25f; });
        AddButton("Controls help", 0, 5, () => { _controlsPage.SetActive(false); _helpPage!.SetActive(true); });
        AddButton("Place handles (3s)", 1, 5, () => { motion.Enabled.Value = true; MotionFlightController.Instance?.SchedulePlacement(); Close(); });
        _controlsInfo = Label(_controlsPage.transform, "", new Vector2(0, -197), new Vector2(510, 105), 17);
        _controlsPage.SetActive(false);
        _helpPage = new GameObject("Controller help", typeof(RectTransform));
        _helpPage.transform.SetParent(rect, false);
        _bindingHelp = Label(_helpPage.transform, "", new Vector2(0, -8), new Vector2(510, 470), 20);
        _helpPage.SetActive(false);
        _buttonParent = rect;
        AddLiveButton(() => _setupPage.activeSelf ? "Flight actions" : _flightPage.activeSelf ? "Cockpit setup" :
            _ejectPage.activeSelf ? "Cancel eject" : _consolePage.activeSelf ? "Back to Flight" : "Back to Setup", 0, 8, () =>
        {
            if (_ejectPage.activeSelf || _consolePage.activeSelf) { ShowFlightPage(); return; }
            if (_controlsPage.activeSelf || _helpPage.activeSelf)
            {
                _controlsPage.SetActive(false); _helpPage.SetActive(false); _setupPage.SetActive(true); return;
            }
            var setup = _setupPage.activeSelf;
            _setupPage.SetActive(!setup); _flightPage.SetActive(setup);
        });
        AddButton("Close", 1, 8, Close);
        LayerHelper.SetLayerRecursive(_panel.transform, LayerHelper.GetVrUiLayer());
    }

    private static void MoveConsole(Vector3 delta)
    {
        var position = ModConfiguration.Instance.CockpitPanelPosition;
        var p = position.Value + delta;
        position.Value = new Vector3(Mathf.Clamp(p.x, -.5f, .5f), Mathf.Clamp(p.y, -.75f, .15f), Mathf.Clamp(p.z, .3f, 1.2f));
    }

    private string BindingsHelp()
    {
        var config = ModConfiguration.Instance;
        var stickLeft = config.MotionControls.LeftHanded.Value;
        var bindings = config.ControllerBindings;
        string Button(ControllerButton button, bool left) => ControllerButtonNames.Describe(button, left, TrackedControllers.NativeFrame(left));
        return $"{(stickLeft ? "LEFT" : "RIGHT")} HAND • STICK\n" +
            $"Fire: {Button(bindings.Fire.Value, stickLeft)}\n" +
            $"Next weapon: {Button(bindings.NextWeapon.Value, stickLeft)} (release)\n" +
            $"Clear targets: {Button(bindings.ClearTargets.Value, stickLeft)}\n\n" +
            $"{(stickLeft ? "RIGHT" : "LEFT")} HAND • THROTTLE\n" +
            $"Countermeasures: {Button(bindings.Countermeasures.Value, !stickLeft)}\n" +
            $"Select target: {Button(bindings.SelectTarget.Value, !stickLeft)} (release)\n" +
            $"CM type: {Button(bindings.NextCountermeasure.Value, !stickLeft)}\n\n" +
            $"Map: {(stickLeft ? "Left" : "Right")} {Button(bindings.Map.Value, stickLeft)}\n" +
            $"Wrist panel: {(stickLeft ? "Right" : "Left")} {Button(bindings.WristMenu.Value, !stickLeft)}\n\n" +
            "Combat buttons need a held or latched handle.\nFree hand: point + trigger for menus/console.\nRelease triggers and grips after menus.\nHold both thumbstick clicks to place handles.";
    }

    private void AddLiveButton(Func<string> text, int column, int row, Action action) =>
        _liveLabels.Add((AddButton(text(), column, row, action), text));

    private Text AddButton(string text, int column, int row, Action action)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_buttonParent, false);
        rect.sizeDelta = new Vector2(248, 49);
        rect.anchoredPosition = new Vector2(column == 0 ? -132 : 132, 177 - row * 57);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => action());
        NativeButtonFeedback.Configure(button, new Color(0.10f, 0.24f, 0.29f));
        return Label(rect, text, Vector2.zero, new Vector2(238, 45), 20);
    }

    private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.GetComponent<Text>();
        label.font = _font;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private void OnDisable() { Close(); _toggle.Reset(); }
    private void OnDestroy() { OnDisable(); if (_instance == this) _instance = null; }
}
