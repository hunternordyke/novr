using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using NOVR.VrCamera;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-50)]
public sealed class MotionFlightController : MonoBehaviour
{
    internal static MotionFlightController? Instance { get; private set; }
    private readonly GripLatch _stickGrip = new();
    private readonly GripLatch _throttleGrip = new();
    private MotionControlVisuals? _visuals;
    private Aircraft? _aircraft;
    private Quaternion _neutralStick;
    private float _throttleStartPosition;
    private float _throttleStartValue;
    private float _calibrationHold;
    private bool _calibrationLatched;
    private Vector3 _lastCalibrationPosition;
    private Vector3 _lastTranslationAnchor;
    private Quaternion _lastCalibrationRotation;
    private bool _ready;
    private HandSample _stickHand;
    private HandSample _throttleHand;
    private MotionControlSettings Settings => ModConfiguration.Instance.MotionControls;

    internal bool StickHeld => _ready && _stickGrip.Held;
    internal bool ThrottleHeld => _ready && _throttleGrip.Held;
    internal float Pitch { get; private set; }
    internal float Roll { get; private set; }
    internal float Yaw { get; private set; }
    internal bool HasYaw => Settings.ThumbstickYaw.Value && _stickHand.HasAxis;
    internal float Throttle { get; private set; }
    private Vector3 StickPosition => HandedPosition(Settings.StickPosition.Value);
    private Vector3 ThrottlePosition => HandedPosition(Settings.ThrottlePosition.Value);

    private void Awake() => Instance = this;

    private void Update()
    {
        if (Keyboard.current?.f7Key.wasPressedThisFrame == true)
        {
            Settings.Enabled.Value = !Settings.Enabled.Value;
            ResetGrips();
            Debug.Log($"[NOVR] Motion flight controls {(Settings.Enabled.Value ? "enabled" : "disabled")}.");
        }
        Sample();
        if (!_ready) return;

        var placementGesture = _stickHand.Valid && _throttleHand.Valid &&
                               _stickHand.StickClick && _throttleHand.StickClick;
        if (placementGesture)
        {
            _calibrationHold += Time.unscaledDeltaTime;
            if (_calibrationHold >= 0.8f && !_calibrationLatched)
            {
                PlaceAtHands();
                _calibrationLatched = true;
            }
        }
        else
        {
            _calibrationHold = 0f;
            _calibrationLatched = false;
        }
        if (Keyboard.current?.f8Key.wasPressedThisFrame == true) PlaceAtHands();
    }

    private void LateUpdate()
    {
        if (!_ready || _visuals == null) return;
        _visuals.Update(StickPosition, ThrottlePosition, Settings.ThrottleTravel.Value,
            Settings.StickAngle.Value, Pitch, Roll, Throttle, StickHeld, ThrottleHeld,
            IsNearStick(), IsNearThrottle(), _stickHand.Valid, _stickHand.Position,
            _throttleHand.Valid, _throttleHand.Position);
    }

    // Called from both the render loop and the native pilot input hooks, so a physics tick
    // never relies on stale controller state just because Unity ran FixedUpdate first.
    internal bool SampleFor(Pilot pilot)
    {
        if (!isActiveAndEnabled)
        {
            Suspend();
            return false;
        }
        Sample();
        return _ready && pilot != null && !pilot.dead && pilot.playerControlled &&
               pilot.aircraft == _aircraft && pilot.currentState == pilot.playerState;
    }

    private void Sample()
    {
        if (!TryGetCockpit(out var aircraft, out var trackingRoot))
        {
            Suspend();
            return;
        }

        if (_aircraft != aircraft || _visuals == null || _visuals.Root == null || _visuals.Root.parent != trackingRoot)
        {
            _visuals?.Dispose();
            _aircraft = aircraft;
            _visuals = new MotionControlVisuals(trackingRoot);
            ResetGrips();
        }

        // NOVR's headset recentering changes the mapping from tracked hands into the cockpit.
        if (_lastCalibrationPosition != NOVRHeadsetData.TranslationCalibrationOffset ||
            _lastTranslationAnchor != NOVRHeadsetData.TranslationAnchor ||
            _lastCalibrationRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            ResetGrips();
            _lastCalibrationPosition = NOVRHeadsetData.TranslationCalibrationOffset;
            _lastTranslationAnchor = NOVRHeadsetData.TranslationAnchor;
            _lastCalibrationRotation = NOVRHeadsetData.RotationCalibrationOffset;
        }

        _ready = true;
        _visuals.Root.gameObject.SetActive(true);
        _stickHand = ReadHand(Settings.LeftHanded.Value ? XRNode.LeftHand : XRNode.RightHand);
        _throttleHand = ReadHand(Settings.LeftHanded.Value ? XRNode.RightHand : XRNode.LeftHand);
        if (!_throttleGrip.Held) Throttle = Mathf.Clamp01(aircraft.GetInputs().throttle);

        var stickWasHeld = _stickGrip.Held;
        _stickGrip.Update(_stickHand.Valid, _stickHand.Grip, IsNearStick());
        if (_stickGrip.Held && !stickWasHeld)
        {
            _neutralStick = _stickHand.Rotation;
            Pulse(_stickHand.Device);
        }

        Pitch = Roll = Yaw = 0f;
        if (_stickGrip.Held)
        {
            // Relative rotation in cockpit axes, independent of the controller's grip orientation.
            var tilt = (_stickHand.Rotation * Quaternion.Inverse(_neutralStick)) * Vector3.up;
            (Pitch, Roll) = FlightControlMath.Stick(tilt.x, tilt.y, tilt.z, Settings.StickAngle.Value,
                Settings.Deadzone.Value, Settings.ResponseExponent.Value);
            Yaw = FlightControlMath.Axis(_stickHand.Axis.x, 0.12f, 1f);
        }

        var throttleWasHeld = _throttleGrip.Held;
        _throttleGrip.Update(_throttleHand.Valid, _throttleHand.Grip, IsNearThrottle());
        if (_throttleGrip.Held && !throttleWasHeld)
        {
            _throttleStartPosition = _throttleHand.Position.z;
            _throttleStartValue = Throttle;
            Pulse(_throttleHand.Device);
        }
        if (_throttleGrip.Held)
            Throttle = FlightControlMath.Throttle(_throttleStartValue, _throttleStartPosition,
                _throttleHand.Position.z, Settings.ThrottleTravel.Value);
    }

