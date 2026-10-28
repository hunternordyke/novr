using NOVR.MotionControls;

var passed = 0;
var payloadTestDir = Path.Combine(Path.GetTempPath(), "novr-payload-" + Guid.NewGuid());
Directory.CreateDirectory(payloadTestDir);
try
{
    var source = Path.Combine(payloadTestDir, "source.dll");
    var destination = Path.Combine(payloadTestDir, "installed.dll");
    var bytes = Enumerable.Range(0, 17003).Select(i => (byte)(i % 251)).ToArray();
    File.WriteAllBytes(source, bytes);
    Check("runtime payload installs missing files", () =>
    {
        True(PayloadFile.CopyIfChanged(source, destination));
        True(File.ReadAllBytes(destination).SequenceEqual(bytes));
    });
    Check("repeat installation leaves an identical locked assembly untouched", () =>
    {
        var stamp = File.GetLastWriteTimeUtc(destination);
        using var mappedAssembly = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        True(!PayloadFile.CopyIfChanged(source, destination));
        True(File.GetLastWriteTimeUtc(destination) == stamp);
    });
    Check("runtime payload replaces a changed file even when sizes match", () =>
    {
        bytes[9000] ^= 0xff;
        File.WriteAllBytes(source, bytes);
        True(PayloadFile.CopyIfChanged(source, destination));
        True(File.ReadAllBytes(destination).SequenceEqual(bytes));
    });
}
finally { Directory.Delete(payloadTestDir, true); }
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

Check("a held flight trigger cannot turn into a menu click", () =>
{
    var flight = new ControllerButtonGate();
    var menu = new ControllerButtonGate();
    flight.Update(true, 0);
    flight.Update(true, 1);
    True(flight.Held && flight.Pressed);
    flight.Update(false, 1);
    menu.Update(true, 1);
    True(!flight.Held && !flight.Released && !menu.Pressed && !menu.Held);
    menu.Update(true, 0);
    menu.Update(true, 1);
    True(menu.Pressed);
});
Check("closing a menu with trigger held cannot start firing", () =>
{
    var flight = new ControllerButtonGate();
    flight.Update(false, 1);
    flight.Update(true, 1);
    True(!flight.Held && !flight.Pressed);
    flight.Update(true, 0);
    flight.Update(true, 1);
    True(flight.Held && flight.Pressed);
});
Check("tracking loss cancels release actions instead of clicking", () =>
{
    var button = new ControllerButtonGate();
    button.Update(true, 0);
    button.Update(true, 1);
    button.Update(false, 0);
    True(!button.Released && !button.Held);
    button.Update(true, 1);
    True(!button.Pressed);
});
Check("non-finite trigger data never produces a release action", () =>
{
    var button = new ControllerButtonGate();
    button.Update(true, 0);
    button.Update(true, 1);
    button.Update(true, float.NaN);
    True(!button.Released && !button.Held);
});
Check("controller taps create exactly one press and one release", () =>
{
    var button = new ControllerButtonGate();
    button.Update(true, 0);
    button.Update(true, 1);
    True(button.Pressed);
    button.Update(true, 1);
    True(button.Held && !button.Pressed && !button.Released);
    button.Update(true, 0);
    True(button.Released && !button.Pressed);
    button.Update(true, 0);
    True(!button.Released);
});
Check("hand-origin ray hits the surface without centre-eye parallax", () =>
{
    True(PointerGeometry.IntersectRect(0.3f, 0, -2, -0.1f, 0, 1, -0.2f, -0.2f, 0.2f, 0.2f, out var distance));
    Near(distance, 2);
    Near(0.3f - 0.1f * distance, 0.1f);
    // A centre-eye ray with the same direction would point at a different location.
    True(Math.Abs(-0.1f * distance - 0.1f) > 0.2f);
});
Check("surface distances survive scaled canvas coordinates", () =>
{
    True(PointerGeometry.IntersectRect(300, 0, -2000, -100, 0, 1000, -200, -200, 200, 200, out var distance));
    Near(distance, 2);
});
Check("parallel, rearward, non-finite and off-panel rays do not click", () =>
{
    True(!PointerGeometry.IntersectRect(0, 0, -2, 1, 0, 0, -1, -1, 1, 1, out _));
    True(!PointerGeometry.IntersectRect(0, 0, 2, 0, 0, 1, -1, -1, 1, 1, out _));
    True(!PointerGeometry.IntersectRect(float.NaN, 0, -2, 0, 0, 1, -1, -1, 1, 1, out _));
    True(!PointerGeometry.IntersectRect(5, 0, -2, 0, 0, 1, -1, -1, 1, 1, out _));
});
Check("distant HUD symbology is excluded from menu hit testing", () =>
    True(!PointerGeometry.IntersectRect(0, 0, -1000, 0, 0, 1, -100, -100, 100, 100, out _)));
Check("map zoom rate is equal at 72, 90, 120 and 144 Hz", () =>
{
    float Simulate(int frames)
    {
        float zoom = 2;
        for (int i = 0; i < frames; i++) zoom = PointerGeometry.Zoom(zoom, 0.6f, 1f / frames);
        return zoom;
    }
    Near(Simulate(72), Simulate(90), 0.0001f);
    Near(Simulate(90), Simulate(120), 0.0001f);
    Near(Simulate(120), Simulate(144), 0.0001f);
});
Check("map zoom ignores invalid input and stays inside game limits", () =>
{
    Near(PointerGeometry.Zoom(2, float.NaN, 1), 2);
    Near(PointerGeometry.Zoom(2, 1, -1), 2);
    Near(PointerGeometry.Zoom(40, 1, 1), 40);
    Near(PointerGeometry.Zoom(1, -1, 1), 1);
    True(PointerGeometry.Zoom(2, 1, 50) < 2.2f);
});

if (args.Length == 2)
    Check("Harmony patch targets and injected fields match the installed game", () =>
        PatchCompatibility.Verify(args[0], args[1]));

Console.WriteLine($"{passed} checks passed.");
