using NOVR.MotionControls;

var passed = 0;
void Check(string name, Action test)
{
    test();
    Console.WriteLine($"PASS {name}");
    passed++;
}
void True(bool condition) { if (!condition) throw new Exception("Expected true"); }
void Near(float actual, float expected, float tolerance = 0.00001f)
{
    if (!float.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
        throw new Exception($"Expected {expected}, got {actual}");
}
GripLatch Grab()
{
    var grip = new GripLatch();
    grip.Update(true, 0, true);
    True(grip.Update(true, 1, true));
    return grip;
}

Check("held grip at startup cannot grab until released", () =>
{
    var grip = new GripLatch();
    True(!grip.Update(true, 1, true));
    True(!grip.Update(true, 0, true));
    True(grip.Update(true, 1, true));
});
Check("pressing away then moving into a handle does not grab", () =>
{
    var grip = new GripLatch();
    grip.Update(true, 0, false);
    True(!grip.Update(true, 1, false));
    True(!grip.Update(true, 1, true));
    grip.Update(true, 0, true);
    True(grip.Update(true, 1, true));
});
Check("grip hysteresis avoids repeated grab/release at threshold", () =>
{
    var grip = Grab();
    True(grip.Update(true, 0.5f, true));
    True(!grip.Update(true, 0.35f, true));
    True(!grip.Update(true, 0.5f, true));
    True(grip.Update(true, 0.65f, true));
});
Check("moving beyond grab radius while holding retains control", () =>
    True(Grab().Update(true, 1, false)));
Check("tracking loss releases and requires a new grip edge", () =>
{
    var grip = Grab();
    True(!grip.Update(false, 1, true));
    True(!grip.Update(true, 1, true));
    grip.Update(true, 0, true);
    True(grip.Update(true, 1, true));
});
Check("pause/recenter reset cannot automatically regrab", () =>
{
    var grip = Grab();
    grip.Reset();
    True(!grip.Update(true, 1, true));
});
Check("non-finite grip input releases and disarms", () =>
{
    foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        var grip = Grab();
        True(!grip.Update(true, value, true));
        True(!grip.Update(true, 1, true));
    }
});
Check("stick and throttle release independently", () =>
{
    var stick = Grab();
    var throttle = Grab();
    stick.Update(false, 1, false);
    True(!stick.Held && throttle.Held);
});
Check("deadzone suppresses small movements", () =>
{
    Near(FlightControlMath.Axis(0.05f, 0.06f, 1.5f), 0);
    Near(FlightControlMath.Axis(-0.05f, 0.06f, 1.5f), 0);
});
Check("axis range clamps symmetrically", () =>
{
    Near(FlightControlMath.Axis(5, 0.06f, 1.5f), 1);
    Near(FlightControlMath.Axis(-5, 0.06f, 1.5f), -1);
});
Check("response curve is bounded, symmetric and monotonic", () =>
{
    float previous = 0;
    for (int i = 0; i <= 1000; i++)
    {
        var input = i / 1000f;
        var output = FlightControlMath.Axis(input, 0.06f, 1.5f);
        True(output >= previous && output <= 1);
        Near(output, -FlightControlMath.Axis(-input, 0.06f, 1.5f));
        previous = output;
    }
});
Check("axis rejects invalid tracking values", () =>
{
    Near(FlightControlMath.Axis(float.NaN, 0.06f, 1.5f), 0);
    Near(FlightControlMath.Axis(float.PositiveInfinity, 0.06f, 1.5f), 0);
    True(float.IsFinite(FlightControlMath.Axis(0.5f, float.NaN, float.NaN)));
});
Check("higher response exponent reduces center sensitivity", () =>
    True(FlightControlMath.Axis(0.5f, 0.06f, 2) < FlightControlMath.Axis(0.5f, 0.06f, 1)));
