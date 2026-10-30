using UnityEngine;

namespace NOVR.MotionControls;

// Measured native FS-20 switch rows; coordinates are fixed to cockpit_int.
internal static class Fs20PanelLayout
{
    internal enum Kind { Page, Switch, Hold, Setup, MapSlave }
    internal sealed class Control
    {
        internal readonly string Id, Label;
        internal readonly Vector3 Point;
        internal readonly Vector2 Size;
        internal readonly Kind Type;
        internal readonly Quaternion Rotation;
        internal Control(string id, string label, Vector3 point, Vector2 size, Kind type, bool side = false)
        { Id = id; Label = label; Point = point; Size = size; Type = type; Rotation = side ? Quaternion.Euler(90,0,0) : Face; }
    }
    internal static readonly Quaternion Face = Quaternion.Euler(12, 0, 0);
    internal static readonly Vector3 Normal = Face * Vector3.back;
    internal static readonly Control[] Controls =
    {
        new("left_prev", "◀ PREV", new(-.154f,.363f,.529f), new(.078f,.035f), Kind.Page),
        new("left_next", "NEXT ▶", new(-.066f,.363f,.529f), new(.078f,.035f), Kind.Page),
        new("right_prev", "◀ PREV", new(.066f,.363f,.529f), new(.078f,.035f), Kind.Page),
        new("right_next", "NEXT ▶", new(.154f,.363f,.529f), new(.078f,.035f), Kind.Page),
        new("gear", "GEAR", new(-.153f,.196f,.498f), new(.074f,.049f), Kind.Switch),
        new("brake", "BRAKE", new(-.051f,.196f,.498f), new(.074f,.049f), Kind.Hold),
        new("radar", "RADAR", new(.051f,.196f,.498f), new(.074f,.049f), Kind.Switch),
        new("aots", "AOTS HUD", new(.153f,.196f,.498f), new(.074f,.049f), Kind.Switch),
        new("map_pan", "MAP PAN", new(0,.285f,.488f), new(.042f,.042f), Kind.MapSlave),
        new("left_lock", "THROTTLE POS", new(-.48f,.08f,.09f), new(.108f,.045f), Kind.Setup, true),
        new("right_lock", "STICK POS", new(.48f,.08f,.09f), new(.108f,.045f), Kind.Setup, true),
        new("seat_up", "▲ UP", new(-.507f,.08f,.025f), new(.048f,.045f), Kind.Setup, true),
        new("seat_down", "▼ DOWN", new(-.453f,.08f,.025f), new(.048f,.045f), Kind.Setup, true),
        new("seat_left", "◀ LEFT", new(-.507f,.08f,-.027f), new(.048f,.045f), Kind.Setup, true),
        new("seat_right", "RIGHT ▶", new(-.453f,.08f,-.027f), new(.048f,.045f), Kind.Setup, true),
        new("seat_forward", "▲ FWD", new(-.507f,.08f,-.079f), new(.048f,.045f), Kind.Setup, true),
        new("seat_back", "▼ BACK", new(-.453f,.08f,-.079f), new(.048f,.045f), Kind.Setup, true),
    };
}
