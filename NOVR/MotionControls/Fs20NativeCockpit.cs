using System;
using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrCamera;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// FS-20 only. Every switch is attached to the native cockpit transform; neither
// the seat offset nor the universal panel's placement is used for its geometry.
[DefaultExecutionOrder(-1140)]
internal sealed class Fs20NativeCockpit : MonoBehaviour
{
    internal static Fs20NativeCockpit? Instance { get; private set; }
    internal static bool IsAircraft(Aircraft? aircraft) => aircraft != null && Fs20ControlMap.Matches(aircraft.definition?.jsonKey);
    internal static bool Selected => GameManager.GetLocalAircraft(out var aircraft) && IsAircraft(aircraft);
    internal static bool BrakeHeld => Instance?._ready == true && Instance._active == "brake";
    private Aircraft? _aircraft;
    private Transform? _root;
    private NativeCockpitMesh? _mesh;
    private readonly Dictionary<string, Vector3> _rests = new();
    private readonly ControllerButtonGate[] _direct = { new(), new() };
    private readonly PointerHandSelection _pointer = new();
    private readonly GuardedEjection _ejection = new();
    private readonly EjectionRequest _request = new();
    private LineRenderer? _laser, _ring;
    private Transform? _ejectHandle, _guard;
    private TextMesh? _hint;
    private Material? _accent, _textMaterial, _yellow, _black;
    private Font? _font;
    private string? _active, _hover;
    private int _owner = -1, _guardHand = -1;
    private bool _rayOwnerLeft, _ready, _sent;
    private Vector3 _start, _calibration, _anchor;
    private Quaternion _recenter;
    private float _pressedAt, _feedbackUntil, _knobAngle;
    private string _feedback = "";
    private TacScreen? _tactical;
    private float _initialRange, _initialPixels;
    private int _findAttempts;
    private float _nextFind;
    private static readonly Vector3 EjectPoint = new(0,-.055f,.448f);
    private static readonly Vector3 FaceNormal = new(0,.205f,-.979f);
    private static readonly System.Reflection.FieldInfo RangeField = AccessTools.Field(typeof(TacScreen), "displaySize");
    private static readonly System.Reflection.FieldInfo PixelsField = AccessTools.Field(typeof(TacScreen), "metersPerPixel");
    private static readonly System.Reflection.FieldInfo IconsField = AccessTools.Field(typeof(TacScreen), "iconLayer");
    private void Awake() => Instance = this;

