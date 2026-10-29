using System;
using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrCamera;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-1150)]
internal sealed class UniversalCockpitPanel : MonoBehaviour
{
    internal static UniversalCockpitPanel? Instance { get; private set; }
    private CockpitPanelModel? _model;
    private CockpitPanelHud? _hud;
    private Aircraft? _aircraft;
    private readonly GuardedEjection _ejection = new();
    private readonly EjectionRequest _ejectionRequest = new();
    private readonly ControllerButtonGate[] _directButtons = { new(), new() };
    private readonly ControllerButtonGate _trigger = new();
    private LineRenderer? _laser;
    private Transform? _dot;
    private Material? _pointerMaterial;
    private Text? _hint;
    private string? _active;
    private int _owner = -1; // 0/1: direct left/right; 2: stick-hand laser
    private Vector3 _startHand;
    private float _pressTime;
    private bool _actionSent;
    private bool _visible;
    private Vector3 _ejectRest;
    private Vector3 _lastCalibration, _lastAnchor;
    private Quaternion _lastRotation;
    private int _guardHand = -1;
    private string? _hover;
    private string? _nearLeft, _nearRight, _rayHover;
    private bool _hoverDirect;
    private string _feedback = "";
    private string? _feedbackControl;
    private float _feedbackUntil;
    private bool _modelFailed;
    private readonly Dictionary<string, Vector3> _buttonRest = new();
    private readonly Dictionary<string, Vector3> _points = new()
    {
        ["mfd_left_prev"] = Vector3.zero, ["mfd_left_next"] = Vector3.zero,
        ["mfd_right_prev"] = Vector3.zero, ["mfd_right_next"] = Vector3.zero,
        ["gear"] = new(0, 0, -.057f), ["radar"] = new(0, 0, -.036f),
        ["engine"] = new(0, 0, -.036f), ["assist"] = new(0, 0, -.036f),
        ["weapon"] = Vector3.zero, ["map"] = Vector3.zero,
        ["cm"] = Vector3.zero, ["hover"] = Vector3.zero, ["zoom_in"] = Vector3.zero, ["zoom_out"] = Vector3.zero,
        ["guard"] = new(0, -.05f, -.017f), ["eject"] = new(0, -.025f, 0)
    };
    internal static bool IsDocked(Transform widget) => Instance?._hud?.Contains(widget) == true;
    internal static bool HasDisplays => Instance?._visible == true && Instance._hud?.HasStatus == true;
    private void Awake() => Instance = this;
    internal void ReleaseMap() => _hud?.ReleaseMap();

