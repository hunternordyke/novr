using System.Collections.Generic;
using NOVR.VrCamera;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// Integrated FS-20 controls are physical-only. No controller ray can operate
// a switch or an eject handle. Native aircraft logic owns every action.
[DefaultExecutionOrder(-1140)]
internal sealed class Fs20NativeCockpit : MonoBehaviour
{
    internal static Fs20NativeCockpit? Instance { get; private set; }
    internal static bool IsAircraft(Aircraft? aircraft) => aircraft != null && Fs20ControlMap.Matches(aircraft.definition?.jsonKey);
    internal static bool Selected => GameManager.GetLocalAircraft(out var aircraft) && IsAircraft(aircraft);
    internal static bool Active => Instance?._ready == true;
    internal static bool BrakeHeld => Active && Instance!._active == "brake";
    internal static bool IsDocked(Transform widget) => Instance?._hud?.Contains(widget) == true;
    internal static Transform? Cockpit => Active ? Instance?._root : null;
    internal static void ReleaseMap() => Instance?._hud?.ReleaseMap();
    internal static bool LeftStickSlaved => Active && Instance?._hud?.LeftStickSlaved == true && !WristMenu.IsOpen && !CockpitLayout.Active;
    internal static bool Pinching(bool left) => Active && Instance!._owner == (left ? 0 : 1) && Instance._active is "gear" or "radar" or "aots";
    private static CockpitLayout.Element SideHandle(bool left) => left == ModConfiguration.Instance.MotionControls.LeftHanded.Value ? CockpitLayout.Element.Stick : CockpitLayout.Element.Throttle;
    private Aircraft? _aircraft;
    private Transform? _root, _displayRoot, _seat;
    private CockpitPanelHud? _hud;
    private Fs20ControlCover? _nativeCover;
    private readonly Dictionary<string, Transform> _actuators = new();
    private readonly Dictionary<string, TextMesh> _labels = new();
    private readonly Dictionary<string, Renderer> _faces = new();
    private readonly List<Mesh> _hardwareMeshes = new();
    private readonly ControllerButtonGate[] _grips = { new(), new() }, _presses = { new(), new() };
    private readonly Dictionary<string, CockpitTouchGate>[] _touch = { new(), new() };
    private readonly string?[] _near = new string?[2];
    private readonly SeatHandlePull _pull = new();
    private readonly EjectionRequest _request = new();
    private Material? _base, _cap, _hover, _on, _metal, _textMaterial;
    private Font? _font;
    private TextMesh? _hint;
    private readonly TextMesh?[] _mfdTitles = new TextMesh?[2];
    private string? _active;
    private int _owner = -1;
    private Vector3 _start, _calibration, _anchor;
    private Quaternion _recenter;
    private bool _ready, _sent, _physicalPress;
    private Quaternion _heldHandRotation;
    private Vector3 _seatGrab;
    private float _seatLift;
    private float _feedbackUntil;
    private string _feedback = "";
    internal static Pose? HandPose(bool left, out bool pointing)
    {
        pointing = false; var self = Instance; var side = left ? 0 : 1;
        if (self?._ready != true) return null;
        var near = self._near[side];
        pointing = near != null && (near.EndsWith("_prev") || near.EndsWith("_next") || near.EndsWith("_lock") || near.StartsWith("seat_") || near is "brake" or "map_pan");
        if (self._owner != side || self._active == null) return null;
        if (self._active.StartsWith("eject_") && self._seat != null)
            return new Pose(self._seat.TransformPoint(self._seatGrab + Vector3.up * self._seatLift), self._seat.rotation * self._heldHandRotation);
        if (self._active is "gear" or "radar" or "aots")
        {
            var actuator = self._actuators[self._active];
            return new Pose(actuator.TransformPoint(new Vector3(0,0,-.022f)), actuator.rotation * self._heldHandRotation);
        }
        return null;
    }
    private void Awake() => Instance = this;
    private void OnEnable() => Application.onBeforeRender += PlaceDisplays;
    private bool Ready(out Aircraft aircraft)
    {
        aircraft = null!;
        if (!ModConfiguration.Instance.Fs20NativeCockpitEnabled.Value || !UnityEngine.XR.XRSettings.enabled ||
            !GameManager.GetLocalAircraft(out aircraft) || !IsAircraft(aircraft) || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || aircraft.HasEjected() || CameraStateManager.cameraMode != CameraMode.cockpit) return false;
        var camera = APIBus.MainCamera;
        if (camera == null || camera.GetComponent<NOVRPoseDriver>() == null || camera.transform.parent == null ||
            NOUIManager.I == null || APIBus.CockpitHudCamera?.transform.parent == null ||
            SceneSingleton<CameraStateManager>.i?.followingUnit != aircraft) return false;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) return true;
        return false;
    }
    private void Update()
    {
        if (!Ready(out var aircraft))
        {
            Pause();
            if (_aircraft != null && (!GameManager.GetLocalAircraft(out var current) || current != _aircraft)) Release();
            return;
        }
        if (_aircraft != aircraft || _root == null)
        {
            Release();
            var cockpit = aircraft.cockpit.GetComponentInChildren<Cockpit>(true);
            if (cockpit == null || cockpit.name != "cockpit_int") return;
            _aircraft = aircraft;
            _root = new GameObject("NOVR FS-20 integrated controls").transform; _root.SetParent(cockpit.transform, false);
            foreach (var node in aircraft.cockpit.GetComponentsInChildren<Transform>(true))
                if (node.name == "EjectionSeat") { _seat = node; break; }
            try
            {
                CreateVisuals();
                try { _nativeCover = new Fs20ControlCover(cockpit.GetComponent<MeshFilter>()); }
                catch (System.Exception e) { Debug.LogWarning("[NOVR] Native control cover skipped: " + e.Message); }
                _displayRoot = new GameObject("NOVR FS-20 native display positions").transform;
                _displayRoot.SetParent(transform, false);
                _hud = new CockpitPanelHud(_displayRoot, true);
            }
            catch (System.Exception e) { Release(); Debug.LogError("[NOVR] FS-20 cockpit setup failed: " + e); enabled = false; return; }
            Debug.Log("[NOVR] FS-20 physical controls ready: separate PREV/NEXT keys for each MFD, gear/brake/radar/AOTS, native seat handles. Left MFD defaults to MAP.");
        }
        if (_calibration != NOVRHeadsetData.TranslationCalibrationOffset || _anchor != NOVRHeadsetData.TranslationAnchor || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        { Cancel(); _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _anchor = NOVRHeadsetData.TranslationAnchor; _recenter = NOVRHeadsetData.RotationCalibrationOffset; }
        _nativeCover?.SetVisible(true);
        _ready = true; _root!.gameObject.SetActive(true); _displayRoot!.gameObject.SetActive(true);
        // Presentation remains fixed and visible while positioning or using
        // setup menus. Only interaction pauses; native floating HUD stays hidden.
        if (!TrackedControllers.Available || !GameManager.flightControlsEnabled || Time.timeScale <= 0 || WristMenu.IsOpen ||
            DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()))
        { Cancel(); _hint!.text = ""; _hud!.ClosePageMenus(); return; }
        _request.Update(aircraft.HasEjected(), Time.unscaledTime, Time.frameCount);
        var motion = MotionFlightController.Instance;
        var leftStick = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0; var hand = TrackedControllers.Read(left);
            var free = hand.Valid && !HelmetNightVision.OwnsHand(left) && CockpitLayout.HeldByHand(left) == CockpitLayout.Element.None &&
                !(left == leftStick ? motion?.StickHeld == true : motion?.ThrottleHeld == true);
            Direct(hand, free, left, i);
        }
        ShowHint();
    }
    private void Direct(ControllerSample hand, bool free, bool left, int side)
    {
        var available = free && !_request.BlocksInput && (_owner < 0 || _owner == side);
        var tracking = APIBus.MainCamera!.transform.parent;
        var world = PilotHandVisuals.TouchPoint(left, hand, tracking);
        var fingertip = PilotHandVisuals.TouchPoint(left, hand, tracking, true);
        _grips[side].Update(available, hand.Grip); _presses[side].Update(available, Mathf.Max(hand.Trigger, hand.Grip));
        string? near = null;
        Fs20PanelLayout.Control? target = null;
        float closest = float.MaxValue;
        var touched = false;
        foreach (var c in Fs20PanelLayout.Controls)
        {
            if (CockpitLayout.Active && c.Type != Fs20PanelLayout.Kind.Setup) continue;
            var touchPosition = c.Type == Fs20PanelLayout.Kind.Switch ? world : fingertip;
            var p = Quaternion.Inverse(c.Rotation) * (_root!.InverseTransformPoint(touchPosition) - c.Point);
            var inside = Mathf.Abs(p.x) < c.Size.x * .5f && Mathf.Abs(p.y) < c.Size.y * .5f;
            var depth = -p.z - (c.Type == Fs20PanelLayout.Kind.Switch ? 0 : .016f); var close = available && inside && depth > -.025f && depth < .085f;
            if (!_touch[side].TryGetValue(c.Id, out var gate)) _touch[side][c.Id] = gate = new CockpitTouchGate();
            var contact = gate.Update(available, inside, depth);
            if (close && Mathf.Abs(depth) < closest)
            { closest = Mathf.Abs(depth); near = c.Id; target = c; touched = contact; }
        }
        // The long striped handles are part of the native seat mesh. Match
        // their upper rails, not a guessed point on a replacement dashboard.
        if (available && _seat != null && !CockpitLayout.Active)
        {
            var p = _seat.InverseTransformPoint(world);
            for (var i = 0; i < 2; i++)
            {
                var rail = new Vector3(i == 0 ? -.258f : .258f, .086f, Mathf.Clamp(p.z, .25f, .42f));
                var distance = Vector3.Distance(p, rail);
                if (distance < .035f && distance < closest) { near = i == 0 ? "eject_left" : "eject_right"; target = null; closest = distance; }
            }
        }
        if (near != _near[side] && near != null) InteractionHaptics.Pulse(hand.Device, .16f, .025f, InteractionCue.Touch);
        _near[side] = near;
        if (_owner < 0 && near != null)
        {
            var seatHandle = near.StartsWith("eject_");
            var flick = target?.Type == Fs20PanelLayout.Kind.Switch;
            var press = seatHandle || flick ? _grips[side].Pressed : touched;
            if (press)
            {
                _active = near; _owner = side; _sent = false; _physicalPress = touched;
                _start = seatHandle ? _seat!.InverseTransformPoint(world) : _root!.InverseTransformPoint(world);
                InteractionHaptics.Pulse(hand.Device, .35f, .045f, seatHandle || flick ? InteractionCue.Grab : InteractionCue.Click);
                if (seatHandle)
                {
                    _pull.Begin(); _seatLift = 0;
                    _seatGrab = new Vector3(near == "eject_left" ? -.258f : .258f, .086f, Mathf.Clamp(_start.z,.25f,.42f));
                    _heldHandRotation = Quaternion.Euler(0, left ? 90 : -90, 0);
                }
                else if (flick) _heldHandRotation = Quaternion.Inverse(_actuators[near].rotation) * PilotHandVisuals.HandRotation(left,hand,tracking);
                else if (target?.Type != Fs20PanelLayout.Kind.Hold) { Activate(near, fingertip); _sent = true; }
            }
        }
        if (_owner != side) return;
        if (!available) { End(); return; }
        if (_active!.StartsWith("eject_"))
        {
            var d = _seat!.InverseTransformPoint(world) - _start;
            _seatLift = Mathf.Clamp(d.y,0,.045f);
            if (!_grips[side].Held || new Vector2(d.x, d.z).magnitude > .12f) { End(); return; }
            if (_pull.Update(true, d.y, new Vector2(d.x, d.z).magnitude))
            {
                ControllerActions.Instance?.QueuePanelAction("Eject"); _request.Begin(Time.unscaledTime, Time.frameCount);
                InteractionHaptics.Pulse(hand.Device, .65f, .09f, InteractionCue.Eject); End();
            }
            return;
        }
        Fs20PanelLayout.Control? active = null;
        foreach (var c in Fs20PanelLayout.Controls) if (c.Id == _active) { active = c; break; }
        if (active == null) { End(); return; }
        var held = active.Type == Fs20PanelLayout.Kind.Switch ? _grips[side].Held : _physicalPress ? near == _active && closest < .012f : _presses[side].Held;
        if (!held || Vector3.Distance(active.Type == Fs20PanelLayout.Kind.Switch ? world : fingertip, _root!.TransformPoint(active.Point)) > .18f)
        { InteractionHaptics.Pulse(hand.Device,.14f,.025f,InteractionCue.Release); End(); return; }
        if (active.Type == Fs20PanelLayout.Kind.Switch && !_sent)
        {
            var delta = Quaternion.Inverse(active.Rotation) * (_root.InverseTransformPoint(world) - _start);
            if (Mathf.Abs(delta.y) >= .012f)
            {
                var desired = delta.y > 0;
                // Gear UP means retract; radar/AOTS UP means on.
                var state = State(active.Id);
                if (state != desired) Activate(active.Id, world);
                _sent = true; InteractionHaptics.Pulse(hand.Device, .5f, .065f, InteractionCue.Switch);
            }
        }
    }
    private bool State(string id) => id switch
    {
        "gear" => !_aircraft!.gearDeployed,
        "radar" => _aircraft!.radar?.activated == true,
        "aots" => ModConfiguration.Instance.OpticalHud.Enabled.Value,
        "brake" => BrakeHeld || _aircraft!.GetInputs().brake > .05f, _ => false
    };
    private void Activate(string id, Vector3 world)
    {
        if (id.EndsWith("_lock")) CockpitLayout.Instance?.ToggleHandle(SideHandle(id == "left_lock"));
        else if (id.StartsWith("seat_"))
        {
            var delta = id switch { "seat_up" => Vector3.up, "seat_down" => Vector3.down, "seat_left" => Vector3.left,
                "seat_right" => Vector3.right, "seat_forward" => Vector3.forward, _ => Vector3.back };
            CockpitProfiles.Instance?.MoveSeat(delta*.02f); ModConfiguration.Instance.Config.Save();
        }
        else if (id == "map_pan") _hud!.ToggleSlave(true);
        else if (id.EndsWith("_prev") || id.EndsWith("_next"))
        {
            _hud!.Cycle(id.StartsWith("left_"), id.EndsWith("_prev") ? -1 : 1);
        }
        else if (id == "gear")
        {
            if (_aircraft!.radarAlt <= .2f) Feedback("GEAR LOCKED ON GROUND");
            else ControllerActions.Instance?.QueuePanelAction("Gear");
        }
        else if (id == "radar")
        {
            if (_aircraft!.radar == null) Feedback("RADAR NOT FITTED");
            else ControllerActions.Instance?.QueuePanelAction("Radar");
        }
        else if (id == "aots") OpticalTargetingHud.Toggle();
    }
    private void Feedback(string text) { _feedback = text; _feedbackUntil = Time.unscaledTime + 2; }
    private void ShowHint()
    {
        var id = _active ?? _near[0] ?? _near[1];
        _hint!.text = _request.BlocksInput ? "EJECT REQUESTED" : Time.unscaledTime < _feedbackUntil ? _feedback :
            id?.StartsWith("eject_") == true ? "GRIP + PULL UP TO EJECT" : "";
        _hint.transform.localPosition = id?.StartsWith("eject_") == true && _seat != null
            ? _root!.InverseTransformPoint(_seat.TransformPoint(new Vector3(id == "eject_left" ? -.258f : .258f, .14f, .33f)))
            : new Vector3(0,.157f,.470f);
    }
    private void LateUpdate()
    {
        if (!_ready) return;
        PlaceDisplays(); _hud!.SetVisible(true); _hud.Update();
        var config = ModConfiguration.Instance;
        foreach (var c in Fs20PanelLayout.Controls)
        {
            var hover = _near[0] == c.Id || _near[1] == c.Id; var active = _active == c.Id;
            var state = c.Id.EndsWith("_lock") ? CockpitLayout.Unlocked(SideHandle(c.Id == "left_lock")) : c.Id == "map_pan" ? _hud.LeftStickSlaved : State(c.Id); var actuator = _actuators[c.Id];
            var material = active || hover ? _hover : state ? _on : _cap;
            if (_faces[c.Id].sharedMaterial != material) _faces[c.Id].sharedMaterial = material;
            if (c.Type == Fs20PanelLayout.Kind.Switch)
                actuator.localRotation = Quaternion.Slerp(actuator.localRotation, Quaternion.Euler(state ? -25 : 25,0,0), FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, 20));
            else actuator.localPosition = new Vector3(0,0,active ? -.008f : -.011f);
            var text = c.Type == Fs20PanelLayout.Kind.Page
                ? c.Label
                : c.Id.EndsWith("_lock") ? (SideHandle(c.Id == "left_lock") == CockpitLayout.Element.Stick ? "STICK POS" : "THROTTLE POS") + "\n" + (CockpitLayout.Unlocked(SideHandle(c.Id == "left_lock")) ? "UNLOCKED" : "LOCKED")
                : c.Id.StartsWith("seat_") ? "SEAT\n" + c.Label
                : c.Id == "map_pan" ? "MAP PAN\n" + (_hud.LeftStickSlaved ? "ON" : "OFF")
                : c.Label + "\n" + (c.Id == "gear" ? (state ? "UP" : "DOWN") : c.Id == "brake" ? "HOLD" : state ? "ON" : "OFF");
            if (_labels[c.Id].text != text) _labels[c.Id].text = text;
        }
        for (var i = 0; i < 2; i++)
        {
            var title = (i == 0 ? "LEFT MFD · " : "RIGHT MFD · ") + MfdPages.ShortTitle(i == 0 ? config.Fs20LeftMfdPage.Value : config.Fs20RightMfdPage.Value);
            if (_mfdTitles[i]!.text != title) _mfdTitles[i]!.text = title;
        }
    }
    private void PlaceDisplays()
    {
        if (!_ready || _root == null || _displayRoot == null) return;
        var game = APIBus.MainCamera?.transform.parent; var ui = APIBus.CockpitHudCamera?.transform.parent;
        if (game == null || ui == null) return;
        _displayRoot.SetPositionAndRotation(ui.TransformPoint(game.InverseTransformPoint(_root.position)), ui.rotation * Quaternion.Inverse(game.rotation) * _root.rotation);
    }
    private void CreateVisuals()
    {
        _base = CockpitMaterials.Hardware("FS-20 recessed switch plate", new Color(.045f,.050f,.053f));
        _cap = CockpitMaterials.Hardware("FS-20 switch cap", new Color(.22f,.24f,.25f));
        _hover = CockpitMaterials.Hardware("FS-20 touched switch", new Color(.45f,.38f,.18f));
        _on = CockpitMaterials.Hardware("FS-20 active switch", new Color(.16f,.32f,.25f));
        _metal = CockpitMaterials.Hardware("FS-20 brushed switch hardware", new Color(.42f,.44f,.46f), .7f);
        var cockpitMaterial = _root!.parent.GetComponent<MeshRenderer>()?.sharedMaterial;
        CockpitMaterials.Fs20Finish(_base,cockpitMaterial,new Color(.55f,.55f,.55f));
        CockpitMaterials.Fs20Finish(_cap,cockpitMaterial,Color.white);
        CockpitMaterials.Fs20Finish(_hover,cockpitMaterial,new Color(1.3f,1.12f,.75f));
        CockpitMaterials.Fs20Finish(_on,cockpitMaterial,new Color(.8f,1.25f,.9f));
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _textMaterial = new Material(Shader.Find("UI/Default")); _textMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
        _textMaterial.color = Color.white; RefreshFont(_font); Font.textureRebuilt += RefreshFont;
        foreach (var c in Fs20PanelLayout.Controls)
        {
            var plate = new GameObject("FS-20 " + c.Label).transform; plate.SetParent(_root, false); plate.localPosition = c.Point; plate.localRotation = c.Rotation;
            Box(plate, Vector3.zero, new Vector3(c.Size.x,c.Size.y,.009f), _base);
            // Recessed corner fasteners and raised bezels seat these controls in
            // the existing instrument cluster instead of floating UI rectangles.
            foreach (var x in new[] { -1f, 1f })
                Cylinder(plate, new Vector3(x*(c.Size.x*.5f-.005f),0,-.005f), .005f, .002f, _metal);
            var actuator = new GameObject("Physical actuator").transform; actuator.SetParent(plate, false); _actuators[c.Id] = actuator;
            if (c.Type == Fs20PanelLayout.Kind.Switch)
            {
                actuator.localPosition = new Vector3(c.Size.x*.31f, 0, -.012f);
                Cylinder(plate, actuator.localPosition + Vector3.forward*.004f, .018f, .008f, _metal);
                Cylinder(actuator,new Vector3(0,0,-.009f),.007f,.026f,_metal);
                _faces[c.Id] = Box(actuator,new Vector3(0,0,-.022f),new Vector3(.020f,.024f,.012f),_cap);
                _labels[c.Id] = Label(plate,new Vector3(-.012f,0,-.005f),.0015f);
            }
            else
            {
                actuator.localPosition = new Vector3(0,0,-.011f);
                _faces[c.Id] = Box(actuator,Vector3.zero,new Vector3(c.Size.x-.008f,c.Size.y-.008f,.009f),_cap);
                _labels[c.Id] = Label(actuator,new Vector3(0,0,-.005f),c.Type == Fs20PanelLayout.Kind.Page ? .0016f : .0015f);
            }
        }
        for (var i = 0; i < 2; i++)
        {
            var nameplate = new GameObject("MFD page name").transform; nameplate.SetParent(_root,false);
            nameplate.localPosition = new Vector3(i == 0 ? -.110f : .110f,.389f,.515f); nameplate.localRotation = Fs20PanelLayout.Face;
            Box(nameplate,Vector3.zero,new Vector3(.164f,.018f,.005f),_base);
            _mfdTitles[i] = Label(nameplate,Vector3.back*.003f,.0016f);
        }
        _hint = Label(_root!,Vector3.zero,.0017f); _hint.transform.localRotation = Fs20PanelLayout.Face;
    }
    private TextMesh Label(Transform parent, Vector3 position, float size)
    {
        var go = new GameObject("White control label", typeof(TextMesh)); go.transform.SetParent(parent,false); go.transform.localPosition = position;
        go.layer = (int)LayerHelper.Layers.Cockpit; var text = go.GetComponent<TextMesh>();
        text.font = _font; text.fontSize = 48; text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = Color.white;
        go.GetComponent<MeshRenderer>().sharedMaterial = _textMaterial; return text;
    }
    private Renderer Box(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go=new GameObject("Machined control housing",typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.layer=(int)LayerHelper.Layers.Cockpit;
        var mesh=CockpitHardware.Keycap(scale);_hardwareMeshes.Add(mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return renderer;
    }
    private static Renderer Cylinder(Transform parent, Vector3 position, float diameter, float length, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale = new Vector3(diameter,length*.5f,diameter); go.layer = (int)LayerHelper.Layers.Cockpit;
        var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; return renderer;
    }
    private void RefreshFont(Font font)
    {
        if (font != _font || _textMaterial == null) return;
        var texture = font.material.mainTexture; _textMaterial.mainTexture = texture;
        _textMaterial.SetVector("_TextureSampleAdd", texture is Texture2D atlas && atlas.format == TextureFormat.Alpha8 ? new Vector4(1,1,1,0) : Vector4.zero);
    }
    private void End() { _active = null; _owner = -1; _seatLift = 0; _pull.Reset(); }
    private void Cancel()
    {
        End(); _request.Reset();
        for (var i = 0; i < 2; i++) { _grips[i].Reset(); _presses[i].Reset(); _near[i] = null; foreach (var gate in _touch[i].Values) gate.Reset(); }
    }
    private void Pause()
    {
        _nativeCover?.SetVisible(false);
        if (_ready) _hud?.Restore();
        _ready = false; Cancel(); if (_root != null) _root.gameObject.SetActive(false); if (_displayRoot != null) _displayRoot.gameObject.SetActive(false);
    }
    private void Release()
    {
        Pause(); _nativeCover?.Dispose(); _nativeCover = null; _hud?.Restore(); _hud = null; Font.textureRebuilt -= RefreshFont;
        if (_root != null) Destroy(_root.gameObject); if (_displayRoot != null) Destroy(_displayRoot.gameObject);
        foreach (var material in new[] { _base,_cap,_hover,_on,_metal,_textMaterial }) if (material != null) Destroy(material);
        foreach (var mesh in _hardwareMeshes) Destroy(mesh); _hardwareMeshes.Clear();
        _root = _displayRoot = _seat = null; _aircraft = null; _actuators.Clear(); _faces.Clear(); _labels.Clear(); foreach (var gates in _touch) gates.Clear();
    }
    private void OnDisable() { Application.onBeforeRender -= PlaceDisplays; Release(); }
    private void OnDestroy() { Release(); if (Instance == this) Instance = null; }
}
