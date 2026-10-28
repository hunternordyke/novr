using System;
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
    private GameObject? _setupPage, _flightPage;
    private Transform? _buttonParent;
    private bool _anchored;
    internal static bool IsOpen => _instance?._panel?.activeSelf == true;
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
                "Seat and handle positions save automatically";
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
        if (_instance?._panel != null) _instance._panel.SetActive(false);
        CursorManager.SetFlag(WristCursorFlag, false);
        MotionFlightController.Instance?.ResetGrips();
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
        background.color = new Color(0.025f, 0.045f, 0.055f, 0.97f);
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
        AddButton("Tactical map", 0, 7, ControllerActions.ToggleMap);
        AddButton("Clear targets", 1, 7, () => SceneSingleton<CombatHUD>.i?.DeselectAll());
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
        Label(_flightPage.transform, "Flight actions close this panel.\nRelease grips, then grab again to fly.\nGear and radar obey the game's restrictions.", new Vector2(0, -224), new Vector2(510, 105), 20);
        _flightPage.SetActive(false);
        _buttonParent = rect;
        AddButton("Flight / Setup", 0, 8, () => { var setup = _setupPage.activeSelf; _setupPage.SetActive(!setup); _flightPage.SetActive(setup); });
        AddButton("Close", 1, 8, Close);
        LayerHelper.SetLayerRecursive(_panel.transform, LayerHelper.GetVrUiLayer());
    }

    private void AddButton(string text, int column, int row, Action action)
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
        Label(rect, text, Vector2.zero, new Vector2(238, 45), 20);
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