    private bool Ready(out Aircraft aircraft, out Transform root)
    {
        aircraft = null!; root = null!;
        if (!ModConfiguration.Instance.CockpitPanelEnabled.Value || !TrackedControllers.Available ||
            CameraStateManager.cameraMode != CameraMode.cockpit || !GameManager.GetLocalAircraft(out aircraft) ||
            aircraft.cockpit == null || aircraft.cockpit.IsDetached() || !GameManager.flightControlsEnabled ||
            Time.timeScale <= 0 || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen)) return false;
        var camera = APIBus.MainCamera;
        if (camera == null || camera.GetComponent<NOVRPoseDriver>() == null || camera.transform.parent == null ||
            SceneSingleton<CameraStateManager>.i?.followingUnit != aircraft) return false;
        var pilotReady = false;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) { pilotReady = true; break; }
        if (!pilotReady) return false;
        // Projected UI lives in NOVR's UI camera space, independent of the
        // aircraft's potentially distant world position. Match the wrist UI.
        if (NOUIManager.I == null || APIBus.CockpitHudCamera.transform.parent == null) return false;
        root = APIBus.CockpitHudCamera.transform.parent;
        return true;
    }

    private void Update()
    {
        var settings = ModConfiguration.Instance;
        if (Keyboard.current?.f6Key.wasPressedThisFrame == true)
        {
            CockpitLayout.Instance?.Finish(false);
            settings.CockpitPanelEnabled.Value = !settings.CockpitPanelEnabled.Value;
            if (settings.CockpitPanelEnabled.Value) _modelFailed = false;
        }
        if (!Ready(out var aircraft, out var root)) { Hide(); return; }
        if (_aircraft != aircraft)
        {
            Hide(); _aircraft = aircraft;
            _feedback = "FREE HAND  •  POINT + TRIGGER, OR REACH + TRIGGER / GRIP";
            _feedbackControl = null;
            _feedbackUntil = Time.unscaledTime + 6f;
        }
        if (_lastCalibration != NOVRHeadsetData.TranslationCalibrationOffset ||
            _lastAnchor != NOVRHeadsetData.TranslationAnchor || _lastRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Hide();
            _lastCalibration = NOVRHeadsetData.TranslationCalibrationOffset;
            _lastAnchor = NOVRHeadsetData.TranslationAnchor;
            _lastRotation = NOVRHeadsetData.RotationCalibrationOffset;
        }
        if (!TryCreateModel()) { Hide(); return; }
        _visible = true; _model!.Root.gameObject.SetActive(true);
        var offset = CockpitLayout.BoundPanel(CockpitLayout.PanelPosition);
        _model.Root.SetPositionAndRotation(root.TransformPoint(CockpitProfiles.SeatOffset + offset), root.rotation * CockpitLayout.PanelRotation);
        _model.Root.localScale = Vector3.one * settings.CockpitPanelScale.Value;
        _model.SetLayoutMode(CockpitLayout.Active);
        _hud!.SetVisible(!CockpitLayout.Active);
        if (CockpitLayout.Active)
        {
            CancelInteraction(); _laser!.enabled = false; _dot!.gameObject.SetActive(false);
            if (_hint != null) _hint.text = CockpitLayout.Hint;
            return;
        }
        // Leave the console visible during placement adjustments, but the wrist
        // menu owns input until closed. This also resets any open eject guard.
        if (WristMenu.IsOpen)
        {
            CancelInteraction(); _laser!.enabled = false; _dot!.gameObject.SetActive(false);
            if (_hint != null) _hint.text = WristMenu.IsConsoleSetup
                ? "CONSOLE SETUP  •  CHANGES APPLY TO EVERY AIRCRAFT"
                : "WRIST MENU OPEN  •  CONSOLE CONTROLS PAUSED";
            return;
        }
        var previousRequest = _ejectionRequest.State;
        _ejectionRequest.Update(aircraft.HasEjected(), Time.unscaledTime, Time.frameCount);
        if (previousRequest != EjectionRequestState.Unconfirmed && _ejectionRequest.State == EjectionRequestState.Unconfirmed)
            ClearInteraction();
        var motion = MotionFlightController.Instance;
        var leftStick = settings.MotionControls.LeftHanded.Value;
        var left = TrackedControllers.Read(true); var right = TrackedControllers.Read(false);
        if (_ejection.Open && (_guardHand == 0 ? !left.Valid : !right.Valid)) { _ejection.Reset(); End(); }
        var leftFree = !(leftStick ? motion?.StickHeld == true : motion?.ThrottleHeld == true);
        var rightFree = !(leftStick ? motion?.ThrottleHeld == true : motion?.StickHeld == true);
        _nearLeft = Direct(left, leftFree, root, 0);
        _nearRight = Direct(right, rightFree, root, 1);
        var hand = leftStick ? left : right;
        var free = leftStick ? leftFree : rightFree;
        var nearHand = leftStick ? _nearLeft : _nearRight;
        var nearOtherHand = leftStick ? _nearRight : _nearLeft;
        // Reaching takes precedence for that hand. The other free hand may still
        // point until a control is grabbed; never accept an invisible laser click.
        var pointerAllowed = !_ejectionRequest.BlocksInput && hand.Valid && free && nearHand == null &&
            settings.ControllerBindings.PointerEnabled.Value && (_owner < 0 || _owner == 2);
        var ray = new Ray(root.TransformPoint(hand.AimPosition + CockpitProfiles.SeatOffset), root.rotation * hand.AimRotation * Vector3.forward);
        var target = _rayHover = pointerAllowed ? RayTarget(ray, out _) : null;
        _trigger.Update(pointerAllowed, hand.Trigger);
        if (_trigger.Pressed && _owner < 0 && target != null) Begin(target, 2, Vector3.zero, hand);
        if (_owner == 2)
        {
            // Moving off the handle cancels the laser hold; it cannot resume while held.
            if (!_trigger.Held || target != _active)
            {
                if (!hand.Valid) _ejection.Reset();
                End();
            }
            else Held(Vector3.zero, hand);
        }
        if (_owner < 0 && !left.Valid && !right.Valid) _ejection.Reset();
        DrawPointer(ray, pointerAllowed, target);
        _hover = _active ?? nearHand ?? target ?? nearOtherHand;
        _hoverDirect = _active != null ? _owner != 2 : nearHand != null || target == null;
        if (_hint != null)
        {
            // Instructions and eject progress belong to the control being used.
            // An unrelated tracking notice or welcome message must not cover them.
            var resultFeedback = Time.unscaledTime < _feedbackUntil && _feedbackControl != null &&
                (_hover == _feedbackControl || _hover == null);
            _hint.text = RequestHint() ?? (_active == "eject" ? Hint(_active) : resultFeedback ? _feedback :
                _hover != null ? Hint(_hover) : motion?.PlacementHint ?? motion?.TrackingHint ??
                (Time.unscaledTime < _feedbackUntil ? _feedback : Hint(null)));
        }
    }

    private string? Direct(ControllerSample hand, bool free, Transform root, int index)
    {
        var available = !_ejectionRequest.BlocksInput && hand.Valid && free && (_owner < 0 || _owner == index);
        var position = root.TransformPoint(hand.Position + CockpitProfiles.SeatOffset);
        var target = available ? Near(position) : null;
        // Either pinch (trigger) or grip can operate a nearby physical control.
        // Track the combined press even away from the panel so arriving with a
        // held button cannot activate a switch or begin an eject pull.
        _directButtons[index].Update(available, Mathf.Max(hand.Grip, hand.Trigger));
        if (_directButtons[index].Pressed && _owner < 0 && target != null)
            Begin(target, index, GesturePosition(position), hand);
        if (_owner == index)
        {
            if (!_directButtons[index].Held)
            {
                if (!available) _ejection.Reset();
                End();
            }
            else Held(GesturePosition(position), hand);
        }
        return target;
    }

    // Rotate into panel axes without dividing by its visual scale. The gear and
    // eject thresholds stay in physical meters at every console size and tilt.
    private Vector3 GesturePosition(Vector3 worldPosition) =>
        Quaternion.Inverse(_model!.Root.rotation) * (worldPosition - _model.Root.position);

    private Vector3 Point(string key) => _model!.Groups[key].TransformPoint(_points[key]);
    private bool CanTarget(string key) => key != "eject" || _ejection.Open;
    private string? Near(Vector3 position)
    {
        string? best = null; var distance = .055f;
        foreach (var pair in _points)
        {
            if (!CanTarget(pair.Key)) continue;
            var candidate = Vector3.Distance(position, Point(pair.Key));
            if (candidate < distance) { distance = candidate; best = pair.Key; }
        }
        return best;
    }
    private string? RayTarget(Ray ray, out float distance)
    {
        string? best = null; distance = 2.5f;
        if (!FrontRay(ray, out _, out _)) return null;
        foreach (var pair in _points)
        {
            if (!CanTarget(pair.Key)) continue;
            var center = Point(pair.Key); var along = Vector3.Dot(center - ray.origin, ray.direction);
            var radius = pair.Key is "guard" or "eject" ? .041f : pair.Key is "cm" or "hover" or "zoom_in" or "zoom_out" ? .018f : .029f;
            if (pair.Key.StartsWith("mfd_")) radius = .013f;
            radius *= _model!.Root.localScale.x;
            if (along > .015f && along < distance && Vector3.Distance(ray.GetPoint(along), center) <= radius)
            { distance = along; best = pair.Key; }
        }
        return best;
    }
    private void Begin(string control, int owner, Vector3 position, ControllerSample hand)
    {
        _active = control; _owner = owner; _startHand = position; _pressTime = Time.unscaledTime; _actionSent = false;
        if (control == "guard")
        {
            if (_ejectionRequest.State == EjectionRequestState.Unconfirmed) _ejectionRequest.Reset();
            if (_ejection.Open) _ejection.Reset();
            else
            {
                _ejection.LiftCover();
                _guardHand = owner == 2 ? (ModConfiguration.Instance.MotionControls.LeftHanded.Value ? 0 : 1) : owner;
            }
        }
        else if (control == "eject") _ejection.BeginPull();
        else if (control != "gear" || owner == 2) Activate(control);
        Pulse(hand);
    }
    private void Held(Vector3 position, ControllerSample hand)
    {
        if (_active == "gear" && _owner != 2 && !_actionSent)
        {
            var movement = position.y - _startHand.y;
            if (Mathf.Abs(movement) >= .025f)
            {
                var wantDown = movement < 0;
                if (_aircraft != null && _aircraft.gearDeployed != wantDown) Activate("gear");
                _actionSent = true; Pulse(hand);
            }
        }
        if (_active == "eject" && _ejection.Pull(_startHand.z - position.z, Time.unscaledTime - _pressTime, _owner != 2))
        { Activate("eject"); Pulse(hand); End(); }
    }
    private void End()
    {
        _active = null; _owner = -1; _ejection.Release();
    }
    private void Activate(string control)
    {
        var action = control switch { "gear" => "Gear", "radar" => "Radar", "engine" => "Toggle Engine", "assist" => "Flight Assist", "hover" => "Auto Hover", "weapon" => "Next Weapon", "cm" => "Next Countermeasure", "eject" => "Eject", _ => null };
        if (control == "hover" && HoverAssist.UnavailableReason(_aircraft) is string reason)
        { Feedback(control, reason); return; }
        if (control == "assist" && _aircraft?.GetControlsFilter()?.HasFlightAssist() != true)
        { Feedback(control, "FLIGHT ASSIST IS NOT SWITCHABLE ON THIS AIRCRAFT"); return; }
        if (control == "radar" && _aircraft?.radar == null) { Feedback(control, "RADAR NOT FITTED TO THIS AIRCRAFT"); return; }
        if (control == "gear" && _aircraft != null && _aircraft.radarAlt <= .2f)
        { Feedback(control, "GEAR LOCKED ON GROUND"); return; }
        if (control.StartsWith("mfd_"))
        {
            _hud?.Cycle(control.StartsWith("mfd_left"), control.EndsWith("prev") ? -1 : 1);
            Feedback(control, "MFD PAGE CHANGED");
        }
        else if (control is "zoom_in" or "zoom_out")
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map != null && !DynamicMap.mapMaximized)
            {
                map.SetZoomLevel(Mathf.Clamp(map.GetZoomLevel() * (control == "zoom_in" ? 1.35f : 1 / 1.35f), 1, 12));
                Feedback(control, $"MAP ZOOM  {map.GetZoomLevel():0.0}×");
            }
        }
        else if (control == "map") ControllerActions.ToggleMap();
        else if (action != null && ControllerActions.Instance != null)
        {
            ControllerActions.Instance.QueuePanelAction(action);
            if (control == "eject") _ejectionRequest.Begin(Time.unscaledTime, Time.frameCount);
        }
        Debug.Log($"[NOVR] Universal cockpit panel: {control}.");
        _actionSent = true;
    }
    private void Feedback(string control, string message)
    { _feedbackControl = control; _feedback = message; _feedbackUntil = Time.unscaledTime + 2f; }
    private static void Pulse(ControllerSample hand)
    {
        if (hand.Device.isValid && hand.Device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            hand.Device.SendHapticImpulse(0, .22f, .035f);
    }
    private string? RequestHint() => _ejectionRequest.State switch
    {
        EjectionRequestState.Pending => "EJECT REQUESTED",
        EjectionRequestState.Accepted => "EJECTION STARTED",
        EjectionRequestState.Unconfirmed => "EJECT NOT CONFIRMED  •  RELEASE, THEN LIFT GUARD TO RETRY",
        _ => null
    };
    private string Hint(string? control)
    {
        var press = _hoverDirect ? "TRIGGER / GRIP" : "TRIGGER";
        return control switch
        {
            "gear" => _hoverDirect ? "GEAR  •  HOLD TRIGGER / GRIP + MOVE UP OR DOWN" : "GEAR  •  TRIGGER TO TOGGLE",
            "radar" => _aircraft?.radar == null ? "RADAR  •  NOT FITTED" : $"RADAR  •  {(_aircraft.radar.activated ? "ON" : "OFF")}  •  {press} TO TOGGLE",
            "engine" => $"ENGINE  •  {(_aircraft?.Ignition == true ? "ON" : "OFF")}  •  {press} TO TOGGLE",
            "assist" => _aircraft?.GetControlsFilter()?.HasFlightAssist() != true
                ? "FLIGHT ASSIST IS NOT SWITCHABLE ON THIS AIRCRAFT"
                : $"FLIGHT ASSIST  •  {(_aircraft.flightAssist ? "ON" : "OFF")}  •  {press} TO TOGGLE",
            "hover" => HoverAssist.UnavailableReason(_aircraft) ??
                $"HOVER ASSIST  •  {(_aircraft?.IsAutoHoverEnabled() == true ? "ON" : "OFF")}  •  {press} TO TOGGLE",
            "mfd_left_prev" or "mfd_right_prev" => $"PREVIOUS MFD PAGE  •  {press}",
            "mfd_left_next" or "mfd_right_next" => $"NEXT MFD PAGE  •  {press}",
            "weapon" => $"NEXT WEAPON  •  {press}", "cm" => $"NEXT COUNTERMEASURE  •  {press}",
            "zoom_in" => $"MAP ZOOM IN  •  {press}", "zoom_out" => $"MAP ZOOM OUT  •  {press}", "map" => $"OPEN TACTICAL MAP  •  {press}",
            "guard" => $"{(_ejection.Open ? "CLOSE" : "LIFT")} EJECT GUARD  •  {press}",
            "eject" => _hoverDirect ? "EJECT  •  HOLD TRIGGER / GRIP + PULL TOWARD YOU" :
                _active == "eject" ? $"EJECT  •  KEEP HOLDING  {Mathf.Clamp01((Time.unscaledTime - _pressTime) / .75f) * 100:0}%" :
                "EJECT  •  HOLD TRIGGER ON HANDLE FOR 0.75s",
            _ => _ejection.Open ? "EJECT GUARD OPEN" : ""
        };
    }
    private void LateUpdate()
    {
        if (!_visible || _model == null || _aircraft == null) return;
        // Native flight input runs after this component's Update. Reflect its
        // acknowledgement in the same rendered frame, including a ground egress.
        if (_ejectionRequest.BlocksInput && _aircraft.HasEjected())
        {
            _ejectionRequest.Update(true, Time.unscaledTime, Time.frameCount);
            if (_hint != null) _hint.text = RequestHint();
        }
        var blend = FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, 18);
        Rotate("gear", _aircraft.gearDeployed ? -30 : 30, blend);
        Rotate("radar", _aircraft.radar?.activated == true ? 25 : -25, blend);
        Rotate("engine", _aircraft.Ignition ? 25 : -25, blend);
        Rotate("assist", _aircraft.GetControlsFilter()?.HasFlightAssist() != true ? 0 : _aircraft.flightAssist ? 25 : -25, blend);
        Rotate("guard", _ejection.Open ? 110 : 0, blend);
        foreach (var point in _points)
            _model.Highlight(point.Key, _active == point.Key ? 2 : _active == null &&
                (_nearLeft == point.Key || _nearRight == point.Key || _rayHover == point.Key) ? 1 : 0);
        _model.SetGroupColor("gear_lamps", _aircraft.gearState switch
        {
            LandingGear.GearState.LockedExtended => new Color(.3f, 1f, .5f),
            LandingGear.GearState.Extending or LandingGear.GearState.Retracting => new Color(1f, .6f, .02f),
            _ => new Color(.025f, .04f, .03f)
        });
        _model.SetGroupColor("hover_lamp", _aircraft.IsAutoHoverEnabled()
            ? new Color(.3f, 1f, .5f) : new Color(.025f, .04f, .03f));
        _model.Groups["eject"].localPosition = _ejectRest + Vector3.back * (_active == "eject" ? .02f : 0);
        foreach (var button in _buttonRest)
            _model.Groups[button.Key].localPosition = button.Value + Vector3.forward * (_active == button.Key ? .003f : 0);
        if (!CockpitLayout.Active) _hud!.Update();
    }
    private bool TryCreateModel()
    {
        if (_modelFailed) return false;
        try { EnsureModel(); return true; }
        catch (Exception exception)
        {
            _hud?.Restore(); _hud = null; _model?.Dispose(); _model = null;
            if (_pointerMaterial != null) Destroy(_pointerMaterial);
            _pointerMaterial = null; _buttonRest.Clear(); _modelFailed = true;
            Debug.LogError($"[NOVR] Console unavailable; keeping the original HUD. Toggle F6 off/on to retry. {exception}");
            return false;
        }
    }
    private void Rotate(string key, float angle, float blend) => _model!.Groups[key].localRotation =
        Quaternion.Slerp(_model.Groups[key].localRotation, Quaternion.Euler(angle, 0, 0), blend);
    private void EnsureModel()
    {
        if (_model != null) return;
        _model = new CockpitPanelModel(transform); _ejectRest = _model.Groups["eject"].localPosition;
        _hud = new CockpitPanelHud(_model.Root);
        foreach (var key in new[] { "weapon", "map", "cm", "hover", "zoom_in", "zoom_out", "mfd_left_prev", "mfd_left_next", "mfd_right_prev", "mfd_right_next" })
            _buttonRest[key] = _model.Groups[key].localPosition;
        var hintObject = new GameObject("Console interaction hint", typeof(RectTransform), typeof(Canvas));
        hintObject.transform.SetParent(_model.Root, false); hintObject.transform.localScale = Vector3.one * .001f;
        hintObject.transform.localPosition = new Vector3(0, .19f, -.03f);
        var canvas = hintObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = APIBus.CockpitHudCamera;
        var textObject = new GameObject("Hint", typeof(RectTransform), typeof(Text)); textObject.transform.SetParent(hintObject.transform, false);
        ((RectTransform)textObject.transform).sizeDelta = new Vector2(780, 30);
        _hint = textObject.GetComponent<Text>(); _hint.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _hint.fontSize = 12; _hint.alignment = TextAnchor.MiddleCenter; _hint.color = new Color(.75f, 1, .8f); _hint.raycastTarget = false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        _pointerMaterial = new Material(shader) { color = new Color(.4f, 1, .7f) };
        if (_pointerMaterial.HasProperty("_BaseColor")) _pointerMaterial.SetColor("_BaseColor", _pointerMaterial.color);
        var laserObject = new GameObject("Console pointer", typeof(LineRenderer)); laserObject.transform.SetParent(_model.Root, false);
        _laser = laserObject.GetComponent<LineRenderer>(); _laser.sharedMaterial = _pointerMaterial; _laser.positionCount = 2;
        _laser.startWidth = .0013f; _laser.endWidth = .0007f; _laser.shadowCastingMode = ShadowCastingMode.Off; _laser.receiveShadows = false;
        _dot = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; _dot.name = "Console pointer dot";
        Destroy(_dot.GetComponent<Collider>()); _dot.SetParent(_model.Root, false); _dot.localScale = Vector3.one * .007f;
        _dot.GetComponent<Renderer>().sharedMaterial = _pointerMaterial;
        LayerHelper.SetLayerRecursive(_model.Root, LayerHelper.GetVrUiLayer());
        Debug.Log("[NOVR] Created shared 3D cockpit console with live HUD/minimap docking.");
    }
    private bool FrontRay(Ray ray, out Vector3 origin, out Vector3 direction)
    {
        var root = _model!.Root;
        origin = root.InverseTransformPoint(ray.origin) - new Vector3(0, 0, -.05f);
        direction = root.InverseTransformVector(ray.direction);
        return TrackedControllers.Finite(origin) && TrackedControllers.Finite(direction) && origin.z < 0 && direction.z > 0;
    }
    private void DrawPointer(Ray ray, bool allowed, string? target)
    {
        var distance = 0f;
        var hit = allowed && FrontRay(ray, out var origin, out var direction) &&
            (target != null || (PointerGeometry.IntersectRect(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z,
                -.385f, -.18f, .385f, .18f, out distance) && distance <= 2.5f));
        _laser!.enabled = hit; _dot!.gameObject.SetActive(hit);
        if (!hit) return;
        // A raised lever/guard can be hit just outside the board's silhouette.
        // Its visible pointer must still agree with the accepted target.
        var point = target == null ? ray.GetPoint(distance) : Point(target);
        _laser.SetPosition(0, ray.origin); _laser.SetPosition(1, point);
        _dot.position = point - ray.direction * .003f;
    }
    private void Hide()
    {
        _hud?.Restore(); _visible = false; CancelInteraction();
        if (_model != null) _model.Root.gameObject.SetActive(false);
    }
    private void CancelInteraction()
    {
        _ejectionRequest.Reset();
        ClearInteraction();
    }
    private void ClearInteraction()
    {
        _ejection.Reset(); _active = _hover = null; _owner = _guardHand = -1;
        _nearLeft = _nearRight = _rayHover = null;
        _trigger.Reset(); foreach (var button in _directButtons) button.Reset();
    }
    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        Hide(); _model?.Dispose();
        if (_pointerMaterial != null) Destroy(_pointerMaterial);
        if (Instance == this) Instance = null;
    }
}

[HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.Maximize))]
internal static class ConsoleMapUndockPatch
{
    [HarmonyPrefix] private static void Prefix() => UniversalCockpitPanel.Instance?.ReleaseMap();
}
