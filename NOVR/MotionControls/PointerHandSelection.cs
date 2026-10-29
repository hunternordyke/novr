namespace NOVR.MotionControls;

// Either hand may acquire the pointer. A held interaction (including its release
// frame) stays with its original hand; held buttons cannot become fresh clicks.
internal sealed class PointerHandSelection
{
    private readonly ControllerButtonGate _left = new(), _right = new();
    private bool _initialized;
    internal bool Left { get; private set; }
    internal ControllerButtonGate Trigger => Left ? _left : _right;

    internal void Update(bool preferLeft, bool leftAvailable, float leftTrigger, bool leftTarget,
        bool rightAvailable, float rightTrigger, bool rightTarget)
    {
        _left.Update(leftAvailable, leftTrigger);
        _right.Update(rightAvailable, rightTrigger);
        if (!_initialized) { Left = preferLeft; _initialized = true; }
        if (Trigger.Held || Trigger.Released) return;
        var other = Left ? _right : _left;
        var available = Left ? leftAvailable : rightAvailable;
        var target = Left ? leftTarget : rightTarget;
        var otherAvailable = Left ? rightAvailable : leftAvailable;
        var otherTarget = Left ? rightTarget : leftTarget;
        if (otherAvailable && (other.Pressed && otherTarget ||
            !other.Held && (!available || !target && otherTarget))) Left = !Left;
    }

    internal void Reset()
    {
        _left.Reset(); _right.Reset(); _initialized = false;
    }
}
