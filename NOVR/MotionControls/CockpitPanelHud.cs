using System;
using System.Collections.Generic;
using TMPro;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Move the existing live widgets, rather than copying their values or drawing a
// second map. Every game-owned transform is restored before the console hides.
internal sealed class CockpitPanelHud
{
    private sealed class Dock
    {
        internal readonly RectTransform Widget, Slot;
        private readonly Transform _parent;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        private readonly Vector2 _anchorMin, _anchorMax, _pivot, _size;
        private readonly int _index;
        private readonly List<Action> _restoreLayout = new();
        private Image? _safety;
        private Text? _safeLabel;
        internal Dock(RectTransform widget, RectTransform slot)
        {
            Widget = widget; Slot = slot; _parent = widget.parent;
            _index = widget.GetSiblingIndex(); _position = widget.localPosition;
            _scale = widget.localScale; _rotation = widget.localRotation;
            _anchorMin = widget.anchorMin; _anchorMax = widget.anchorMax;
            _pivot = widget.pivot; _size = widget.sizeDelta;
            var dimensions = widget.rect.size;
            if (widget.name == "countermeasuresBackground") dimensions = new Vector2(240, 48);
            widget.SetParent(slot, false);
            widget.anchorMin = widget.anchorMax = new Vector2(.5f, .5f);
            widget.sizeDelta = dimensions;
            widget.localRotation = Quaternion.identity;
            if (widget.name == "weaponPanel" || widget.name == "countermeasuresBackground")
            {
                var background = widget.GetComponent<Image>();
                if (background != null)
                {
                    var enabled = background.enabled;
                    _restoreLayout.Add(() => { if (background != null) background.enabled = enabled; });
                    background.enabled = false;
                }
            }
            if (widget.name == "weaponPanel")
            {
                _safety = Find(widget, "weaponDisabled")?.GetComponent<Image>();
                if (_safety != null)
                {
                    var safety = _safety; var color = safety.color;
                    safety.color = new Color(.4f, .3f, .03f, .18f);
                    _safeLabel = Label(widget, "SAFE", new Vector2(80, 33), new Vector2(70, 24), 17);
                    _safeLabel.color = new Color(1f, .75f, .2f);
                    var label = _safeLabel;
                    _restoreLayout.Add(() =>
                    {
                        if (safety != null) safety.color = color;
                        if (label != null) UnityEngine.Object.Destroy(label.gameObject);
                    });
                }
            }
            if (widget.name == "countermeasuresBackground")
            {
                Reflow("countermeasuresImage", new Vector2(-98, 0), new Vector2(36, 36));
                Reflow("countermeasureName", new Vector2(-4, 0), new Vector2(142, 42), 17);
                Reflow("countermeasureAmmo", new Vector2(96, 0), new Vector2(42, 34), 23);
            }
            Fit();
            Debug.Log($"[NOVR] Docked {widget.name} on universal cockpit display; source size={dimensions}.");
        }
        private void Reflow(string name, Vector2 position, Vector2 size, int fontSize = 0)
        {
            var rect = Find(Widget, name) as RectTransform;
            if (rect == null) return;
            var min = rect.anchorMin; var max = rect.anchorMax; var pivot = rect.pivot;
            var delta = rect.sizeDelta; var pos = rect.localPosition; var scale = rect.localScale;
            _restoreLayout.Add(() =>
            {
                if (rect == null) return;
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.sizeDelta = delta; rect.localPosition = pos; rect.localScale = scale;
            });
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition3D = position; rect.localScale = Vector3.one;
            var text = rect.GetComponent<TMP_Text>();
            if (text == null || fontSize == 0) return;
            var originalSize = text.fontSize; var auto = text.enableAutoSizing;
            var alignment = text.alignment; var wrap = text.enableWordWrapping;
            _restoreLayout.Add(() =>
            {
                if (text == null) return;
                text.fontSize = originalSize; text.enableAutoSizing = auto;
                text.alignment = alignment; text.enableWordWrapping = wrap;
            });
            text.enableAutoSizing = false; text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = true;
        }
        internal void Fit()
        {
            if (Widget == null || Widget.parent != Slot) return;
            if (_safeLabel != null) _safeLabel.enabled = _safety != null && _safety.isActiveAndEnabled;
            // Map extent is its clipped viewport, not the large terrain image and
            // distant icons underneath it. The same applies to status containers.
            var size = Widget.rect.size;
            if (size.x < 1 || size.y < 1) return;
            var scale = Mathf.Min(Slot.rect.width / size.x, Slot.rect.height / size.y);
            Widget.localScale = Vector3.one * scale;
            Widget.localPosition = -(Vector3)Widget.rect.center * scale;
        }
        internal void Restore()
        {
            foreach (var restore in _restoreLayout) restore();
            _restoreLayout.Clear();
            if (Widget == null || Widget.parent != Slot) return;
            if (_parent == null)
            {
                // Docking temporarily moves native UI under the persistent VR
                // rig. If its owning scene has gone, it must not become an
                // orphaned singleton or keep updating a destroyed aircraft.
                Widget.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(Widget.gameObject);
                return;
            }
            Widget.SetParent(_parent, false);
            Widget.anchorMin = _anchorMin; Widget.anchorMax = _anchorMax;
            Widget.pivot = _pivot; Widget.sizeDelta = _size;
            Widget.localPosition = _position; Widget.localScale = _scale; Widget.localRotation = _rotation;
            Widget.SetSiblingIndex(_index);
        }
    }
    private readonly Dictionary<string, RectTransform> _slots = new();
    private readonly Dictionary<string, Dock> _docks = new();
    private readonly Transform _canvas;
    private float _nextFind;
    internal bool HasStatus => _docks.ContainsKey("weaponPanel");
    internal bool Contains(Transform widget)
    {
        foreach (var dock in _docks.Values) if (dock.Widget == widget && widget.parent == dock.Slot) return true;
        return false;
    }
    internal CockpitPanelHud(Transform panel)
    {
        var go = new GameObject("Console live displays", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _canvas = go.transform; _canvas.SetParent(panel, false); _canvas.localScale = Vector3.one * .001f;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        Slot("weaponPanel", new Vector2(-130, 83), new Vector2(216, 90));
        Slot("countermeasuresBackground", new Vector2(-130, 17), new Vector2(216, 42));
        Slot("PowerPanel", new Vector2(-130, -20), new Vector2(216, 27));
        // Keep time-critical missile alerts in the pilot's forward view. Only the
        // grid reference moves off the former lower-left map/status container.
        Slot("GridAircraft", new Vector2(-105, -57), new Vector2(150, 18));
        var grid = Label(_canvas, "GRID", new Vector2(-202, -57), new Vector2(50, 20), 11);
        grid.rectTransform.localPosition += Vector3.back * 46;
        Slot("map", new Vector2(130, 25), new Vector2(215, 200));
        LayerHelper.SetLayerRecursive(_canvas, LayerHelper.GetVrUiLayer());
    }
    private static Text Label(Transform parent, string caption, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Console " + caption, typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = caption; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.4f, .8f, .6f); text.raycastTarget = false;
        go.layer = parent.gameObject.layer;
        return text;
    }
    private void Slot(string key, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Display " + key, typeof(RectTransform), typeof(RectMask2D));
        var rect = (RectTransform)go.transform; rect.SetParent(_canvas, false);
        rect.anchoredPosition3D = new Vector3(center.x, center.y, -46);
        rect.sizeDelta = size; _slots.Add(key, rect);
    }
    internal void Update()
    {
        if (Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            var flightHud = SceneSingleton<FlightHud>.i;
            if (flightHud != null)
            {
                foreach (var slot in _slots)
                {
                    if (slot.Key == "map" || (_docks.TryGetValue(slot.Key, out var existing) && existing.Widget != null)) continue;
                    var widget = Find(flightHud.transform, slot.Key) as RectTransform;
                    if (widget != null) _docks[slot.Key] = new Dock(widget, slot.Value);
                }
            }
        }
        var map = SceneSingleton<DynamicMap>.i;
        if (!DynamicMap.mapMaximized && map != null && map.gameObject.activeInHierarchy &&
            (!_docks.TryGetValue("map", out var current) || current.Widget != map.transform || map.transform.parent != current.Slot))
            _docks["map"] = new Dock((RectTransform)map.transform, _slots["map"]);
        foreach (var dock in _docks.Values) dock.Fit();
    }
    private static Transform? Find(Transform root, string prefix)
    {
        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name.StartsWith(prefix)) return child;
            var nested = Find(child, prefix); if (nested != null) return nested;
        }
        return null;
    }
    internal void ReleaseMap()
    {
        if (_docks.TryGetValue("map", out var dock)) { dock.Restore(); _docks.Remove("map"); }
    }
    internal void Restore()
    {
        foreach (var dock in _docks.Values) dock.Restore();
        _docks.Clear(); _nextFind = 0;
    }
}
