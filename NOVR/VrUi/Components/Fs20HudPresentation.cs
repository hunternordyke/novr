using System.Collections.Generic;
using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Hide duplicate instrument presentations, retaining their scripts and every
// world-space target/weapon/flight-path marker. Restore visibility on context exit.
[DefaultExecutionOrder(140)]
internal sealed class Fs20HudPresentation : MonoBehaviour
{
    private readonly Dictionary<Transform, (CanvasGroup group, float alpha, bool owned)> _hidden = new();
    private float _nextFind;
    private FlightHud? _hud;
    private void Awake() => _hud = GetComponent<FlightHud>();
    private void LateUpdate()
    {
        var active = Fs20NativeCockpit.Active;
        if (active && _hud != null && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            if (SceneSingleton<HUDAppManager>.i != null) Hide(SceneSingleton<HUDAppManager>.i.transform);
            foreach (var hmd in _hud.GetComponentsInChildren<NOVRHMDBehavior>(true)) Hide(hmd.transform);
            foreach (var app in _hud.GetComponentsInChildren<HUDAppManager>(true)) Hide(app.transform);
            foreach (var panel in _hud.GetComponentsInChildren<HudReadability>(true)) Hide(panel.transform);
        }
        foreach (var pair in _hidden)
            if (pair.Value.group != null)
                pair.Value.group.alpha = active && !UniversalCockpitPanel.IsDocked(pair.Key) ? 0 : pair.Value.alpha;
    }
    private void Hide(Transform source)
    {
        if (_hidden.ContainsKey(source) || UniversalCockpitPanel.IsDocked(source)) return;
        var group = source.GetComponent<CanvasGroup>(); var owned = group == null;
        if (owned) group = source.gameObject.AddComponent<CanvasGroup>();
        if (group != null) _hidden.Add(source, (group, group.alpha, owned));
    }
    private void Release()
    {
        foreach (var pair in _hidden) if (pair.Value.group != null) { pair.Value.group.alpha = pair.Value.alpha; if (pair.Value.owned) Destroy(pair.Value.group); }
        _hidden.Clear(); _nextFind = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
