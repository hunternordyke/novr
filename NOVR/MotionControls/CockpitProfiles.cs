using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.MotionControls;

// Profile coordinates are relative to the recentered play space. Seat translation
// moves the headset and both hands together, preserving their relative geometry.
[DefaultExecutionOrder(-1300)]
internal sealed class CockpitProfiles : MonoBehaviour
{
    private sealed class Profile
    {
        public ConfigEntry<Vector3> Seat = null!, Stick = null!, Throttle = null!;
    }
    private readonly Dictionary<string, Profile> _profiles = new();
    private Profile? _current;
    private string? _key;
    internal static CockpitProfiles? Instance { get; private set; }
    internal static Vector3 SeatOffset => Instance?._current?.Seat.Value ?? Vector3.zero;
    internal static Vector3 StickPosition => Instance?._current?.Stick.Value ?? ModConfiguration.Instance.MotionControls.StickPosition.Value;
    internal static Vector3 ThrottlePosition => Instance?._current?.Throttle.Value ?? ModConfiguration.Instance.MotionControls.ThrottlePosition.Value;
    internal string AircraftName { get; private set; } = "No aircraft";

    private void Awake() => Instance = this;
    private void Update() => Refresh();
    internal void Refresh()
    {
        if (CameraStateManager.cameraMode != CameraMode.cockpit || !GameManager.GetLocalAircraft(out var aircraft) || aircraft.definition == null)
        {
            _current = null;
            _key = null;
            AircraftName = "No aircraft";
            return;
        }
        var identity = aircraft.definition.jsonKey;
        if (string.IsNullOrEmpty(identity)) identity = aircraft.definition.name;
        // Separate mirrored layouts so changing handedness does not overwrite the other setup.
        identity += ModConfiguration.Instance.MotionControls.LeftHanded.Value ? ":left" : ":right";
        if (_key == identity) return;
        _key = identity;
        AircraftName = aircraft.definition.unitName;
        if (!_profiles.TryGetValue(identity, out _current))
        {
            using var hash = SHA256.Create();
            var section = "Cockpit " + System.BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 16);
            var config = ModConfiguration.Instance.Config;
            var settings = ModConfiguration.Instance.MotionControls;
            _current = new Profile
            {
                Seat = config.Bind(section, "Seat Offset", Vector3.zero, "Seat translation in meters for " + identity),
                Stick = config.Bind(section, "Stick Position", settings.StickPosition.Value, "Saved stick placement for " + identity),
                Throttle = config.Bind(section, "Throttle Position", settings.ThrottlePosition.Value, "Saved throttle placement for " + identity)
            };
            _current.Seat.Value = BoundSeat(_current.Seat.Value);
            if (!TrackedControllers.Finite(_current.Stick.Value)) _current.Stick.Value = (Vector3)settings.StickPosition.DefaultValue;
            if (!TrackedControllers.Finite(_current.Throttle.Value)) _current.Throttle.Value = (Vector3)settings.ThrottlePosition.DefaultValue;
            _profiles.Add(identity, _current);
        }
        MotionFlightController.Instance?.ResetGrips();
    }

    internal void SaveHandles(Vector3 stick, Vector3 throttle)
    {
        Refresh();
        if (_current == null) return;
        _current.Stick.Value = stick;
        _current.Throttle.Value = throttle;
        ModConfiguration.Instance.Config.Save();
    }

    internal void MoveSeat(Vector3 delta)
    {
        Refresh();
        if (_current == null) return;
        _current.Seat.Value = BoundSeat(_current.Seat.Value + delta);
        MotionFlightController.Instance?.ResetGrips();
    }

    internal void ResetProfile()
    {
        if (_current == null) return;
        _current.Seat.Value = Vector3.zero;
        _current.Stick.Value = ModConfiguration.Instance.MotionControls.StickPosition.Value;
        _current.Throttle.Value = ModConfiguration.Instance.MotionControls.ThrottlePosition.Value;
        MotionFlightController.Instance?.ResetGrips();
    }

    private static Vector3 BoundSeat(Vector3 value) => !TrackedControllers.Finite(value) ? Vector3.zero :
        new Vector3(Mathf.Clamp(value.x, -0.5f, 0.5f), Mathf.Clamp(value.y, -0.5f, 0.5f), Mathf.Clamp(value.z, -0.5f, 0.5f));
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
