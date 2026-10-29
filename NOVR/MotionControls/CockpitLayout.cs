using UnityEngine;
using NOVR.VrUi;

namespace NOVR.MotionControls;

// Layout edits remain previews until explicitly saved. Tracking/menu transitions
// release the grab, and never turn an already held grip into a new selection.
[DefaultExecutionOrder(-1250)]
internal sealed class CockpitLayout : MonoBehaviour
{
    internal enum Element { None, Stick, Throttle, Panel }
    private sealed class HandDrag
    {
        internal readonly ControllerButtonGate Gate = new();
        internal Element Element, Hover;
        internal Vector3 StartHand, StartPosition, Offset;
        internal Quaternion RotationOffset;
    }
    internal static CockpitLayout? Instance { get; private set; }
    internal static bool Active => Instance?._active == true;
    private bool _active, _leftHanded;
    private Aircraft? _aircraft;
    private Vector3 _stick, _throttle, _panel, _calibration, _anchor;
    private Quaternion _panelRotation, _recenterRotation;
    private readonly HandDrag[] _hands = { new(), new() };
    private readonly LineRenderer?[] _rays = new LineRenderer?[2];
    private Material? _rayMaterial;
    internal static Vector3? StickPreview => Active ? Instance!._stick : null;
    internal static Vector3? ThrottlePreview => Active ? Instance!._throttle : null;
    internal static Vector3 PanelPosition => Active ? Instance!._panel : ModConfiguration.Instance.CockpitPanelPosition.Value;
    internal static Quaternion PanelRotation => Active ? Instance!._panelRotation : Quaternion.Euler(
        ModConfiguration.Instance.CockpitPanelTilt.Value, ModConfiguration.Instance.CockpitPanelYaw.Value, ModConfiguration.Instance.CockpitPanelRoll.Value);
    internal static string Hint => "EDIT LAYOUT  •  GRIP A HANDLE OR PANEL TO MOVE  •  VIEW → SAVE LAYOUT";
    internal static bool Held(Element element) => Active && (Instance!._hands[0].Element == element || Instance._hands[1].Element == element);
    internal static bool Hovered(Element element) => Active && (Instance!._hands[0].Hover == element || Instance._hands[1].Hover == element);
    private Vector3 Mirror(Vector3 p) => _leftHanded ? new Vector3(-p.x, p.y, p.z) : p;
    private void Awake() => Instance = this;

    internal void Begin()
    {
        if (_active || !GameManager.GetLocalAircraft(out var aircraft) || CameraStateManager.cameraMode != CameraMode.cockpit) return;
        CockpitProfiles.Instance?.Refresh();
        _stick = CockpitProfiles.StickPosition; _throttle = CockpitProfiles.ThrottlePosition;
        _panel = BoundPanel(PanelPosition); _panelRotation = PanelRotation;
        _aircraft = aircraft; _leftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _anchor = NOVRHeadsetData.TranslationAnchor;
        _recenterRotation = NOVRHeadsetData.RotationCalibrationOffset;
        ModConfiguration.Instance.MotionControls.Enabled.Value = true;
        ModConfiguration.Instance.CockpitPanelEnabled.Value = true;
        MotionFlightController.Instance?.CancelPlacement();
        ControllerActions.Instance?.ResetButtons();
        _active = true; Release(); WristMenu.Close();
    }

    internal void Finish(bool save)
    {
        if (!_active) return;
        var sameAircraft = GameManager.GetLocalAircraft(out var aircraft) && aircraft == _aircraft &&
            _leftHanded == ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        _active = false; Release();
        if (save && sameAircraft)
        {
            CockpitProfiles.Instance?.SaveHandles(_stick, _throttle);
            var settings = ModConfiguration.Instance;
            settings.CockpitPanelPosition.Value = _panel;
            var angles = _panelRotation.eulerAngles;
            settings.CockpitPanelTilt.Value = Mathf.DeltaAngle(0, angles.x);
            settings.CockpitPanelYaw.Value = Mathf.DeltaAngle(0, angles.y);
            settings.CockpitPanelRoll.Value = Mathf.DeltaAngle(0, angles.z);
            settings.Config.Save();
        }
        MotionFlightController.Instance?.ResetGrips(); ControllerActions.Instance?.ResetButtons();
    }