Check("pulling the stick commands nose up, pushing commands nose down", () =>
{
    var sine = (float)Math.Sin(25 * Math.PI / 180);
    var cosine = (float)Math.Cos(25 * Math.PI / 180);
    Near(FlightControlMath.Stick(0, cosine, -sine, 25, 0.06f, 1.5f).pitch, 1);
    Near(FlightControlMath.Stick(0, cosine, sine, 25, 0.06f, 1.5f).pitch, -1);
});
Check("leaning stick right commands right roll without pitch", () =>
{
    var axes = FlightControlMath.Stick((float)Math.Sin(25 * Math.PI / 180),
        (float)Math.Cos(25 * Math.PI / 180), 0, 25, 0.06f, 1.5f);
    Near(axes.roll, 1);
    Near(axes.pitch, 0);
});
Check("neutral or invalid stick poses cannot deflect controls", () =>
{
    var neutral = FlightControlMath.Stick(0, 1, 0, 25, 0.06f, 1.5f);
    Near(neutral.pitch, 0);
    Near(neutral.roll, 0);
    var invalid = FlightControlMath.Stick(float.NaN, 1, 0, 25, 0.06f, 1.5f);
    Near(invalid.pitch, 0);
    Near(invalid.roll, 0);
});
Check("grabbing throttle preserves current thrust", () =>
    Near(FlightControlMath.Throttle(0.73f, 1.2f, 1.2f, 0.25f), 0.73f));
Check("forward throttle movement increases thrust proportionately", () =>
    Near(FlightControlMath.Throttle(0.3f, 0.2f, 0.3f, 0.25f), 0.7f));
Check("throttle clamps at both physical travel limits", () =>
{
    Near(FlightControlMath.Throttle(0.5f, 0, 10, 0.25f), 1);
    Near(FlightControlMath.Throttle(0.5f, 0, -10, 0.25f), 0);
});
Check("regrabbing throttle at another location causes no jump", () =>
{
    var first = FlightControlMath.Throttle(0.3f, 0.2f, 0.3f, 0.25f);
    Near(FlightControlMath.Throttle(first, -0.3f, -0.3f, 0.25f), first);
});
Check("invalid throttle samples or zero travel retain thrust", () =>
{
    Near(FlightControlMath.Throttle(0.6f, 0, float.NaN, 0.25f), 0.6f);
    Near(FlightControlMath.Throttle(0.6f, 0, 1, 0), 0.6f);
    Near(FlightControlMath.Throttle(0.6f, 0, 1, float.PositiveInfinity), 0.6f);
});
Check("native throttle handoff preserves every binding/collective convention", () =>
{
    foreach (var negativeRange in new[] { false, true })
    foreach (var invertedCollective in new[] { false, true })
    for (int i = 0; i <= 100; i++)
    {
        var thrust = i / 100f;
        var native = FlightControlMath.ToSimulatedThrottle(thrust, negativeRange, invertedCollective);
        var restored = negativeRange ? 0.5f * (native + 1f) : native;
        if (invertedCollective) restored = 1f - restored;
        Near(restored, thrust);
    }
});
Check("HUD smoothing follows identically at 72, 90, and 120 Hz", () =>
{
    float Simulate(int frames)
    {
        float remaining = 1;
        for (int i = 0; i < frames; i++) remaining *= 1 - FlightControlMath.SmoothingWeight(1f / frames, 10);
        return remaining;
    }
    Near(Simulate(72), Simulate(90));
    Near(Simulate(90), Simulate(120));
    Near(Simulate(90), (float)Math.Exp(-10));
});
Check("HUD smoothing remains bounded after a frame stall", () =>
{
    var weight = FlightControlMath.SmoothingWeight(5, 10);
    True(weight >= 0 && weight <= 1);
    Near(FlightControlMath.SmoothingWeight(0, 10), 0);
});

if (args.Length == 2)
    Check("Harmony patch targets and injected fields match the installed game", () =>
        PatchCompatibility.Verify(args[0], args[1]));

Console.WriteLine($"{passed} checks passed.");
