using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOVR.VrUi.Native;

internal static class NativeMissionHeaders
{
    // QuickLoadMany was removed in Nuclear Option 0.34. Skip broken headers individually.
    public static IEnumerable<(MissionKey key, MissionQuickLoad mission)> QuickLoadMany(IEnumerable<MissionKey> keys)
    {
        foreach (var key in keys)
        {
            if (MissionSaveLoad.QuickLoadOne(key, out var mission, out var error))
                yield return (key, mission);
            else
                Debug.LogWarning($"[NOVR] Could not read mission '{key}': {error}");
        }
    }
}