    private void Update()
    {
        if (!_active) return;
        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft != _aircraft || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || CameraStateManager.cameraMode != CameraMode.cockpit ||
            _leftHanded != ModConfiguration.Instance.MotionControls.LeftHanded.Value)
        { Finish(false); return; }
        if (_calibration != NOVRHeadsetData.TranslationCalibrationOffset || _anchor != NOVRHeadsetData.TranslationAnchor ||
            _recenterRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Release(); _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
            _anchor = NOVRHeadsetData.TranslationAnchor; _recenterRotation = NOVRHeadsetData.RotationCalibrationOffset;
        }
        if (!TrackedControllers.Available || WristMenu.IsOpen || Time.timeScale <= 0 || DynamicMap.mapMaximized ||
            !GameManager.flightControlsEnabled || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen)) { Release(); return; }
        for (var i = 0; i < 2; i++) MoveHand(i, TrackedControllers.Read(i == 0));
    }

    private Vector3 GripPosition(Element element)
    {
        if (element == Element.Stick) return Mirror(_stick);
        if (element == Element.Panel) return _panel;
        var offset = FlightControlMath.ThrottleOffset(MotionFlightController.UsesLiftCollective(_aircraft),
            MotionFlightController.Instance?.Throttle ?? .5f, ModConfiguration.Instance.MotionControls.ThrottleTravel.Value);
        return Mirror(_throttle) + new Vector3(0, offset.height, offset.forward);
    }

    private void MoveHand(int index, ControllerSample hand)
    {
        var drag = _hands[index];
        drag.Gate.Update(hand.Valid, hand.Grip);
        if (!drag.Gate.Held) drag.Element = Element.None;
        var ray = new Ray(hand.AimPosition, hand.AimRotation * Vector3.forward);
        drag.Hover = hand.Valid ? Select(hand.Position, ray, index, out _) : Element.None;
        if (drag.Gate.Pressed && drag.Hover != Element.None)
        {
            drag.Element = drag.Hover; drag.StartHand = hand.Position;
            drag.StartPosition = GripPosition(drag.Element);
            drag.Offset = Quaternion.Inverse(hand.Rotation) * (_panel - hand.Position);
            drag.RotationOffset = Quaternion.Inverse(hand.Rotation) * _panelRotation;
            Pulse(hand);
        }
        if (drag.Element != Element.None && hand.Valid && drag.Gate.Held)
        {
            var delta = hand.Position - drag.StartHand;
            if (drag.Element == Element.Panel)
            {
                _panel = BoundPanel(hand.Position + hand.Rotation * drag.Offset);
                _panelRotation = hand.Rotation * drag.RotationOffset;
            }
            else
            {
                var grip = drag.StartPosition + delta;
                if (drag.Element == Element.Stick) _stick = Mirror(BoundHandle(grip));
                else _throttle = Mirror(BoundHandle(grip - (GripPosition(Element.Throttle) - Mirror(_throttle))));
            }
        }
        DrawRay(index, hand, drag.Element != Element.None ? drag.Element : drag.Hover);
    }

    private Element Select(Vector3 hand, Ray ray, int index, out float distance)
    {
        var best = Element.None; distance = 2.5f;
        // Reach takes precedence over pointing. Either hand may move any item.
        foreach (var element in new[] { Element.Stick, Element.Throttle, Element.Panel })
        {
            if (_hands[1 - index].Element == element) continue;
            var d = element == Element.Panel ? PanelDistance(hand) : Vector3.Distance(hand, GripPosition(element));
            if (d < (element == Element.Panel ? .065f : ModConfiguration.Instance.MotionControls.GrabRadius.Value) && d < distance)
            { best = element; distance = d; }
        }
        if (best != Element.None) return best;
        foreach (var element in new[] { Element.Stick, Element.Throttle, Element.Panel })
        {
            if (_hands[1 - index].Element == element) continue;
            // In edit mode the panel is transparent. A ray aimed at a handle
            // behind it must still select the handle, rather than its outline.
            if (element == Element.Panel && best != Element.None) return best;
            float along;
            if (element == Element.Panel)
            {
                var rotation = Quaternion.Inverse(_panelRotation);
                var origin = rotation * (ray.origin - _panel); var direction = rotation * ray.direction;
                var size = ModConfiguration.Instance.CockpitPanelScale.Value;
                if (!PointerGeometry.IntersectRect(origin.x, origin.y, origin.z + .05f * size,
                    direction.x, direction.y, direction.z, -.385f * size, -.18f * size, .385f * size, .18f * size, out along)) continue;
            }
            else
            {
                var center = GripPosition(element); along = Vector3.Dot(center - ray.origin, ray.direction);
                if (Vector3.Distance(ray.GetPoint(along), center) > .09f) continue;
            }
            if (along > .01f && along < distance) { best = element; distance = along; }
        }
        return best;
    }

    private float PanelDistance(Vector3 hand)
    {
        var p = Quaternion.Inverse(_panelRotation) * (hand - _panel);
        var scale = ModConfiguration.Instance.CockpitPanelScale.Value;
        var closest = new Vector3(Mathf.Clamp(p.x, -.385f * scale, .385f * scale), Mathf.Clamp(p.y, -.18f * scale, .18f * scale), -.05f * scale);
        return Vector3.Distance(p, closest);
    }
    internal static Vector3 BoundPanel(Vector3 p) => !TrackedControllers.Finite(p) ? new Vector3(0, -.32f, .65f) :
        new Vector3(Mathf.Clamp(p.x, -1.2f, 1.2f), Mathf.Clamp(p.y, -1.2f, .65f), Mathf.Clamp(p.z, .15f, 1.5f));
    private static Vector3 BoundHandle(Vector3 p) => new(Mathf.Clamp(p.x, -1.2f, 1.2f), Mathf.Clamp(p.y, -1.25f, .7f), Mathf.Clamp(p.z, -.35f, 1.5f));
    private void DrawRay(int index, ControllerSample hand, Element element)
    {
        if (!hand.Valid || element == Element.None) { if (_rays[index] != null) _rays[index]!.enabled = false; return; }
        var root = APIBus.CockpitHudCamera?.transform.parent;
        if (root == null) return;
        if (_rays[index] == null)
        {
            _rayMaterial ??= CockpitMaterials.Opaque("NOVR layout pointer", new Color(.3f, 1f, .8f));
            var go = new GameObject("Layout pointer", typeof(LineRenderer)); go.transform.SetParent(transform, false);
            var line = _rays[index] = go.GetComponent<LineRenderer>(); line.sharedMaterial = _rayMaterial;
            line.positionCount = 2; line.startWidth = .0015f; line.endWidth = .001f;
            go.layer = (int)LayerHelper.GetVrUiLayer();
        }
        _rays[index]!.enabled = true;
        _rays[index]!.SetPosition(0, root.TransformPoint(hand.AimPosition + CockpitProfiles.SeatOffset));
        _rays[index]!.SetPosition(1, root.TransformPoint(GripPosition(element) + CockpitProfiles.SeatOffset));
    }
    private void Release()
    {
        foreach (var hand in _hands) { hand.Gate.Reset(); hand.Element = hand.Hover = Element.None; }
        foreach (var ray in _rays) if (ray != null) ray.enabled = false;
    }
    private static void Pulse(ControllerSample hand)
    { if (hand.Device.isValid && hand.Device.TryGetHapticCapabilities(out var c) && c.supportsImpulse) hand.Device.SendHapticImpulse(0, .2f, .035f); }
    private void OnDisable() => Finish(false);
    private void OnDestroy()
    {
        Finish(false); foreach (var ray in _rays) if (ray != null) Destroy(ray.gameObject);
        if (_rayMaterial != null) Destroy(_rayMaterial); if (Instance == this) Instance = null;
    }
}
