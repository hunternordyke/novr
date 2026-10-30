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
    private readonly bool _integrated;
    private BepInEx.Configuration.ConfigEntry<MfdPage> LeftPage => _integrated ? ModConfiguration.Instance.Fs20LeftMfdPage : ModConfiguration.Instance.LeftMfdPage;
    private BepInEx.Configuration.ConfigEntry<MfdPage> RightPage => _integrated ? ModConfiguration.Instance.Fs20RightMfdPage : ModConfiguration.Instance.RightMfdPage;
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
            var layers = new List<(GameObject go, int layer)>();
            foreach (var node in widget.GetComponentsInChildren<Transform>(true)) layers.Add((node.gameObject, node.gameObject.layer));
            var active = widget.gameObject.activeSelf;
            _restoreLayout.Add(() => { foreach (var node in layers) if (node.go != null) node.go.layer = node.layer; if (widget != null) widget.gameObject.SetActive(active); });
            LayerHelper.SetLayerRecursive(widget, LayerHelper.GetVrUiLayer()); widget.gameObject.SetActive(true);
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
                    var visibility = _safeLabel.gameObject.AddComponent<CanvasGroup>();
                    visibility.ignoreParentGroups = true; visibility.blocksRaycasts = false;
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
    private readonly List<Dock> _nativeDocks = new();
    private readonly List<GameObject> _nativeSlots = new();
    private readonly Transform _canvas;
    private readonly CanvasGroup[] _brightness = new CanvasGroup[2];
    private readonly NativeWeaponReadout _weaponReadout = new();
    private readonly Text _targetStatus;
    private float _nextReadout, _lastBrightness = -1;
    private readonly RectTransform _weapons, _map;
    private readonly RectTransform[] _screens = new RectTransform[2];
    private readonly MfdPageMenu[] _pageMenus = new MfdPageMenu[2];
    private readonly Text[] _headers = new Text[2], _messages = new Text[2], _numbers = new Text[2];
    private readonly RawImage[] _feeds = new RawImage[2];
    private readonly Vector3[] _corners = new Vector3[4];
    private float _nextFind;
    private int _slave = -1, _labelSlave = -2;
    private MfdPage _labelLeft = (MfdPage)(-1), _labelRight = (MfdPage)(-1);
    private Vector2 _mapPan;
    private readonly ControllerButtonGate _recenter = new();
    internal bool LeftStickSlaved => _slave >= 0;
    internal Vector2 MapPan => _mapPan;
    internal void ToggleSlave(bool left)
    {
        var index = left ? 0 : 1;
        if (_slave == index) { _slave = -1; _mapPan = Vector2.zero; _recenter.Reset(); return; }
        var config = ModConfiguration.Instance;
        // Map is the first supported interactive page. Selecting the slave key
        // brings it onto this MFD, swapping the other page if necessary.
        if (left) { if (RightPage.Value == MfdPage.Map) RightPage.Value = LeftPage.Value; LeftPage.Value = MfdPage.Map; }
        else { if (LeftPage.Value == MfdPage.Map) LeftPage.Value = RightPage.Value; RightPage.Value = MfdPage.Map; }
        _slave = index; _mapPan = Vector2.zero; _recenter.Reset();
    }
    private void UpdateSlave()
    {
        if (_slave >= 0 && (_slave == 0 ? _left : _right) != MfdPage.Map) { _slave = -1; _mapPan = Vector2.zero; }
        if (_labelSlave != _slave || _labelLeft != _left || _labelRight != _right)
        {
            _labelSlave = _slave; _labelLeft = _left; _labelRight = _right;
            for (var i = 0; i < 2; i++)
            {
                var page = i == 0 ? _left : _right;
                _numbers[i].text = _slave == i ? "SLAVED" : "L STK";
            }
        }
        if (_slave < 0 || VrUi.WristMenu.IsOpen || CockpitLayout.Active || !TrackedControllers.Available) { _recenter.Reset(); return; }
        var hand = TrackedControllers.Read(true);
        _recenter.Update(hand.Valid, hand.StickClick ? 1 : 0);
        if (_recenter.Pressed) { _mapPan = Vector2.zero; InteractionHaptics.Pulse(hand.Device); }
        if (!hand.Valid || !hand.HasAxis) return;
        var map = SceneSingleton<DynamicMap>.i; if (map == null || DynamicMap.mapMaximized) return;
        var axis = new Vector2(FlightControlMath.Axis(hand.Axis.x, .2f, 1), FlightControlMath.Axis(hand.Axis.y, .2f, 1));
        _mapPan = Vector2.ClampMagnitude(_mapPan - axis * (180 * Mathf.Min(Time.unscaledDeltaTime, .05f) / Mathf.Max(1, map.GetZoomLevel())), 2000);
    }
    private Aircraft? _aircraft;
    private MfdPage _left = (MfdPage)(-1), _right = (MfdPage)(-1);
    private HUDApp[] _apps = Array.Empty<HUDApp>();
    private TacScreen? _tac;
    internal bool HasStatus => _docks.ContainsKey("weaponPanel");
    internal bool Contains(Transform widget)
    {
        foreach (var dock in _docks.Values) if (dock.Widget != null && (widget == dock.Widget || widget.IsChildOf(dock.Widget))) return true;
        foreach (var dock in _nativeDocks) if (dock.Widget != null && (widget == dock.Widget || widget.IsChildOf(dock.Widget))) return true;
        return false;
    }
    internal CockpitPanelHud(Transform panel, bool integrated = false)
    {
        _integrated = integrated;
        var go = new GameObject("Console live displays", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _canvas = go.transform; _canvas.SetParent(panel, false); _canvas.localScale = Vector3.one * .001f;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera; go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        for (var i = 0; i < 2; i++)
        {
            var x = i == 0 ? -130f : 130f;
            _screens[i] = Rect("MFD " + i, _canvas, new Vector2(x, 20), new Vector2(215, 180));
            _screens[i].localPosition += Vector3.back * 46;
            _screens[i].gameObject.AddComponent<RectMask2D>();
            _brightness[i] = _screens[i].gameObject.AddComponent<CanvasGroup>();
            _headers[i] = Label(_canvas, "", new Vector2(x, 120), new Vector2(212, 18), 12);
            _headers[i].rectTransform.localPosition += Vector3.back * 46;
            _numbers[i] = Label(_canvas, "", new Vector2(x, -89), new Vector2(45, 14), 10);
            _numbers[i].rectTransform.localPosition += Vector3.back * 53;
            _pageMenus[i] = new MfdPageMenu(i == 0, _headers[i], _screens[i]);
            _messages[i] = Label(_screens[i], "", Vector2.zero, new Vector2(200, 165), 16);
            var feed = Rect("Native camera feed", _screens[i], Vector2.zero, new Vector2(215, 180));
            _feeds[i] = feed.gameObject.AddComponent<RawImage>(); _feeds[i].raycastTarget = false; _feeds[i].enabled = false;
        }
        _weapons = Rect("Weapons page", _canvas, Vector2.zero, new Vector2(215, 180));
        _targetStatus = Label(_weapons, "", new Vector2(0, 79), new Vector2(212, 18), 11);
        _targetStatus.supportRichText = false;
        var readoutVisibility = _targetStatus.gameObject.AddComponent<CanvasGroup>();
        readoutVisibility.ignoreParentGroups = true; readoutVisibility.blocksRaycasts = false;
        Slot("weaponPanel", _weapons, new Vector2(0, 32), new Vector2(212, 72));
        Slot("countermeasuresBackground", _weapons, new Vector2(0, -25), new Vector2(212, 32));
        Slot("PowerPanel", _weapons, new Vector2(0, -55), new Vector2(212, 20));
        Slot("GridAircraft", _weapons, new Vector2(25, -80), new Vector2(150, 16));
        Label(_weapons, "GRID", new Vector2(-72, -80), new Vector2(50, 16), 11);
        _map = Rect("Map page", _canvas, Vector2.zero, new Vector2(215, 180));
        Slot("map", _map, Vector2.zero, new Vector2(215, 180));
        if (integrated)
        {
            // Reuse the lower native display apertures. The canvas root is fixed
            // to the actual cockpit; this layout never uses a movable panel.
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? -110f : 110f;
                _screens[i].localPosition = new Vector3(x, 282, 503);
                _screens[i].localRotation = Quaternion.Euler(12, 0, 0);
                _screens[i].localScale = Vector3.one * .63f;
                var background = _screens[i].gameObject.AddComponent<Image>();
                background.color = new Color(.004f, .009f, .008f, 1); background.raycastTarget = false;
                _headers[i].rectTransform.localPosition = new Vector3(x, 340, 514);
                _headers[i].rectTransform.localRotation = Quaternion.Euler(12, 0, 0);
                _headers[i].rectTransform.localScale = Vector3.one * .67f;
                _numbers[i].gameObject.SetActive(false);
                _headers[i].transform.parent.gameObject.SetActive(false); // Physical nameplate carries the page title.
            }
        }
        LayerHelper.SetLayerRecursive(_canvas, LayerHelper.GetVrUiLayer());
    }
    private float _aspect = 1;
    internal void SetAspect(float aspect)
    {
        if (Mathf.Abs(_aspect - aspect) < .001f) return;
        _aspect = aspect;
        for (var i = 0; i < 2; i++)
        {
            var x = (i == 0 ? -130 : 130) * aspect;
            _screens[i].sizeDelta = new Vector2(215 * aspect, 180);
            foreach (var rect in new[] { _screens[i], _headers[i].rectTransform, _numbers[i].rectTransform })
            { var p = rect.localPosition; p.x = x; rect.localPosition = p; }
        }
        _map.sizeDelta = new Vector2(215 * aspect, 180);
        _slots["map"].sizeDelta = _map.sizeDelta;
        RestoreNative(); _nextFind = 0;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = center; rect.sizeDelta = size; go.layer = parent.gameObject.layer;
        return rect;
    }
    private static Text Label(Transform parent, string caption, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Rect("Console label", parent, position, size); var text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.text = caption;
        text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.4f, .8f, .6f); text.raycastTarget = false; return text;
    }
    private void Slot(string key, Transform parent, Vector2 center, Vector2 size)
    {
        var rect = Rect("Display " + key, parent, center, size); rect.gameObject.AddComponent<RectMask2D>(); _slots.Add(key, rect);
    }
    internal void SetVisible(bool visible)
    {
        _canvas.gameObject.SetActive(visible);
        if (!visible) ClosePageMenus();
    }
    internal void ClosePageMenus() { foreach (var menu in _pageMenus) menu?.Close(); }
    internal string? PageHint(string? control) => _pageMenus[0].Hint(control) ?? _pageMenus[1].Hint(control);
    internal bool PagePoint(string control, out Vector3 point) => _pageMenus[0].TryPoint(control, out point) || _pageMenus[1].TryPoint(control, out point);
    internal bool PageRayPoint(string id, Ray ray, out Vector3 point) => _pageMenus[0].RayPoint(id, ray, out point) || _pageMenus[1].RayPoint(id, ray, out point);
    internal string? NearPageControl(Vector3 position, ref float distance)
    {
        var left = _pageMenus[0].Near(position, ref distance);
        return _pageMenus[1].Near(position, ref distance) ?? left;
    }
    internal string? RayPageControl(Ray ray, ref float distance)
    {
        var left = _pageMenus[0].RayTarget(ray, ref distance);
        return _pageMenus[1].RayTarget(ray, ref distance) ?? left;
    }
    internal void HighlightPages(string? active, string? left, string? right, string? ray)
    { foreach (var menu in _pageMenus) menu.Highlight(active, left, right, ray); }
    internal bool ActivatePageControl(string control, out string feedback)
    {
        for (var i = 0; i < 2; i++)
        {
            if (!_pageMenus[i].Activate(control, out var page, out feedback)) continue;
            if (_pageMenus[i].Open) _pageMenus[1 - i].Close();
            if (page.HasValue)
            {
                var config = ModConfiguration.Instance;
                var pair = MfdPages.Select(LeftPage.Value, RightPage.Value, i == 0, page.Value);
                LeftPage.Value = pair.left; RightPage.Value = pair.right;
            }
            return true;
        }
        feedback = ""; return false;
    }
    private int PageAvailability()
    {
        var mask = (1 << (int)MfdPage.Weapons) | (1 << (int)MfdPage.Map);
        foreach (var app in _apps)
        {
            if (app == null) continue;
            var name = app.GetType().Name;
            if (name == "BasicFlightInstruments") mask |= 1 << (int)MfdPage.Flight;
            if (name is "StatusGauges" or "EngineTelemetry" or "FuelGauge") mask |= 1 << (int)MfdPage.EngineFuel;
            if (name == "SystemStatusDisplay") mask |= 1 << (int)MfdPage.Systems;
        }
        if (_tac != null) mask |= 1 << (int)MfdPage.Tactical;
        if (_aircraft?.targetCam != null) mask |= 1 << (int)MfdPage.Sensor;
        return mask;
    }
    internal void Cycle(bool left, int direction)
    {
        ClosePageMenus();
        var config = ModConfiguration.Instance;
        var pages = MfdPages.Cycle(LeftPage.Value, RightPage.Value, left, direction);
        LeftPage.Value = pages.left; RightPage.Value = pages.right;
    }
    internal void Update()
    {
        if (!GameManager.GetLocalAircraft(out var aircraft)) { Restore(); return; }
        var config = ModConfiguration.Instance;
        if (LeftPage.Value == RightPage.Value)
            RightPage.Value = (MfdPage)(((int)LeftPage.Value + 1) % MfdPages.Count);
        if (_aircraft != aircraft) { Restore(); _aircraft = aircraft; }
        if (_left != LeftPage.Value || _right != RightPage.Value)
        {
            RestoreNative(); _left = LeftPage.Value; _right = RightPage.Value; _nextFind = 0;
            PlacePage(_weapons, MfdPage.Weapons); PlacePage(_map, MfdPage.Map);
            for (var i = 0; i < 2; i++)
            {
                var page = i == 0 ? _left : _right;
                _numbers[i].text = ((int)page + 1) + " / " + MfdPages.Count;
                _messages[i].text = ""; _feeds[i].enabled = false;
            }
        }
        if (Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            var flightHud = SceneSingleton<FlightHud>.i;
            if (flightHud != null)
                foreach (var slot in _slots)
                {
                    if (slot.Key == "map" || (_docks.TryGetValue(slot.Key, out var existing) && existing.Widget != null)) continue;
                    var widget = Find(flightHud.transform, slot.Key) as RectTransform;
                    if (widget != null) _docks[slot.Key] = new Dock(widget, slot.Value);
                }
            var manager = aircraft.cockpit.GetComponentInChildren<MFDAppManager>(true);
            _apps = manager == null ? Array.Empty<HUDApp>() : (HUDApp[])HarmonyLib.AccessTools.Field(typeof(MFDAppManager), "apps").GetValue(manager);
            _tac = aircraft.cockpit.GetComponentInChildren<TacScreen>(true);
            for (var i = 0; i < 2; i++) BuildNativePage(i, i == 0 ? _left : _right);
        }
        var map = SceneSingleton<DynamicMap>.i;
        if (!DynamicMap.mapMaximized && map != null && map.gameObject.activeSelf &&
            (!_docks.TryGetValue("map", out var current) || current.Widget != map.transform || map.transform.parent != current.Slot))
            _docks["map"] = new Dock((RectTransform)map.transform, _slots["map"]);
        foreach (var dock in _docks.Values) dock.Fit();
        foreach (var dock in _nativeDocks) dock.Fit();
        UpdateSlave();
        if (_lastBrightness != config.MfdBrightness.Value)
        {
            _lastBrightness = config.MfdBrightness.Value;
            for (var i = 0; i < 2; i++) _brightness[i].alpha = _lastBrightness;
        }
        if (_weapons.gameObject.activeInHierarchy && Time.unscaledTime >= _nextReadout)
        {
            _nextReadout = Time.unscaledTime + .1f;
            var readout = _weaponReadout.Read(aircraft);
            if (_targetStatus.text != readout.text) _targetStatus.text = readout.text;
            _targetStatus.color = readout.caution ? new Color(1, .75f, .2f) : new Color(.55f, .9f, .7f);
        }
        var available = PageAvailability();
        for (var i = 0; i < 2; i++)
        {
            UpdateFeed(i, i == 0 ? _left : _right);
            _pageMenus[i].Refresh(i == 0 ? _left : _right, available);
        }
    }
    private void PlacePage(RectTransform page, MfdPage kind)
    {
        var side = _left == kind ? 0 : _right == kind ? 1 : -1;
        page.SetParent(side < 0 ? _canvas : _screens[side], false);
        page.localPosition = Vector3.zero; page.localScale = Vector3.one; page.localRotation = Quaternion.identity;
        page.gameObject.SetActive(side >= 0);
    }
    private void BuildNativePage(int index, MfdPage page)
    {
        if (page != MfdPage.Flight && page != MfdPage.EngineFuel && page != MfdPage.Systems) return;
        foreach (var dock in _nativeDocks) if (dock.Slot.IsChildOf(_screens[index])) return;
        var names = page == MfdPage.Flight ? new[] { "BasicFlightInstruments" } :
            page == MfdPage.EngineFuel ? new[] { "StatusGauges" } : new[] { "SystemStatusDisplay" };
        var apps = System.Array.FindAll(_apps, app => app != null && System.Array.IndexOf(names, app.GetType().Name) >= 0);
        if (apps.Length == 0 && page == MfdPage.EngineFuel)
            apps = System.Array.FindAll(_apps, app => app != null && (app.GetType().Name == "EngineTelemetry" || app.GetType().Name == "FuelGauge"));
        _messages[index].text = apps.Length == 0 ? "DISPLAY NOT FITTED\nTO THIS AIRCRAFT" : "";
        var columns = apps.Length > 1 ? 2 : 1; var rows = Mathf.CeilToInt(apps.Length / (float)columns);
        for (var i = 0; i < apps.Length; i++)
        {
            if (apps[i].transform is not RectTransform widget) continue;
            var size = new Vector2((215f * _aspect - 8) / columns, 174f / rows);
            var position = new Vector2((i % columns - (columns - 1) / 2f) * size.x, ((rows - 1) / 2f - i / columns) * size.y);
            var slot = Rect("Native instrument slot", _screens[index], position, size);
            _nativeSlots.Add(slot.gameObject); _nativeDocks.Add(new Dock(widget, slot));
        }
    }
    private void UpdateFeed(int index, MfdPage page)
    {
        if (page != MfdPage.Tactical && page != MfdPage.Sensor) return;
        Texture? texture = null; var uv = new UnityEngine.Rect(0, 0, 1, 1);
        if (page == MfdPage.Sensor && _aircraft?.targetCam != null)
        {
            var camera = HarmonyLib.AccessTools.Field(typeof(TargetCam), "cam").GetValue(_aircraft.targetCam) as Camera;
            if (camera != null && camera.enabled) texture = camera.targetTexture;
        }
        else if (page == MfdPage.Tactical && _tac != null)
        {
            var camera = HarmonyLib.AccessTools.Field(typeof(TacScreen), "cam").GetValue(_tac) as Camera;
            var center = HarmonyLib.AccessTools.Field(typeof(TacScreen), "radarCenter").GetValue(_tac) as RectTransform;
            var canvas = HarmonyLib.AccessTools.Field(typeof(TacScreen), "canvas").GetValue(_tac) as Canvas;
            var viewport = center?.GetComponentInParent<RectMask2D>()?.transform as RectTransform;
            if (camera != null && camera.isActiveAndEnabled && viewport != null && canvas?.transform is RectTransform canvasRect)
            {
                texture = camera.targetTexture;
                // The radar center is only an icon origin, not its visible bounds.
                // Normalize the native clipped viewport in canvas coordinates:
                // screen-space canvas/camera poses can differ until render time.
                viewport.GetWorldCorners(_corners);
                var min = new Vector2(1, 1); var max = Vector2.zero;
                foreach (var corner in _corners)
                {
                    var local = canvasRect.InverseTransformPoint(corner);
                    var rect = canvasRect.rect;
                    var point = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                min = Vector2.Max(Vector2.zero, min); max = Vector2.Min(Vector2.one, max);
                uv = UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (uv.width <= .001f || uv.height <= .001f) texture = null;
            }
        }
        var image = _feeds[index]; image.texture = texture; image.uvRect = uv; image.enabled = texture != null;
        _messages[index].text = texture != null ? "" : page == MfdPage.Sensor ? "NO ACTIVE SENSOR IMAGE\nSelect a target or landing view" : "TACTICAL DISPLAY UNAVAILABLE";
        if (texture == null) return;
        var ratio = texture.width * uv.width / Mathf.Max(1, texture.height * uv.height);
        image.rectTransform.sizeDelta = ratio > 215f * _aspect / 180 ? new Vector2(215 * _aspect, 215 * _aspect / ratio) : new Vector2(180 * ratio, 180);
    }
    private static Transform? Find(Transform root, string prefix)
    {
        for (var i = 0; i < root.childCount; i++)
        { var child = root.GetChild(i); if (child.name.StartsWith(prefix)) return child; var nested = Find(child, prefix); if (nested != null) return nested; }
        return null;
    }
    internal void ReleaseMap()
    { if (_docks.TryGetValue("map", out var dock)) { dock.Restore(); _docks.Remove("map"); } }
    private void RestoreNative()
    {
        foreach (var dock in _nativeDocks) dock.Restore(); _nativeDocks.Clear();
        foreach (var slot in _nativeSlots) if (slot != null) UnityEngine.Object.Destroy(slot); _nativeSlots.Clear();
        foreach (var feed in _feeds) { feed.texture = null; feed.enabled = false; }
    }
    internal void Restore()
    {
        ClosePageMenus();
        _targetStatus.text = ""; _nextReadout = 0;
        _slave = -1; _mapPan = Vector2.zero; _recenter.Reset();
        RestoreNative(); foreach (var dock in _docks.Values) dock.Restore(); _docks.Clear(); _nextFind = 0;
        _left = _right = (MfdPage)(-1); _aircraft = null; _apps = Array.Empty<HUDApp>(); _tac = null;
    }
}
