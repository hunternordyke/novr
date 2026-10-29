using System;
using System.Collections.Generic;
using System.Linq;
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
    private GameObject? _setupPage, _flightPage, _ejectPage, _consolePage, _controlsPage, _helpPage, _bindingsPage, _handsPage, _resetPage, _homePage, _seatPage, _comfortPage, _alignmentPage, _displayPage, _feedbackPage;
    private readonly Dictionary<GameObject, string> _pageTitles = new();
    private MenuHistory<GameObject>? _history;
    private Button? _backButton;
    private readonly List<(Text label, Func<string> text)> _liveLabels = new();
    private Text? _controlsInfo, _bindingHelp, _throttleHelp, _helpFooter, _bindingsInfo;
    private float _nextHelpRefresh;
    private Aircraft? _ejectAircraft, _openAircraft, _resetAircraft;
    private bool _resetLeftHanded, _followHand;
    private Vector3 _menuPosition;
    private Quaternion _menuRotation;
    private Button? _hoverButton, _assistButton;
    private Transform? _buttonParent;
    private bool _anchored;
    private string _pageTitle = "VIEW";
    internal static bool IsOpen => _instance?._panel?.activeSelf == true;
    internal static bool IsConsoleSetup => IsOpen && _instance?._consolePage?.activeSelf == true;
    private static bool CollectiveControls => GameManager.GetLocalAircraft(out var aircraft) && MotionFlightController.UsesLiftCollective(aircraft);
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
        if (_backButton != null) _backButton.interactable = _history?.CanBack == true;
        foreach (var item in _liveLabels)
            if (item.label != null && item.label.gameObject.activeInHierarchy)
            { var text = item.text(); if (item.label.text != text) item.label.text = text; }
        if (_hoverButton != null && _flightPage?.activeSelf == true)
            _hoverButton.interactable = GameManager.GetLocalAircraft(out var hoverAircraft) &&
                HoverAssist.UnavailableReason(hoverAircraft) == null;
        if (_assistButton != null && _flightPage?.activeSelf == true)
            _assistButton.interactable = GameManager.GetLocalAircraft(out var assistAircraft) &&
                assistAircraft.GetControlsFilter()?.HasFlightAssist() == true;
        if (_controlsInfo != null && _controlsPage?.activeSelf == true)
        {
            var motion = config.MotionControls;
            var travelLabel = CollectiveControls ? "Collective lift" : "Throttle travel";
            var direction = CollectiveControls ? "UP increases" : "FORWARD increases";
            _controlsInfo.text = $"Stick {motion.StickAngle.Value:0}° • Twist {motion.YawAngle.Value:0}° • Deadzone {motion.Deadzone.Value * 100:0}%\n{travelLabel} {motion.ThrottleTravel.Value * 100:0.#} cm • {direction}\nPedals: turn both yaw options OFF.";
        }
        if (Time.unscaledTime >= _nextHelpRefresh)
        {
            if (_bindingHelp != null && _helpPage?.activeSelf == true)
            {
                _bindingHelp.text = HandHelp(true); _throttleHelp!.text = HandHelp(false);
                var stickLeft = config.MotionControls.LeftHanded.Value;
                var b = config.ControllerBindings;
                _helpFooter!.text = "Combat buttons need a held or latched handle.\n" +
                    "Map: " + ControllerButtonNames.Describe(b.Map.Value, stickLeft, TrackedControllers.NativeFrame(stickLeft)) +
                    " · View: " + ControllerButtonNames.Describe(b.WristMenu.Value, !stickLeft, TrackedControllers.NativeFrame(!stickLeft)) +
                    "\n" + (b.RightBumperZoom.Value ? "Right bumper: hold magnifier, with either grip released or held." : "Right bumper magnifier is OFF.");
            }
            if (_bindingsInfo != null && _bindingsPage?.activeSelf == true) _bindingsInfo.text = ThrottleBindingsHelp();
            _nextHelpRefresh = Time.unscaledTime + .25f;
        }
        if (_ejectPage?.activeSelf == true &&
            (!GameManager.GetLocalAircraft(out var confirmAircraft) || confirmAircraft != _ejectAircraft))
        {
            Close();
            return;
        }
        if (!cockpit || !GameManager.GetLocalAircraft(out var currentAircraft) || currentAircraft != _openAircraft || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu))
        {
            Close();
            return;
        }
        var camera = APIBus.CockpitHudCamera;
        if (camera == null) { Close(); return; }
        var trackingRoot = camera.transform.parent;
        var followHand = config.MotionControls.ViewMenuFollowsHand.Value;
        if (_followHand != followHand) { _followHand = followHand; _anchored = false; }
        if (!_anchored || followHand && hand.Valid && VrUiCursor.I?.PrimaryHeld != true)
        {
            Vector3 position; Quaternion rotation;
            if (followHand && hand.Valid)
            {
                position = hand.Position + new Vector3(0, .19f, .16f);
                if (trackingRoot != null) position = trackingRoot.TransformPoint(position);
                var forward = position - camera.transform.position;
                rotation = forward.sqrMagnitude > .01f ? Quaternion.LookRotation(forward, camera.transform.up) : camera.transform.rotation;
            }
            else
            {
                var up = trackingRoot != null ? trackingRoot.up : Vector3.up;
                var forward = Vector3.ProjectOnPlane(camera.transform.forward, up);
                if (forward.sqrMagnitude < .01f) forward = trackingRoot != null ? trackingRoot.forward : Vector3.forward;
                forward.Normalize();
                position = camera.transform.position + forward * .72f - up * .03f;
                rotation = Quaternion.LookRotation(forward, up);
            }
            var blend = _anchored ? FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, 14) : 1;
            position = Vector3.Lerp(_panel!.transform.position, position, blend);
            rotation = Quaternion.Slerp(_panel.transform.rotation, rotation, blend);
            _menuPosition = trackingRoot != null ? trackingRoot.InverseTransformPoint(position) : position;
            _menuRotation = trackingRoot != null ? Quaternion.Inverse(trackingRoot.rotation) * rotation : rotation;
            _anchored = true;
        }
        _panel!.transform.SetPositionAndRotation(trackingRoot != null ? trackingRoot.TransformPoint(_menuPosition) : _menuPosition,
            trackingRoot != null ? trackingRoot.rotation * _menuRotation : _menuRotation);
        if (_status != null)
        {
            var status = _pageTitle + "\n" + (CockpitProfiles.Instance?.AircraftName ?? "Cockpit") +
                " · Motion " + (config.MotionControls.Enabled.Value ? "ON" : "OFF") + "\n" +
                (CockpitLayout.Active ? "Layout preview · Save or cancel when finished" : _consolePage?.activeSelf == true ? "Outline preview · Close View to see displays" : "Either hand: point + trigger · View closes this menu");
            if (_status.text != status) _status.text = status;
        }
    }

    private void Open()
    {
        if (DynamicMap.mapMaximized) SceneSingleton<DynamicMap>.i?.Minimize();
        if (!GameManager.GetLocalAircraft(out _openAircraft)) return;
        EnsurePanel();
        Home();
        _panel!.SetActive(true);
        _anchored = false;
        CursorManager.SetFlag(WristCursorFlag, true);
        MotionFlightController.Instance?.ResetGrips();
    }

    internal static void Close()
    {
        if (_instance?._resetPage?.activeSelf == true) _instance.ShowSetupPage();
        if (_instance?._ejectPage?.activeSelf == true) _instance.ShowFlightPage();
        if (_instance != null) _instance._ejectAircraft = null;
        if (_instance?._panel != null) _instance._panel.SetActive(false);
        CursorManager.SetFlag(WristCursorFlag, false);
        MotionFlightController.Instance?.ResetGrips();
    }

    private void ShowPage(GameObject page)
    {
        _history!.Push(page); RefreshPage();
    }
    private void Back() { _history!.Back(); RefreshPage(); }
    private void Home() { _history!.Home(); RefreshPage(); }
    private void RefreshPage()
    {
        foreach (var page in _pageTitles.Keys) page.SetActive(page == _history!.Current);
        if (_history!.Current != _ejectPage) _ejectAircraft = null;
        if (_history.Current != _resetPage) _resetAircraft = null;
        _pageTitle = string.Join(" / ", _history.Trail.Select(page => _pageTitles[page]));
        _nextHelpRefresh = 0;
    }
    private void ShowFlightPage() { Home(); ShowPage(_flightPage!); }
    private void ShowSetupPage() { Home(); ShowPage(_setupPage!); }
    private void AskReset()
    {
        CockpitLayout.Instance?.Finish(false);
        if (!GameManager.GetLocalAircraft(out _resetAircraft)) return;
        _resetLeftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        ShowPage(_resetPage!);
    }
    private void ConfirmReset()
    {
        if (GameManager.GetLocalAircraft(out var aircraft) && aircraft == _resetAircraft &&
            _resetLeftHanded == ModConfiguration.Instance.MotionControls.LeftHanded.Value)
            CockpitProfiles.Instance?.ResetProfile();
        Back();
    }
    private void AskToEject()
    {
        if (!GameManager.GetLocalAircraft(out _ejectAircraft)) return;
        ShowPage(_ejectPage!);
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
        _status = Label(rect, "", new Vector2(0, 265), new Vector2(510, 105), 19);
        var config = ModConfiguration.Instance;
        var motion = config.MotionControls;
        _homePage = Page("VIEW");
        AddButton("Flight actions", 0, 0, () => ShowPage(_flightPage!));
        AddButton("Cockpit setup", 1, 0, () => ShowPage(_setupPage!));
        AddButton("Flight controls", 0, 1, () => ShowPage(_controlsPage!));
        AddButton("Comfort & display", 1, 1, () => ShowPage(_comfortPage!));
        AddButton("Controller guide", 0, 2, () => ShowPage(_helpPage!));
        AddButton("Recenter view", 1, 2, () => { NOVRHeadsetData.CalibrateTranslation(); NOVRHeadsetData.CalibrateRotation(); Close(); });
        AddLiveButton(() => CockpitLayout.Active ? "Save layout" : "Edit cockpit layout", 0, 4, ToggleLayout);
        AddLiveButton(() => CockpitLayout.Active ? "Cancel layout" : "Motion: " + (motion.Enabled.Value ? "ON" : "OFF"), 1, 4, () =>
        {
            if (CockpitLayout.Active) { CockpitLayout.Instance?.Finish(false); Close(); }
            else motion.Enabled.Value = !motion.Enabled.Value;
        });
        Label(_homePage.transform, "FLY · weapons, gear, radar and hover\nSET UP · seat, panel and handle placement\nTUNE · response, hands, HUD and feedback", new Vector2(0,-184), new Vector2(510,108), 19);

        _setupPage = Page("COCKPIT");
        AddButton("Seat position", 0, 0, () => ShowPage(_seatPage!));
        AddButton("Console position", 1, 0, () => ShowPage(_consolePage!));
        AddLiveButton(() => CockpitLayout.Active ? "Save layout" : "Edit cockpit layout", 0, 1, ToggleLayout);
        AddLiveButton(() => CockpitLayout.Active ? "Cancel layout" : "Place handles (3s)", 1, 1, () =>
        {
            if (CockpitLayout.Active) CockpitLayout.Instance?.Finish(false);
            else { motion.Enabled.Value = true; MotionFlightController.Instance?.SchedulePlacement(); }
            Close();
        });
        AddLiveButton(() => "Stick hand: " + (motion.LeftHanded.Value ? "LEFT" : "RIGHT"), 0, 2,
            () => { CockpitLayout.Instance?.Finish(false); motion.LeftHanded.Value = !motion.LeftHanded.Value; Close(); });
        AddButton("Reset seat & handles…", 1, 2, AskReset);
        Label(_setupPage.transform, "Edit layout: grip each item to move it.\nReopen View to save or cancel.\n\nSeat movement leaves handles and panel in place.\nPlacements are saved for this aircraft.", new Vector2(0,-115), new Vector2(510,220), 20);

        _seatPage = Page("SEAT");
        AddButton("Seat up", 0, 0, () => CockpitProfiles.Instance?.MoveSeat(Vector3.up * .02f));
        AddButton("Seat down", 1, 0, () => CockpitProfiles.Instance?.MoveSeat(Vector3.down * .02f));
        AddButton("Seat left", 0, 1, () => CockpitProfiles.Instance?.MoveSeat(Vector3.left * .02f));
        AddButton("Seat right", 1, 1, () => CockpitProfiles.Instance?.MoveSeat(Vector3.right * .02f));
        AddButton("Seat forward", 0, 2, () => CockpitProfiles.Instance?.MoveSeat(Vector3.forward * .02f));
        AddButton("Seat back", 1, 2, () => CockpitProfiles.Instance?.MoveSeat(Vector3.back * .02f));
        Label(_seatPage.transform, "Each step moves the viewpoint 2 cm.\nHandles and console keep their cockpit positions.", new Vector2(0,-92), new Vector2(510,140), 20);

        _resetPage = Page("RESET SETUP");
        Label(_resetPage.transform, "Restore the default seat, stick and throttle\nfor this aircraft and handedness?\n\nYour saved handle positions will be replaced.\nThe control panel keeps its current placement.", new Vector2(0,45), new Vector2(510,180), 21);
        AddButton("Keep my setup", 0, 4, Back);
        AddButton("Reset seat & handles", 1, 4, ConfirmReset);

        _flightPage = Page("FLIGHT");
        AddButton("Next weapon", 0, 0, () => ControllerActions.Instance?.QueueWristAction("Next Weapon"));
        AddButton("Next countermeasure", 1, 0, () => ControllerActions.Instance?.QueueWristAction("Next Countermeasure"));
        AddButton("Unlock last target", 0, 1, () => { SceneSingleton<CombatHUD>.i?.DeselectLast(); Close(); });
        AddButton("Clear targets", 1, 1, () => { SceneSingleton<CombatHUD>.i?.DeselectAll(); Close(); });
        AddButton("Toggle gear", 0, 2, () => ControllerActions.Instance?.QueueWristAction("Gear"));
        AddButton("Toggle radar", 1, 2, () => ControllerActions.Instance?.QueueWristAction("Radar"));
        AddButton("Toggle engine", 0, 3, () => ControllerActions.Instance?.QueueWristAction("Toggle Engine"));
        AddButton("Tactical map", 1, 3, ControllerActions.ToggleMap);
        var assistLabel = AddButton("Flight assist", 0, 4, () => ControllerActions.Instance?.QueueWristAction("Flight Assist"));
        _assistButton = assistLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((assistLabel, () => GameManager.GetLocalAircraft(out var a) && a.GetControlsFilter()?.HasFlightAssist() == true ? "Toggle flight assist" : "Assist: not switchable"));
        var hoverLabel = AddButton("Hover assist", 1, 4, () => ControllerActions.Instance?.QueueWristAction("Auto Hover"));
        _hoverButton = hoverLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((hoverLabel, () => !GameManager.GetLocalAircraft(out var a) || a.GetControlsFilter()?.HasAutoHover() != true
            ? "Hover: not fitted" : a.radarAlt < 1f ? "Hover: ground locked" : $"Hover assist: {(a.IsAutoHoverEnabled() ? "ON" : "OFF")}"));
        AddButton("Eject…", 0, 6, AskToEject);
        _ejectPage = Page("EJECT");
        Label(_ejectPage.transform, "Eject from this aircraft?\nThis starts the game's ejection sequence.", new Vector2(0,65), new Vector2(510,120), 24);
        AddButton("Cancel", 0, 4, Back);
        AddButton("Confirm eject", 1, 4, ConfirmEject);

        _consolePage = Page("CONSOLE");
        AddLiveButton(() => "Console: " + (config.CockpitPanelEnabled.Value ? "ON" : "OFF"), 0, 0, () => { CockpitLayout.Instance?.Finish(false); config.CockpitPanelEnabled.Value = !config.CockpitPanelEnabled.Value; });
        AddButton("Anchor to dashboard", 1, 0, () => { CockpitPanelPlacement.Instance?.ResetToDashboard(); config.CockpitPanelEnabled.Value = true; });
        AddButton("Panel up", 0, 1, () => MoveConsole(Vector3.up * .02f));
        AddButton("Panel down", 1, 1, () => MoveConsole(Vector3.down * .02f));
        AddButton("Panel left", 0, 2, () => MoveConsole(Vector3.left * .02f));
        AddButton("Panel right", 1, 2, () => MoveConsole(Vector3.right * .02f));
        AddButton("Panel nearer", 0, 3, () => MoveConsole(Vector3.back * .02f));
        AddButton("Panel farther", 1, 3, () => MoveConsole(Vector3.forward * .02f));
        AddButton("Tilt up", 0, 4, () => CockpitPanelPlacement.Adjust(Vector3.zero, tilt: 5));
        AddButton("Tilt down", 1, 4, () => CockpitPanelPlacement.Adjust(Vector3.zero, tilt: -5));
        AddButton("Panel smaller", 0, 5, () => CockpitPanelPlacement.Adjust(Vector3.zero, resize: -.05f));
        AddButton("Panel larger", 1, 5, () => CockpitPanelPlacement.Adjust(Vector3.zero, resize: .05f));
        AddLiveButton(() => CockpitLayout.Active ? "Save layout" : "Grab to position", 0, 6, ToggleLayout);
        var placementInfo = Label(_consolePage.transform, "", new Vector2(0, -223), new Vector2(510, 65), 16);
        _liveLabels.Add((placementInfo, () => CockpitPanelPlacement.Status + "\nPanel placement is saved per aircraft.\nSeat movement leaves panel and handles in place."));

        _controlsPage = Page("CONTROLS");
        AddLiveButton(() => "Stick grip: " + (motion.StickTapGrip.Value ? "TAP" : "HOLD"), 0, 0,
            () => { motion.StickTapGrip.Value = !motion.StickTapGrip.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddLiveButton(() => (CollectiveControls ? "Collective: " : "Throttle grip: ") + (motion.ThrottleTapGrip.Value ? "TAP" : "HOLD"), 1, 0,
            () => { motion.ThrottleTapGrip.Value = !motion.ThrottleTapGrip.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddButton("Less sensitive", 0, 1, () => motion.StickAngle.Value += 5);
        AddButton("More sensitive", 1, 1, () => motion.StickAngle.Value -= 5);
        AddButton("Deadzone −", 0, 2, () => motion.Deadzone.Value -= .01f);
        AddButton("Deadzone +", 1, 2, () => motion.Deadzone.Value += .01f);
        AddLiveButton(() => CollectiveControls ? "Shorter lift (1 cm)" : "Shorter travel (1 cm)", 0, 3, () => motion.ThrottleTravel.Value -= .01f);
        AddLiveButton(() => CollectiveControls ? "Longer lift (1 cm)" : "Longer travel (1 cm)", 1, 3, () => motion.ThrottleTravel.Value += .01f);
        AddLiveButton(() => "Twist yaw: " + (motion.TwistYaw.Value ? "ON" : "OFF"), 0, 4,
            () => { motion.TwistYaw.Value = !motion.TwistYaw.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddLiveButton(() => "Thumbstick yaw: " + (motion.ThumbstickYaw.Value ? "ON" : "OFF"), 1, 4, () => motion.ThumbstickYaw.Value = !motion.ThumbstickYaw.Value);
        AddLiveButton(() => "MIL detent: " + (motion.MilDetent.Value ? "ON" : "OFF"), 0, 5, () => motion.MilDetent.Value = !motion.MilDetent.Value);
        AddButton("Reset response", 1, 5, () => { motion.StickAngle.Value = 25; motion.YawAngle.Value = 30; motion.Deadzone.Value = .06f; motion.ResponseExponent.Value = 1.5f; motion.ThrottleTravel.Value = .12f; });
        AddButton("Button bindings", 0, 6, () => ShowPage(_bindingsPage!));
        AddButton("Controller guide", 1, 6, () => ShowPage(_helpPage!));
        _controlsInfo = Label(_controlsPage.transform, "", new Vector2(0,-225), new Vector2(510,65), 16);
        _bindingsPage = Page("BINDINGS");
        AddButton("Trigger: CMs", 0, 0, () => { config.ControllerBindings.ApplyThrottleLayout(ThrottleBindingLayout.CountermeasureTrigger); _nextHelpRefresh = 0; });
        AddButton("Trigger: brakes", 1, 0, () => { config.ControllerBindings.ApplyThrottleLayout(ThrottleBindingLayout.BrakeTrigger); _nextHelpRefresh = 0; });
        AddButton("VTOL-style controls", 0, 1, () => { config.ControllerBindings.ApplyVtolStyle(); _nextHelpRefresh = 0; });
        AddButton("Controller guide", 1, 1, () => ShowPage(_helpPage!));
        _bindingsInfo = Label(_bindingsPage.transform, "", new Vector2(0,-61), new Vector2(510,310), 17);
        _helpPage = Page("GUIDE");
        _bindingHelp = Label(_helpPage.transform, "", new Vector2(-132, 12), new Vector2(248, 390), 18);
        _throttleHelp = Label(_helpPage.transform, "", new Vector2(132, 12), new Vector2(248, 390), 18);
        _helpFooter = Label(_helpPage.transform, "", new Vector2(0, -217), new Vector2(510, 77), 16);

        _comfortPage = Page("COMFORT");
        AddButton("Hands & arms", 0, 0, () => ShowPage(_handsPage!));
        AddButton("HUD & display", 1, 0, () => ShowPage(_displayPage!));
        AddButton("Sound & haptics", 0, 1, () => ShowPage(_feedbackPage!));
        AddLiveButton(() => "Brow NV: " + (motion.HelmetNightVision.Value ? "ON" : "OFF"), 1, 1, () => motion.HelmetNightVision.Value = !motion.HelmetNightVision.Value);
        AddLiveButton(() => "Menu: " + (motion.ViewMenuFollowsHand.Value ? "FOLLOW HAND" : "STEADY"), 0, 3, () => motion.ViewMenuFollowsHand.Value = !motion.ViewMenuFollowsHand.Value);
        AddButton("Bring menu forward", 1, 3, () => _anchored = false);
        Label(_comfortPage.transform, "Either controller can point and click.\n\nBrow NV: raise a free hand to your forehead,\nthen press its trigger to toggle night vision.", new Vector2(0,-149), new Vector2(510,160), 20);

        _handsPage = Page("HANDS");
        AddLiveButton(() => "Gloves: " + (motion.RiggedGloves.Value ? "ON" : "OFF"), 0, 0, () => motion.RiggedGloves.Value = !motion.RiggedGloves.Value);
        AddLiveButton(() => "Sleeves: " + (motion.PilotArms.Value ? "ON" : "OFF"), 1, 0, () => motion.PilotArms.Value = !motion.PilotArms.Value);
        AddButton("Slimmer sleeves", 0, 1, () => motion.SleeveThickness.Value -= .05f);
        AddButton("Wider sleeves", 1, 1, () => motion.SleeveThickness.Value += .05f);
        AddButton("Hand alignment", 0, 2, () => ShowPage(_alignmentPage!));
        var handInfo = Label(_handsPage.transform, "", new Vector2(0,-97), new Vector2(510,235), 20);
        _liveLabels.Add((handInfo, () => $"Sleeve width: {motion.SleeveThickness.Value*100:0}%\n\nFree hands follow your controllers.\nHeld hands wrap around and follow the handle.\nRelease the grip to move the hand freely.\n\nSleeves hide beyond their natural reach."));

        _alignmentPage = Page("ALIGNMENT");
        AddButton("Palms upright", 0, 0, () => motion.GloveRotation.Value = new Vector3(0,0,90));
        AddButton("Palms down", 1, 0, () => motion.GloveRotation.Value = Vector3.zero);
        AddButton("Wrist tips down", 0, 1, () => motion.GloveRotation.Value += Vector3.right * 5);
        AddButton("Wrist tips up", 1, 1, () => motion.GloveRotation.Value -= Vector3.right * 5);
        AddButton("Palm roll −", 0, 2, () => motion.GloveRotation.Value -= Vector3.forward * 5);
        AddButton("Palm roll +", 1, 2, () => motion.GloveRotation.Value += Vector3.forward * 5);
        AddButton("Hands nearer", 0, 3, () => motion.GloveOffset.Value += Vector3.back * .005f);
        AddButton("Hands farther", 1, 3, () => motion.GloveOffset.Value += Vector3.forward * .005f);
        AddButton("Hands lower", 0, 4, () => motion.GloveOffset.Value += Vector3.down * .005f);
        AddButton("Hands higher", 1, 4, () => motion.GloveOffset.Value += Vector3.up * .005f);
        AddButton("Reset hand alignment", 0, 5, () => { motion.GloveOffset.Value = new Vector3(0,-.015f,-.055f); motion.GloveRotation.Value = new Vector3(0,0,90); });
        var alignmentInfo = Label(_alignmentPage.transform, "", new Vector2(0,-190), new Vector2(510,125), 18);
        _liveLabels.Add((alignmentInfo, () => $"Tilt {motion.GloveRotation.Value.x:0}° · Palm roll {motion.GloveRotation.Value.z:0}°\nFree-hand visuals only; hold a handle to attach.\nClose View to inspect alignment.\nPointer and flight inputs keep their real positions."));

        _displayPage = Page("DISPLAY");
        AddButton("Flight HUD smaller", 0, 0, () => { config.FocusedFlightHud.Value = true; config.FlightHudScale.Value -= .04f; });
        AddButton("Flight HUD larger", 1, 0, () => { config.FocusedFlightHud.Value = true; config.FlightHudScale.Value += .04f; });
        AddButton("HUD nearer", 0, 1, () => config.HudPanelDistance.Value -= .5f);
        AddButton("HUD farther", 1, 1, () => config.HudPanelDistance.Value += .5f);
        AddLiveButton(() => "Backplates: " + (config.HudContrast.Value ? "ON" : "OFF"), 0, 2, () => config.HudContrast.Value = !config.HudContrast.Value);
        AddLiveButton(() => "Declutter: " + (config.HudDeclutter.Value ? "ON" : "OFF"), 1, 2, () => config.HudDeclutter.Value = !config.HudDeclutter.Value);
        AddButton("Reset HUD", 0, 4, () => { config.FocusedFlightHud.Value = true; config.FlightHudScale.Value = .38f; config.HudPanelScale.Value = 1.15f; config.HudPanelDistance.Value = 4; config.HudContrast.Value = true; config.HudDeclutter.Value = false; });
        var displayInfo = Label(_displayPage.transform, "", new Vector2(0,-180), new Vector2(510,125), 19);
        _liveLabels.Add((displayInfo, () => $"Flight HUD size {config.FlightHudScale.Value*100:0}%\nStatus-panel depth {config.HudPanelDistance.Value:0.0} m\nDepth changes preserve apparent size."));

        _feedbackPage = Page("FEEDBACK");
        AddButton("Quieter sounds", 0, 0, () => motion.InteractionVolume.Value -= .1f);
        AddButton("Louder sounds", 1, 0, () => motion.InteractionVolume.Value += .1f);
        AddButton("Weaker haptics", 0, 1, () => motion.HapticStrength.Value -= .1f);
        AddButton("Stronger haptics", 1, 1, () => motion.HapticStrength.Value += .1f);
        var feedbackInfo = Label(_feedbackPage.transform, "", new Vector2(0,-55), new Vector2(510,220), 21);
        _liveLabels.Add((feedbackInfo, () => $"Sounds {motion.InteractionVolume.Value*100:0}%\nHaptics {motion.HapticStrength.Value*100:0}%\n\nZero disables that feedback.\nButtons, grabs, releases and detents\nshare these settings."));

        _history = new MenuHistory<GameObject>(_homePage);
        _buttonParent = rect;
        _backButton = AddButton("Back", 0, 8, Back, 3).transform.parent.GetComponent<Button>();
        AddButton("Home", 1, 8, Home, 3);
        AddButton("Close", 2, 8, Close, 3);
        Home();
        LayerHelper.SetLayerRecursive(_panel.transform, LayerHelper.GetVrUiLayer());
    }

    private static void ToggleLayout()
    {
        if (CockpitLayout.Active) { CockpitLayout.Instance?.Finish(true); Close(); }
        else CockpitLayout.Instance?.Begin();
    }

    private static void MoveConsole(Vector3 delta)
    {
        CockpitPanelPlacement.Adjust(delta);
    }

    private GameObject Page(string title)
    {
        var page = new GameObject(title, typeof(RectTransform));
        page.transform.SetParent(_panel!.transform, false);
        page.SetActive(false); _pageTitles.Add(page, title); _buttonParent = page.transform;
        return page;
    }

    private string ThrottleBindingsHelp()
    {
        var config = ModConfiguration.Instance;
        var left = !config.MotionControls.LeftHanded.Value;
        var frame = TrackedControllers.NativeFrame(left);
        var buttons = config.ControllerBindings.ThrottleButtons;
        var layout = ThrottleBindingProfiles.Identify(buttons);
        var wristConflict = ThrottleBindingProfiles.SharesButton(config.ControllerBindings.WristMenu.Value, frame,
            buttons.Brake, buttons.Countermeasures, buttons.Target, buttons.NextCountermeasure);
        string Name(ControllerButton value) => !left && config.ControllerBindings.RightBumperZoom.Value && ControllerButtonNames.Resolve(value, frame) == ControllerButton.Shoulder
            ? "Reserved for zoom" : ControllerButtonNames.Describe(value, left, frame);
        return $"{(left ? "LEFT" : "RIGHT")} HAND • THROTTLE\n" +
            "Layout: " + (layout == ThrottleBindingLayout.VtolStyle ? "VTOL-style trigger brake" : layout == ThrottleBindingLayout.CountermeasureTrigger ? "Countermeasure trigger" : layout == ThrottleBindingLayout.BrakeTrigger ? "Brake trigger" : "Custom") +
            $"\n\nBrake: {Name(buttons.Brake)} (hold)\nCountermeasures: {Name(buttons.Countermeasures)}\n" +
            (config.ControllerBindings.SelectTargetOnStick.Value ? "Target selection is on the STICK hand.\n" : $"Select target: {Name(buttons.Target)} (release)\n") +
            $"CM type: {Name(buttons.NextCountermeasure)} (release)\n\n" +
            (wristConflict ? "Wrist button overlaps; edit custom binding.\n" :
                ThrottleBindingProfiles.Conflicts(buttons, frame) ? "Shared buttons detected; choose a layout.\n" : "") +
            "Grip throttle + thumbstick: VTOL angle.\nVTOL style: stick click selects a target.\nRight bumper is reserved while zoom is enabled.";
    }

    private string HandHelp(bool stick)
    {
        var config = ModConfiguration.Instance;
        var left = stick ? config.MotionControls.LeftHanded.Value : !config.MotionControls.LeftHanded.Value;
        var b = config.ControllerBindings;
        string Name(ControllerButton button) => !left && b.RightBumperZoom.Value && ControllerButtonNames.Resolve(button, TrackedControllers.NativeFrame(left)) == ControllerButton.Shoulder
            ? "Reserved for magnifier" : ControllerButtonNames.Describe(button, left, TrackedControllers.NativeFrame(left));
        string Action(string name, ControllerButton button) => name + "\n" + Name(button) + "\n\n";
        var text = (stick ? "STICK" : CollectiveControls ? "COLLECTIVE" : "THROTTLE") + " · " + (left ? "LEFT" : "RIGHT") + "\n\n";
        if (stick)
        {
            text += Action("Fire (hold)", b.Fire.Value) + Action("Next weapon (release)", b.NextWeapon.Value);
            if (b.SelectTargetOnStick.Value) text += Action("Select target (release)", b.SelectTarget.Value);
            text += Action("Unlock last (release)", b.UnlockTarget.Value) + Action("Clear all (release)", b.ClearTargets.Value);
        }
        else
        {
            text += Action("Brake (hold)", b.Brake.Value) + Action("Countermeasures (hold)", b.Countermeasures.Value) + Action("CM type (release)", b.NextCountermeasure.Value);
            if (!b.SelectTargetOnStick.Value) text += Action("Select target (release)", b.SelectTarget.Value);
            text += "Grip + thumbstick\nVTOL angle, when fitted\n\nL STK on MFD\nMap pan · click to recenter";
        }
        return text.TrimEnd();
    }

    private void AddLiveButton(Func<string> text, int column, int row, Action action) =>
        _liveLabels.Add((AddButton(text(), column, row, action), text));

    private Text AddButton(string text, int column, int row, Action action, int columns = 2)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_buttonParent, false);
        var width = columns == 3 ? 160f : 248f;
        rect.sizeDelta = new Vector2(width, 49);
        rect.anchoredPosition = new Vector2((column - (columns - 1) * .5f) * (width + 16), 177 - row * 57);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => action());
        NativeButtonFeedback.Configure(button, new Color(0.10f, 0.24f, 0.29f));
        return Label(rect, text, Vector2.zero, new Vector2(width - 10, 45), 20);
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