    private bool Ready(out Aircraft aircraft, out Transform tracking)
    {
        aircraft = null!; tracking = null!;
        if (!ModConfiguration.Instance.Fs20NativeCockpitEnabled.Value || !TrackedControllers.Available ||
            !GameManager.GetLocalAircraft(out aircraft) || !IsAircraft(aircraft) || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || aircraft.HasEjected() || CameraStateManager.cameraMode != CameraMode.cockpit ||
            !GameManager.flightControlsEnabled || Time.timeScale <= 0 || WristMenu.IsOpen || CockpitLayout.Active ||
            DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse())) return false;
        var camera = APIBus.MainCamera;
        if (camera == null || camera.GetComponent<NOVRPoseDriver>() == null || camera.transform.parent == null ||
            SceneSingleton<CameraStateManager>.i?.followingUnit != aircraft) return false;
        var pilotReady = false;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) { pilotReady = true; break; }
        if (!pilotReady) return false;
        tracking = camera.transform.parent; return true;
    }
    private void Update()
    {
        if (!Ready(out var aircraft, out var tracking))
        {
            Pause();
            if (_aircraft != null && (!GameManager.GetLocalAircraft(out var current) || current != _aircraft)) Release();
            return;
        }
        if (_aircraft != aircraft || _root == null)
        {
            Release();
            var cockpit = aircraft.cockpit.GetComponentInChildren<Cockpit>(true);
            var filter = cockpit?.GetComponent<MeshFilter>();
            if (filter?.sharedMesh == null || cockpit!.name != "cockpit_int") return;
            _aircraft = aircraft;
            _root = new GameObject("NOVR FS-20 integrated interactions").transform; _root.SetParent(cockpit.transform,false);
            try { CreateVisuals(); }
            catch (Exception e) { Release(); Debug.LogError("[NOVR] FS-20 interaction setup failed: " + e.Message); enabled = false; return; }
            try
            {
                _mesh = new NativeCockpitMesh(filter,_root);
                foreach (var part in _mesh.Parts) _rests[part.Key] = part.Value.localPosition;
            }
            catch (Exception e) { Debug.LogWarning("[NOVR] FS-20 switch animation unavailable; retaining native geometry and click targets: " + e.Message); }
            Debug.Log("[NOVR] FS-20 native cockpit interactions attached. Original screens and warning lights retained.");
        }
        if (_calibration != NOVRHeadsetData.TranslationCalibrationOffset || _anchor != NOVRHeadsetData.TranslationAnchor || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Cancel(); _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _anchor = NOVRHeadsetData.TranslationAnchor;
            _recenter = NOVRHeadsetData.RotationCalibrationOffset;
        }
        _ready = true; _root!.gameObject.SetActive(true); _mesh?.SetVisible(true);
        _request.Update(aircraft.HasEjected(),Time.unscaledTime,Time.frameCount);
        if (_tactical == null && _findAttempts < 8 && Time.unscaledTime >= _nextFind)
        {
            _findAttempts++; _nextFind = Time.unscaledTime + 1;
            _tactical = aircraft.cockpit.GetComponentInChildren<TacScreen>(true);
            if (_tactical != null) { _initialRange = (float)RangeField.GetValue(_tactical); _initialPixels = (float)PixelsField.GetValue(_tactical); }
        }
        var left = TrackedControllers.Read(true); var right = TrackedControllers.Read(false);
        if (_ejection.Open && (_guardHand == 0 ? !left.Valid : !right.Valid)) { _ejection.Reset(); End(); }
        var motion = MotionFlightController.Instance; var leftStick = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        var lf = left.Valid && !HelmetNightVision.OwnsHand(true) && !(leftStick ? motion?.StickHeld == true : motion?.ThrottleHeld == true);
        var rf = right.Valid && !HelmetNightVision.OwnsHand(false) && !(leftStick ? motion?.ThrottleHeld == true : motion?.StickHeld == true);
        var nearL = Direct(left,lf,tracking,0); var nearR = Direct(right,rf,tracking,1);
        var pointerOn = ModConfiguration.Instance.ControllerBindings.PointerEnabled.Value && !_request.BlocksInput;
        var la = pointerOn && lf && nearL == null && (_owner < 0 || _owner == 2 && _rayOwnerLeft);
        var ra = pointerOn && rf && nearR == null && (_owner < 0 || _owner == 2 && !_rayOwnerLeft);
        var lr = new Ray(tracking.TransformPoint(left.AimPosition),tracking.rotation * left.AimRotation * Vector3.forward);
        var rr = new Ray(tracking.TransformPoint(right.AimPosition),tracking.rotation * right.AimRotation * Vector3.forward);
        var lt = la ? RayTarget(lr) : null; var rt = ra ? RayTarget(rr) : null;
        _pointer.Update(leftStick,la,left.Trigger,lt != null,ra,right.Trigger,rt != null);
        var hand = _pointer.Left ? left : right; var ray = _pointer.Left ? lr : rr;
        var target = _pointer.Left ? lt : rt;
        if (_pointer.Trigger.Pressed && _owner < 0 && target != null)
        { _rayOwnerLeft = _pointer.Left; Begin(target,2,Vector3.zero,hand); }
        if (_owner == 2)
        {
            if (!_pointer.Trigger.Held || target != _active) End();
            else Hold(Vector3.zero,hand);
        }
        _hover = _active ?? nearL ?? nearR ?? target;
        _laser!.enabled = target != null && (_pointer.Left ? la : ra);
        if (_laser.enabled) { _laser.SetPosition(0,ray.origin); _laser.SetPosition(1,Point(target!)); }
        ShowHint(_hover);
    }
    private string? Direct(ControllerSample hand, bool free, Transform tracking, int index)
    {
        var available = free && !_request.BlocksInput && (_owner < 0 || _owner == index);
        var world = tracking.TransformPoint(hand.Position);
        var target = available ? Near(world) : null;
        _direct[index].Update(available,Mathf.Max(hand.Trigger,hand.Grip));
        if (_direct[index].Pressed && _owner < 0 && target != null) Begin(target,index,LocalMeters(world),hand);
        if (_owner == index)
        {
            if (!_direct[index].Held) End();
            else if (_active != "gear" && _active != "eject" && Vector3.Distance(world,Point(_active!)) > .09f) End();
            else Hold(LocalMeters(world),hand);
        }
        return target;
    }
    private Vector3 LocalMeters(Vector3 world) => Quaternion.Inverse(_root!.rotation) * (world - _root.position);
    private Vector3 LocalPoint(string id)
    {
        if (id == "guard") return _root!.InverseTransformPoint(_guard!.TransformPoint(new Vector3(0,-.026f,-.003f)));
        if (id == "eject") return _ejectHandle!.localPosition;
        foreach (var c in Fs20ControlMap.Controls)
            if (c.Id == id)
                return _mesh != null && _mesh.Parts.TryGetValue(id,out var part)
                    ? part.localPosition + part.localRotation * (c.Point - _rests[id]) : c.Point;
        return Vector3.zero;
    }
    private Vector3 Point(string id) => _root!.TransformPoint(LocalPoint(id));
    private IEnumerable<string> Targets()
    {
        foreach (var c in Fs20ControlMap.Controls) yield return c.Id;
        yield return "guard";
        if (_ejection.Open) yield return "eject";
    }
    private string? Near(Vector3 world)
    {
        var distance = .035f; string? best = null;
        foreach (var id in Targets()) { var d = Vector3.Distance(world,Point(id)); if (d < distance) { best = id; distance = d; } }
        return best;
    }
    private string? RayTarget(Ray ray)
    {
        // Only the pilot-facing side is clickable. Aiming through the back of
        // the cluster or across its side cannot operate an unseen switch.
        if (Vector3.Dot(ray.direction,_root!.TransformDirection(FaceNormal)) > -.15f) return null;
        var error = float.MaxValue; string? best = null;
        foreach (var id in Targets())
        {
            var center = Point(id); var along = Vector3.Dot(center-ray.origin,ray.direction);
            var d = Vector3.Distance(ray.GetPoint(along),center);
            var radius = id is "gear" or "weapon" or "guard" or "eject" ? .024f : .011f;
            if (along > .025f && along < 1.8f && d < radius && d/radius < error)
            { best = id; error = d/radius; }
        }
        return best;
    }
    private void Begin(string id, int owner, Vector3 local, ControllerSample hand)
    {
        _active = id; _owner = owner; _start = local; _pressedAt = Time.unscaledTime; _sent = false;
        if (id == "guard")
        {
            if (_request.State == EjectionRequestState.Unconfirmed) _request.Reset();
            if (_ejection.Open) _ejection.Reset();
            else { _ejection.LiftCover(); _guardHand = owner == 2 ? (_rayOwnerLeft ? 0 : 1) : owner; }
        }
        else if (id == "eject") _ejection.BeginPull();
        else if (id != "brake" && (id != "gear" || owner == 2)) Activate(id);
        InteractionHaptics.Pulse(hand.Device,cue:id == "guard" ? InteractionCue.Guard : InteractionCue.Click);
    }
    private void Hold(Vector3 local, ControllerSample hand)
    {
        if (_active == "gear" && _owner != 2 && !_sent && Mathf.Abs(local.y-_start.y) >= .025f)
        {
            if (_aircraft!.gearDeployed != (local.y < _start.y)) Activate("gear");
            _sent = true; InteractionHaptics.Pulse(hand.Device);
        }
        if (_active == "eject" && _ejection.Pull(_start.z-local.z,Time.unscaledTime-_pressedAt,_owner != 2))
        {
            ControllerActions.Instance?.QueuePanelAction("Eject"); _request.Begin(Time.unscaledTime,Time.frameCount);
            InteractionHaptics.Pulse(hand.Device,.5f,.065f,InteractionCue.Eject); End();
        }
    }
    private void Activate(string id)
    {
        if (id == "gear" && _aircraft!.radarAlt <= .2f) { Feedback("GEAR LOCKED ON GROUND"); return; }
        if (id == "hover" && HoverAssist.UnavailableReason(_aircraft) is string reason) { Feedback(reason); return; }
        if (id == "assist" && _aircraft!.GetControlsFilter()?.HasFlightAssist() != true) { Feedback("FLIGHT ASSIST FIXED ON THIS AIRCRAFT"); return; }
        if (id == "radar" && _aircraft!.radar == null) { Feedback("RADAR NOT FITTED"); return; }
        switch (id)
        {
            case "range_in": ChangeRange(.5f); break;
            case "range_out": ChangeRange(2); break;
            case "map": ControllerActions.ToggleMap(); break;
            case "aots": OpticalTargetingHud.Toggle(); break;
            case "head": OpticalTargetingHud.LookToAim(); break;
            case "unlock": SceneSingleton<CombatHUD>.i?.DeselectLast(); break;
            case "clear": SceneSingleton<CombatHUD>.i?.DeselectAll(); break;
            default:
                foreach (var c in Fs20ControlMap.Controls) if (c.Id == id && c.Action.Length > 0) ControllerActions.Instance?.QueuePanelAction(c.Action);
                if (id is "weapon" or "next_weapon" or "prev_weapon") _knobAngle += id == "prev_weapon" ? -45 : 45;
                break;
        }
    }
    private void ChangeRange(float factor)
    {
        if (_tactical == null) { Feedback("TACTICAL DISPLAY INITIALIZING"); return; }
        var current = (float)RangeField.GetValue(_tactical); var next = Mathf.Clamp(current * factor,5000,160000);
        if (current <= 0 || next == current) return;
        RangeField.SetValue(_tactical,next); PixelsField.SetValue(_tactical,(float)PixelsField.GetValue(_tactical) * current / next);
        if (IconsField.GetValue(_tactical) is Transform icons)
            for (var i = 0; i < icons.childCount; i++) icons.GetChild(i).localPosition *= current / next;
        Feedback($"TACTICAL SPAN {next / 1000:0} KM");
    }
    private void Feedback(string text) { _feedback = text; _feedbackUntil = Time.unscaledTime + 2; }
    private void ShowHint(string? id)
    {
        _ring!.enabled = id != null;
        if (id != null)
        {
            var local = LocalPoint(id) + FaceNormal * .003f;
            for (var i = 0; i < 25; i++)
            {
                var angle = i * Mathf.PI * 2 / 24;
                var offset = Vector3.right * Mathf.Cos(angle) * .014f + new Vector3(0,.979f,.205f) * Mathf.Sin(angle) * .014f;
                _ring.SetPosition(i,_root!.TransformPoint(local+offset));
            }
            _hint!.transform.localPosition = local + new Vector3(0,.039f,-.009f);
        }
        var message = "";
        if (_request.BlocksInput) message = "EJECT REQUESTED";
        else if (_request.State == EjectionRequestState.Unconfirmed) message = "EJECT NOT CONFIRMED · LIFT GUARD TO RETRY";
        else if (Time.unscaledTime < _feedbackUntil) message = _feedback;
        else if (id == "guard") message = _ejection.Open ? "CLOSE EJECT GUARD" : "LIFT EJECT GUARD";
        else if (id == "eject") message = _owner == 2 ? "EJECT · FRESH TRIGGER PRESS" : "EJECT · GRAB + PULL TOWARD YOU";
        else if (id != null)
            foreach (var c in Fs20ControlMap.Controls) if (c.Id == id) message = c.Label + (id switch
            {
                "gear" => " · " + (_aircraft!.gearDeployed ? "DOWN" : "UP"),
                "radar" => " · " + (_aircraft!.radar?.activated == true ? "ON" : "OFF"),
                "engine" => " · " + (_aircraft!.Ignition ? "ON" : "OFF"),
                "assist" => " · " + (_aircraft!.flightAssist ? "ON" : "OFF"),
                "hover" => " · " + (_aircraft!.IsAutoHoverEnabled() ? "ON" : "OFF"),
                "aots" => " · " + (ModConfiguration.Instance.OpticalHud.Enabled.Value ? "ON" : "OFF"), _ => ""
            });
        if (id == null) _hint!.transform.localPosition = new Vector3(0,.325f,.510f);
        _hint!.text = message;
    }
    private void LateUpdate()
    {
        if (!_ready || _aircraft == null || _root == null) return;
        var blend = FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime,18);
        if (_mesh != null)
            foreach (var c in Fs20ControlMap.Controls)
            {
                if (!_mesh.Parts.TryGetValue(c.Id,out var part)) continue;
                var angle = c.Id switch
                {
                    "engine" => _aircraft.Ignition ? 60 : 0,
                    "assist" => _aircraft.flightAssist ? 0 : -60,
                    "radar" => _aircraft.radar?.activated == true ? 60 : 0,
                    "hover" => _aircraft.IsAutoHoverEnabled() ? 60 : 0,
                    "brake" => _aircraft.GetInputs().brake > .05f ? 60 : 0, _ => 0
                };
                var target = _rests[c.Id];
                if (c.Id == "gear" && _aircraft.gearDeployed) target += new Vector3(0,-.032f,-.0067f);
                if (c.Animation == Fs20ControlMap.Motion.Button && _active == c.Id) target -= FaceNormal * .002f;
                part.localPosition = Vector3.Lerp(part.localPosition,target,blend);
                part.localRotation = Quaternion.Slerp(part.localRotation,c.Animation == Fs20ControlMap.Motion.Knob
                    ? Quaternion.AngleAxis(_knobAngle,FaceNormal) : Quaternion.Euler(angle,0,0),blend);
            }
        _guard!.localRotation = Quaternion.Slerp(_guard.localRotation,Quaternion.Euler(_ejection.Open ? -100 : 0,0,0),blend);
        _ejectHandle!.localPosition = EjectPoint + Vector3.back * (_active == "eject" ? .018f : 0);
    }
    private void CreateVisuals()
    {
        _accent = CockpitMaterials.Opaque("FS-20 interaction cue",new Color(.45f,.9f,.8f));
        _yellow = CockpitMaterials.Opaque("FS-20 eject yellow",new Color(.85f,.65f,.06f));
        _black = CockpitMaterials.Opaque("FS-20 eject guard",new Color(.035f,.045f,.045f));
        _laser = Line("FS-20 pointer",2,.001f); _ring = Line("FS-20 hover",25,.0008f);
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _textMaterial = new Material(Shader.Find("UI/Default")); _textMaterial.SetInt("unity_GUIZTestMode",(int)CompareFunction.LessEqual);
        _textMaterial.color = new Color(.8f,.95f,.9f); RefreshFont(_font); Font.textureRebuilt += RefreshFont;
        var label = new GameObject("FS-20 contextual hint",typeof(TextMesh)); label.transform.SetParent(_root,false);
        label.layer = (int)LayerHelper.Layers.Cockpit; label.transform.localRotation = Quaternion.Euler(12,0,0);
        _hint = label.GetComponent<TextMesh>(); _hint.font = _font; _hint.fontSize = 48; _hint.characterSize = .0024f;
        _hint.anchor = TextAnchor.MiddleCenter; _hint.alignment = TextAlignment.Center;
        label.GetComponent<MeshRenderer>().sharedMaterial = _textMaterial;
        // A seat-style pull loop below the existing center pedestal. The native
        // JETTISON control is deliberately not repurposed as ejection.
        _ejectHandle = new GameObject("FS-20 eject pull loop").transform; _ejectHandle.SetParent(_root,false); _ejectHandle.localPosition = EjectPoint;
        Bar(_ejectHandle,new(-.025f,0,0),new(.009f,.035f,.012f),_yellow);
        Bar(_ejectHandle,new(.025f,0,0),new(.009f,.035f,.012f),_yellow);
        Bar(_ejectHandle,new(0,-.017f,0),new(.058f,.009f,.012f),_yellow);
        for (var i = -2; i <= 2; i++) Bar(_ejectHandle,new(i*.011f,-.017f,-.0002f),new(.005f,.0092f,.0124f),_black);
        _guard = new GameObject("FS-20 eject guard").transform; _guard.SetParent(_root,false); _guard.localPosition = EjectPoint + Vector3.up*.026f + Vector3.back*.008f;
        Bar(_guard,new(0,-.026f,0),new(.069f,.055f,.005f),_black);
        var guardLabel = new GameObject("EJECT",typeof(TextMesh)); guardLabel.transform.SetParent(_guard,false); guardLabel.layer = (int)LayerHelper.Layers.Cockpit;
        guardLabel.transform.localPosition = new Vector3(0,-.026f,-.003f);
        var text = guardLabel.GetComponent<TextMesh>(); text.font = _font; text.fontSize = 48; text.characterSize = .002f; text.anchor = TextAnchor.MiddleCenter; text.text = "EJECT";
        guardLabel.GetComponent<MeshRenderer>().sharedMaterial = _textMaterial;
    }
    private LineRenderer Line(string name, int count, float width)
    {
        var go = new GameObject(name,typeof(LineRenderer)); go.transform.SetParent(_root,false); go.layer = (int)LayerHelper.Layers.Cockpit;
        var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = _accent; line.positionCount = count;
        line.startWidth = line.endWidth = width; line.enabled = false; line.shadowCastingMode = ShadowCastingMode.Off; return line;
    }
    private static void Bar(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.layer = (int)LayerHelper.Layers.Cockpit; go.GetComponent<Renderer>().sharedMaterial = material;
    }
    private void RefreshFont(Font font) { if (font == _font && _textMaterial != null) _textMaterial.mainTexture = font.material.mainTexture; }
    private void End() { _active = null; _owner = -1; _ejection.Release(); }
    private void Cancel()
    {
        End(); _hover = null; _ejection.Reset(); _request.Reset(); _guardHand = -1;
        foreach (var gate in _direct) gate.Reset(); _pointer.Reset();
    }
    private void Pause()
    {
        _ready = false; Cancel(); _mesh?.SetVisible(false); if (_root != null) _root.gameObject.SetActive(false);
    }
    private void Release()
    {
        Pause();
        if (_tactical != null && _initialRange > 0)
        {
            var current = (float)RangeField.GetValue(_tactical);
            if (IconsField.GetValue(_tactical) is Transform icons)
                for (var i = 0; i < icons.childCount; i++) icons.GetChild(i).localPosition *= current / _initialRange;
            RangeField.SetValue(_tactical,_initialRange); PixelsField.SetValue(_tactical,_initialPixels);
        }
        _tactical = null; _findAttempts = 0; _nextFind = 0; _initialRange = _initialPixels = 0;
        Font.textureRebuilt -= RefreshFont; _mesh?.Dispose(); _mesh = null; _rests.Clear();
        if (_root != null) Destroy(_root.gameObject);
        foreach (var material in new[] { _accent,_textMaterial,_yellow,_black }) if (material != null) Destroy(material);
        _root = null; _aircraft = null; _knobAngle = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (Instance == this) Instance = null; }
}