    private bool TryGetCockpit(out Aircraft aircraft, out Transform trackingRoot)
    {
        aircraft = null!;
        trackingRoot = null!;
        if (!Settings.Enabled.Value || !XRSettings.enabled || !OpenXRUtility.IsSessionFocused ||
            !GameManager.flightControlsEnabled || Time.timeScale <= 0f || DynamicMap.mapMaximized ||
            RadialMenuMain.IsInUse() || (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            CameraStateManager.cameraMode != CameraMode.cockpit || !GameManager.GetLocalAircraft(out aircraft) ||
            aircraft.cockpit == null || aircraft.cockpit.IsDetached()) return false;

        var hasLocalPilot = false;
        foreach (var pilot in aircraft.pilots)
        {
            if (pilot != null && !pilot.dead && pilot.playerControlled && pilot.currentState == pilot.playerState)
            {
                hasLocalPilot = true;
                break;
            }
        }
        if (!hasLocalPilot) return false;

        var camera = APIBus.MainCamera;
        if (camera == null || camera.GetComponent<NOVRPoseDriver>() == null || camera.transform.parent == null)
            return false;
        var cameraState = SceneSingleton<CameraStateManager>.i;
        if (cameraState == null || cameraState.followingUnit != aircraft) return false;

        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked)
            return false;
        if (head.TryGetFeatureValue(CommonUsages.userPresence, out var present) && !present) return false;
        trackingRoot = camera.transform.parent;
        return true;
    }

    private bool IsNearStick() => _stickHand.Valid &&
        Vector3.Distance(_stickHand.Position, StickPosition) <= Settings.GrabRadius.Value;

    private bool IsNearThrottle() => _throttleHand.Valid && Vector3.Distance(_throttleHand.Position,
        ThrottlePosition + Vector3.forward * ((Throttle - 0.5f) * Settings.ThrottleTravel.Value)) <= Settings.GrabRadius.Value;

    private Vector3 HandedPosition(Vector3 position) => Settings.LeftHanded.Value
        ? new Vector3(-position.x, position.y, position.z) : position;

    private void PlaceAtHands()
    {
        if (!_stickHand.Valid || !_throttleHand.Valid) return;
        Settings.StickPosition.Value = HandedPosition(_stickHand.Position);
        Settings.ThrottlePosition.Value = HandedPosition(_throttleHand.Position -
            Vector3.forward * ((Throttle - 0.5f) * Settings.ThrottleTravel.Value));
        ModConfiguration.Instance.Config.Save();
        ResetGrips();
        Pulse(_stickHand.Device);
        Pulse(_throttleHand.Device);
        Debug.Log("[NOVR] Virtual stick and throttle placed at hands. Release grips before grabbing.");
    }

    internal void ResetGrips()
    {
        _stickGrip.Reset();
        _throttleGrip.Reset();
        Pitch = Roll = Yaw = 0f;
    }

    private void Suspend()
    {
        ResetGrips();
        _ready = false;
        _calibrationHold = 0f;
        _calibrationLatched = false;
        if (_visuals?.Root != null) _visuals.Root.gameObject.SetActive(false);
    }

    private void OnDisable() => Suspend();

    private void OnDestroy()
    {
        Suspend();
        _visuals?.Dispose();
        if (Instance == this) Instance = null;
    }

    private static void Pulse(UnityEngine.XR.InputDevice device)
    {
        if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            device.SendHapticImpulse(0, 0.25f, 0.035f);
    }

    private static HandSample ReadHand(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked ||
            !device.TryGetFeatureValue(CommonUsages.trackingState, out var tracking) ||
            (tracking & (InputTrackingState.Position | InputTrackingState.Rotation)) !=
            (InputTrackingState.Position | InputTrackingState.Rotation) ||
            !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
            !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation) ||
            !Finite(position) || !Finite(new Vector3(rotation.x, rotation.y, rotation.z)) ||
            !FlightControlMath.IsFinite(rotation.w) || Quaternion.Dot(rotation, rotation) < 0.5f) return default;

        if (!device.TryGetFeatureValue(CommonUsages.grip, out var grip))
        {
            if (!device.TryGetFeatureValue(CommonUsages.gripButton, out var pressed)) return default;
            grip = pressed ? 1f : 0f;
        }
        var hasAxis = device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis);
        device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var stickClick);
        return new HandSample
        {
            Valid = FlightControlMath.IsFinite(grip), Device = device, Grip = grip, Axis = axis,
            HasAxis = hasAxis, StickClick = stickClick,
            Position = NOVRHeadsetData.TranslationAnchor + NOVRHeadsetData.TranslationCalibrationOffset + position,
            Rotation = NOVRHeadsetData.RotationCalibrationOffset * rotation
        };
    }

    private static bool Finite(Vector3 value) => FlightControlMath.IsFinite(value.x) &&
        FlightControlMath.IsFinite(value.y) && FlightControlMath.IsFinite(value.z);

    private struct HandSample
    {
        public bool Valid, HasAxis, StickClick;
        public UnityEngine.XR.InputDevice Device;
        public float Grip;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Axis;
    }
}
