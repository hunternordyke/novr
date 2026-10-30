using UnityEngine;

namespace NOVR.MotionControls;

// Authored local joint poses, fitted to the installed FS-20 control meshes.
// Fingers follow the grip's taper independently; the thumb remains available
// for HOTAS controls. No native mesh data or per-frame collision solver ships.
internal static class Fs20HandPoses
{
    internal static Vector3 Wrist(bool stick, bool left) => stick
        ? new Vector3(left ? -.058f : .058f, .015f, -.068f)
        : new Vector3(left ? -.0045f : .0045f, .031f, -.05775f);

    internal static Quaternion FireButtonJoint(int bone, bool left, Quaternion open, Quaternion closed, bool pressed)
    {
        // Separate thumb pad contact and short inward press on the red
        // weapon-release cap. Palm and support fingers use the same stick pose.
        var curl = bone == 2 ? (pressed ? .15f : .1125f) : bone == 3 ? (pressed ? .5608015f : .5833015f) : .4660955f;
        var pose = Quaternion.Slerp(open, closed, curl);
        if (bone != 2) return pose;
        var angles = new Vector3(102.66439f, -97.58724f, -62.70365f);
        if (left) { angles.y = -angles.y; angles.z = -angles.z; }
        return Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up) * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
    }

    internal static Quaternion Joint(int bone, bool stick, bool left, Quaternion open, Quaternion closed, bool thumbContact = false)
    {
        var curl = stick ? bone switch
        {
            2 => .02f, 3 => .52f, 4 => .75f,
            7 => .23f, 8 => .16f, 9 => .01f,
            12 => .47f, 13 => .60f, 14 => .66f,
            17 => .55f, 18 => .55f, 19 => .81f,
            22 => .83f, 23 => .48f, 24 => .66f, _ => 0
        } : bone switch
        {
            3 => .13f, 4 => .08f,
            7 => .12f, 8 => .24f, 9 => .37f,
            12 => .31f, 13 => .49f, 14 => .85f,
            17 => .32f, 18 => .63f, 19 => .85f,
            22 => .47f, 23 => .85f, 24 => .64f, _ => 0
        };
        if (thumbContact && bone >= 2 && bone <= 4)
            curl = stick ? (bone == 2 ? .311062f : 1) : (bone == 2 ? .204672f : bone == 3 ? .095984f : .368217f);
        var pose = Quaternion.Slerp(open, closed, curl);
        if (bone == 2)
        {
            var angles = stick ? new Vector3(65.25f, -64.13583f, -40.44866f) : new Vector3(-26,18,0);
            if (thumbContact) angles = stick ? new Vector3(89.39010f,-78.24105f,-76.24373f) : new Vector3(-1.57100f,48.67644f,14.84383f);
            if (stick == left) { angles.y = -angles.y; angles.z = -angles.z; }
            pose = Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up) * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
        }
        if (stick && bone == 7) pose = Quaternion.AngleAxis(left ? 3.5f : -3.5f, Vector3.up) * pose;
        return pose;
    }
}
