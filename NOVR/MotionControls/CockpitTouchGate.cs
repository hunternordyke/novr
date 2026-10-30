namespace NOVR.MotionControls;

// A hand must approach from in front. Starting inside a button, teleporting,
// recovering tracking or leaving a menu cannot produce a phantom touch.
internal sealed class CockpitTouchGate
{
    private bool _initialized, _armed;
    internal bool Update(bool available, bool withinFace, float depth)
    {
        if (!available || !FlightControlMath.IsFinite(depth)) { Reset(); return false; }
        if (!_initialized) { _initialized = true; return false; }
        if (depth > .015f) _armed = true;
        if (!withinFace || depth > .004f || depth < -.025f) return false;
        var pressed = _armed; _armed = false; return pressed;
    }
    internal void Reset() { _initialized = _armed = false; }
}

internal sealed class SeatHandlePull
{
    private bool _held;
    internal void Begin() => _held = true;
    internal bool Update(bool held, float upward, float sideways)
    {
        if (!held || !FlightControlMath.IsFinite(upward) || !FlightControlMath.IsFinite(sideways) || sideways > .12f)
        { Reset(); return false; }
        if (!_held || upward < .045f) return false;
        Reset(); return true;
    }
    internal void Reset() => _held = false;
}
