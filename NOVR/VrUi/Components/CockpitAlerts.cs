using System.Collections.Generic;
using HarmonyLib;
using NOVR.MotionControls;
using NOVR.VrUi.HarmonyPatches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// A presentation-only view of native detections. Never disables ThreatList,
// MessageUI, KillDisplay or RadarWarning: those own sounds, expiry and gameplay.
[DefaultExecutionOrder(120)]
internal sealed class CockpitAlerts : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<ThreatList, Dictionary<PersistentID, ThreatItem>> Items = AccessTools.FieldRefAccess<ThreatList, Dictionary<PersistentID, ThreatItem>>("itemLookup");
    private static readonly AccessTools.FieldRef<ThreatItem, Missile> MissileOf = AccessTools.FieldRefAccess<ThreatItem, Missile>("missile");
    private static readonly AccessTools.FieldRef<MessageUI, TextMeshProUGUI> FeedOf = AccessTools.FieldRefAccess<MessageUI, TextMeshProUGUI>("killFeedText");
    private static readonly AccessTools.FieldRef<MessageUI, MessageFeed> MessagesOf = AccessTools.FieldRefAccess<MessageUI, MessageFeed>("messageFeed");
    private static readonly AccessTools.FieldRef<MessageUI, ChatBox> ChatOf = AccessTools.FieldRefAccess<MessageUI, ChatBox>("chat");
    private static readonly AccessTools.FieldRef<MessageUI, Image> BackgroundOf = AccessTools.FieldRefAccess<MessageUI, Image>("backgroundImage");
    private static readonly AccessTools.FieldRef<KillDisplay, TextMeshProUGUI> RewardOf = AccessTools.FieldRefAccess<KillDisplay, TextMeshProUGUI>("killText");
    private static readonly AccessTools.FieldRef<KillDisplay, TextMeshProUGUI> RankOf = AccessTools.FieldRefAccess<KillDisplay, TextMeshProUGUI>("rankText");
    private static readonly AccessTools.FieldRef<KillDisplay, Image> RankBackground = AccessTools.FieldRefAccess<KillDisplay, Image>("rankBackground");
    private sealed class Signal
    {
        internal Unit Source = null!;
        internal bool Jam;
        internal Vector3 Direction;
        internal float Expires;
    }
    private sealed class TextMirror
    {
        internal readonly TMP_Text Source;
        private readonly TextMeshProUGUI[] _copies;
        private readonly CanvasGroup _hide;
        private string? _last;
        internal TextMirror(TMP_Text source, Transform parent, Vector2 position, Vector2 size, int fontSize, bool feed = false)
        {
            Source = source;
            _hide = source.gameObject.AddComponent<CanvasGroup>();
            _hide.blocksRaycasts = false;
            _copies = new TextMeshProUGUI[feed ? 3 : 1];
            for (var i = 0; i < _copies.Length; i++)
            {
                var go = new GameObject("Compact notification", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
                var copy = go.GetComponent<TextMeshProUGUI>(); _copies[i] = copy;
                copy.font = source.font; copy.fontSharedMaterial = source.fontSharedMaterial;
                copy.text = ""; copy.fontSize = fontSize; copy.richText = false; copy.raycastTarget = false;
                copy.alignment = TextAlignmentOptions.TopLeft; copy.enableWordWrapping = !feed;
                copy.overflowMode = TextOverflowModes.Ellipsis;
                var height = size.y / _copies.Length;
                copy.rectTransform.sizeDelta = new Vector2(size.x, height);
                copy.rectTransform.anchoredPosition = position + new Vector2(0, size.y * .5f - height * (i + .5f));
            }
        }
        internal void Refresh(bool active, bool show, bool feed)
        {
            if (_hide != null) _hide.alpha = active ? 0 : 1;
            var visible = active && show && Source != null && Source.isActiveAndEnabled;
            foreach (var copy in _copies) copy.enabled = visible;
            if (!visible) return;
            var text = Source!.text;
            if (_last != text)
            {
                _last = text;
                var compact = CockpitAlertMath.LatestFeed(text, feed ? 3 : 2);
                var lines = feed ? compact.Split('\n') : new[] { compact };
                for (var i = 0; i < _copies.Length; i++) _copies[i].text = i < lines.Length ? lines[i] : "";
            }
            foreach (var copy in _copies) copy.color = feed ? new Color(.72f, .82f, .84f, .85f) : Source.color;
        }
        internal void Release()
        {
            if (_hide != null) { _hide.alpha = 1; Destroy(_hide); }
            foreach (var copy in _copies) if (copy != null) Destroy(copy.gameObject);
        }
    }

    internal static CockpitAlerts? Instance { get; private set; }
    internal static bool Active => Instance?._active == true;
    private Aircraft? _aircraft;
    private ThreatList? _list;
    private CanvasGroup? _listHide, _rankHide;
    private Image? _rankBackground;
    private MessageUI? _messages;
    private CanvasGroup? _messageBackgroundHide;
    private TextMirror? _feed, _reward, _rank;
    private readonly List<Signal> _signals = new();
    private readonly List<ThreatItem> _missiles = new();
    private readonly List<Text> _pins = new();
    private readonly List<Text> _rows = new();
    private GameObject? _root, _threatRoot;
    private Transform? _feedRoot;
    private Text? _heading;
    private bool _active;
    private float _nextRead, _nextFind;
    private static readonly Color MissileColor = new(1f, .34f, .20f);
    private static readonly Color RadarColor = new(1f, .74f, .25f);
    private static readonly Color JamColor = new(.45f, .85f, 1f);
    private CockpitAlertSettings Settings => ModConfiguration.Instance.CockpitAlerts;

    private void Awake() => Instance = this;
    private void OnEnable() => Application.onBeforeRender += Place;

    internal static void Record(Aircraft aircraft, Unit source, bool jam)
    {
        var self = Instance;
        if (self == null || aircraft == null || source == null || !GameManager.GetLocalAircraft(out var local) || local != aircraft) return;
        self.SetAircraft(aircraft);
        Signal? signal = null;
        foreach (var candidate in self._signals)
            if (candidate.Source == source && candidate.Jam == jam) { signal = candidate; break; }
        if (signal == null)
        {
            // Bound memory and geometry even in missions with many emitters.
            if (self._signals.Count >= 24) self._signals.RemoveAt(0);
            signal = new Signal { Source = source, Jam = jam }; self._signals.Add(signal);
        }
        // Radar bearing is sampled only when the game reports a detection.
        signal.Direction = (source.transform.position - aircraft.transform.position).normalized;
        signal.Expires = Time.timeSinceLevelLoad + (jam ? 1f : 4f);
    }

    private void SetAircraft(Aircraft? aircraft)
    {
        if (_aircraft == aircraft) return;
        ReleaseSources(); _signals.Clear(); _missiles.Clear();
        _aircraft = aircraft; _nextRead = _nextFind = 0;
    }

    private bool Ready() => Settings.Enabled.Value && XRSettings.enabled && _aircraft != null &&
        _aircraft.cockpit != null && !_aircraft.cockpit.IsDetached() &&
        CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.flightControlsEnabled &&
        NOUIManager.I != null && APIBus.MainCamera != null &&
        SceneSingleton<CameraStateManager>.i?.followingUnit == _aircraft &&
        Time.timeScale > 0 && !PlayerSettings.cinematicMode && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
        !WristMenu.IsOpen && !CockpitLayout.Active &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);

    private void LateUpdate()
    {
        GameManager.GetLocalAircraft(out var aircraft); SetAircraft(aircraft);
        var wasActive = _active; _active = Ready();
        if (wasActive && !_active) ThreatItemNotchDirectionPatch.RestoreCompactIndicators();
        if (_active)
        {
            EnsureUi();
            if (Time.unscaledTime >= _nextFind) { _nextFind = Time.unscaledTime + 1; FindSources(); }
            if (Time.unscaledTime >= _nextRead) { _nextRead = Time.unscaledTime + .1f; RefreshThreats(); }
            Place();
        }
        if (_root != null) _root.SetActive(_active);
        if (_listHide != null) _listHide.alpha = _active ? 0 : 1;
        if (_rankHide != null) _rankHide.alpha = _active ? 0 : 1;
        if (_messageBackgroundHide != null && _messages != null)
            _messageBackgroundHide.alpha = _active && MessagesOf(_messages).NoText && !ChatOf(_messages).gameObject.activeSelf ? 0 : 1;
        _feed?.Refresh(_active, Settings.Feed.Value, true);
        _reward?.Refresh(_active, true, false); _rank?.Refresh(_active, true, false);
    }

    private void FindSources()
    {
        if (_list == null && SceneSingleton<FlightHud>.i != null)
        {
            _list = SceneSingleton<FlightHud>.i.GetComponentInChildren<ThreatList>(true);
            if (_list != null)
            {
                _listHide = _list.gameObject.AddComponent<CanvasGroup>();
                _listHide.blocksRaycasts = false;
            }
        }
        var messages = SceneSingleton<MessageUI>.i;
        if (_messages != messages)
        {
            if (_messageBackgroundHide != null) { _messageBackgroundHide.alpha = 1; Destroy(_messageBackgroundHide); }
            _messages = messages;
            var messageBackground = messages != null ? BackgroundOf(messages) : null;
            _messageBackgroundHide = messageBackground != null ? messageBackground.gameObject.AddComponent<CanvasGroup>() : null;
        }
        var source = messages != null ? FeedOf(messages) : null;
        if (_feed?.Source != source)
        {
            _feed?.Release(); _feed = source == null ? null : new TextMirror(source, _feedRoot!, new Vector2(0, -28), new Vector2(450, 110), 18, true);
        }
        var kills = SceneSingleton<KillDisplay>.i;
        var reward = kills != null ? RewardOf(kills) : null;
        if (_reward?.Source != reward)
        {
            _reward?.Release(); _reward = reward == null ? null : new TextMirror(reward, _feedRoot!, new Vector2(0, 66), new Vector2(450, 45), 20);
        }
        var rank = kills != null ? RankOf(kills) : null;
        if (_rank?.Source != rank)
        {
            _rank?.Release(); _rank = rank == null ? null : new TextMirror(rank, _feedRoot!, new Vector2(0, -115), new Vector2(450, 45), 18);
        }
        var background = kills != null ? RankBackground(kills) : null;
        if (_rankBackground != background)
        {
            if (_rankHide != null) { _rankHide.alpha = 1; Destroy(_rankHide); }
            _rankBackground = background;
            _rankHide = background == null ? null : background.gameObject.AddComponent<CanvasGroup>();
            if (_rankHide != null) _rankHide.blocksRaycasts = false;
        }
    }

    private void Place()
    {
        if (!_active || _root == null || _aircraft == null || _aircraft.cockpitViewPoint == null) return;
        var gameRoot = APIBus.MainCamera?.transform.parent;
        var uiRoot = APIBus.CockpitHudCamera.transform.parent;
        if (gameRoot == null || uiRoot == null) return;
        var eye = _aircraft.cockpitViewPoint;
        // Same cockpit-to-UI-space conversion as the dashboard. Physical leaning,
        // head turns and saved seat shifts do not drag the overlay around.
        var position = eye.TransformPoint(new Vector3(0, Settings.Height.Value, 1.4f));
        _root.transform.SetPositionAndRotation(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(position)),
            uiRoot.rotation * Quaternion.Inverse(gameRoot.rotation) * eye.rotation);
        _root.transform.localScale = Vector3.one * (.00085f * Settings.Scale.Value);
    }

    private void RefreshThreats()
    {
        if (_aircraft == null) return;
        _missiles.Clear();
        if (_list != null)
            foreach (var entry in Items(_list))
                if (entry.Value != null && MissileOf(entry.Value) != null) _missiles.Add(entry.Value);
        _missiles.Sort(CompareThreats);
        var now = Time.timeSinceLevelLoad;
        _signals.RemoveAll(s => s.Source == null || s.Expires <= now);
        _threatRoot!.SetActive(_missiles.Count > 0 || _signals.Count > 0);
        foreach (var pin in _pins) pin.enabled = false;
        foreach (var row in _rows) row.text = "";
        _heading!.text = _missiles.Count > 0 ? $"MISSILE  {_missiles.Count}" : "RADAR / EW";
        _heading.color = _missiles.Count > 0 ? MissileColor : RadarColor;
        var pinIndex = 0;
        for (var i = 0; i < _missiles.Count; i++)
        {
            var item = _missiles[i]; var missile = MissileOf(item);
            var direction = _aircraft.transform.InverseTransformDirection(missile.transform.position - _aircraft.transform.position);
            Pin(ref pinIndex, direction, "M", MissileColor, 70);
            if (i >= 3) continue;
            var height = direction.y > direction.magnitude * .2f ? " HIGH" : direction.y < -direction.magnitude * .2f ? " LOW" : "";
            var text = $"{missile.GetSeekerType()}  {CockpitAlertMath.Clock(direction.x, direction.z)}h{height}  {UnitConverter.DistanceReading(direction.magnitude)}";
            if (ThreatItemNotchDirectionPatch.TryGetNotchDirection(item, out var notch) && missile.GetSeekerType().Contains("RH"))
            {
                notch = _aircraft.transform.InverseTransformDirection(notch);
                text += $"\nNOTCH {CockpitAlertMath.Clock(notch.x, notch.z)}h";
            }
            _rows[i].text = text; _rows[i].color = MissileColor;
        }
        var radar = 0; var jamming = 0;
        foreach (var signal in _signals)
        {
            var direction = _aircraft.transform.InverseTransformDirection(signal.Direction);
            var color = signal.Jam ? JamColor : RadarColor;
            color.a = Mathf.Clamp01((signal.Expires - now) / (signal.Jam ? 1f : 4f));
            Pin(ref pinIndex, direction, signal.Jam ? "J" : "R", color, signal.Jam ? 88 : 52);
            if (signal.Jam) jamming++; else radar++;
        }
        _rows[3].text = (_missiles.Count > 3 ? $"+{_missiles.Count - 3} more  ·  " : "") + $"RADAR {radar}  ·  JAM {jamming}";
        _rows[3].color = RadarColor;
    }

    private int CompareThreats(ThreatItem a, ThreatItem b)
    {
        var first = MissileOf(a); var second = MissileOf(b);
        var priority = (second.seekerMode == Missile.SeekerMode.activeLock).CompareTo(first.seekerMode == Missile.SeekerMode.activeLock);
        return priority != 0 ? priority : (first.transform.position - _aircraft!.transform.position).sqrMagnitude.CompareTo((second.transform.position - _aircraft!.transform.position).sqrMagnitude);
    }
    private void Pin(ref int index, Vector3 direction, string symbol, Color color, float radius)
    {
        if (index >= _pins.Count) return;
        var pin = _pins[index++]; var p = CockpitAlertMath.Scope(direction.x, direction.z, radius);
        pin.rectTransform.anchoredPosition = new Vector2(-100 + p.x, p.y);
        pin.text = symbol; pin.color = color; pin.enabled = true;
    }

    private void EnsureUi()
    {
        if (_root != null) return;
        _root = new GameObject("NOVR frameless cockpit alerts", typeof(RectTransform), typeof(Canvas));
        _root.transform.SetParent(transform, false);
        var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        var group = _root.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
        _threatRoot = new GameObject("Threat scope", typeof(RectTransform));
        _threatRoot.transform.SetParent(_root.transform, false); _threatRoot.transform.localPosition = new Vector3(-390, 0, 0);
        _feedRoot = new GameObject("Quiet notifications", typeof(RectTransform)).transform;
        _feedRoot.SetParent(_root.transform, false); _feedRoot.localPosition = new Vector3(350, 0, 0);
        for (var i = 0; i < 36; i++)
        {
            var angle = i * Mathf.PI * 2 / 36;
            var tick = new GameObject("Bearing tick", typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(_threatRoot.transform, false);
            var rect = (RectTransform)tick.transform;
            rect.anchoredPosition = new Vector2(-100 + Mathf.Sin(angle) * 70, Mathf.Cos(angle) * 70);
            rect.sizeDelta = new Vector2(1, i % 9 == 0 ? 9 : 3);
            rect.localRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg);
            var image = tick.GetComponent<Image>(); image.raycastTarget = false; image.color = new Color(.55f, .7f, .72f, .45f);
        }
        Label(_threatRoot.transform, "12", new Vector2(-100, 111), new Vector2(40, 25), 16);
        Label(_threatRoot.transform, "6", new Vector2(-100, -111), new Vector2(40, 25), 16);
        Label(_threatRoot.transform, "9", new Vector2(-212, 0), new Vector2(30, 25), 16);
        Label(_threatRoot.transform, "3", new Vector2(12, 0), new Vector2(30, 25), 16);
        Label(_threatRoot.transform, "▲", new Vector2(-100, 0), new Vector2(30, 30), 20);
        _heading = Label(_threatRoot.transform, "", new Vector2(200, 106), new Vector2(290, 28), 22);
        _heading.alignment = TextAnchor.MiddleLeft;
        for (var i = 0; i < 4; i++)
        {
            var row = Label(_threatRoot.transform, "", new Vector2(200, 59 - i * 49), new Vector2(290, 46), i == 3 ? 16 : 18);
            row.alignment = TextAnchor.MiddleLeft; _rows.Add(row);
        }
        for (var i = 0; i < 40; i++) _pins.Add(Label(_threatRoot.transform, "", Vector2.zero, new Vector2(24, 24), 20));
        LayerHelper.SetLayerRecursive(_root.transform, LayerHelper.GetVrUiLayer());
    }
    private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Alert text", typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = value; text.fontSize = fontSize; text.supportRichText = false;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.color = new Color(.72f, .82f, .84f);
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
        var shadow = go.GetComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(1, -1);
        return text;
    }
    private void ReleaseSources()
    {
        if (_listHide != null) { _listHide.alpha = 1; Destroy(_listHide); }
        if (_rankHide != null) { _rankHide.alpha = 1; Destroy(_rankHide); }
        if (_messageBackgroundHide != null) { _messageBackgroundHide.alpha = 1; Destroy(_messageBackgroundHide); }
        _feed?.Release(); _reward?.Release(); _rank?.Release();
        _feed = _reward = _rank = null; _list = null; _listHide = _rankHide = null; _rankBackground = null;
        _messageBackgroundHide = null; _messages = null;
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= Place; _active = false; ReleaseSources();
        ThreatItemNotchDirectionPatch.RestoreCompactIndicators();
        if (_root != null) _root.SetActive(false);
    }
    private void OnDestroy() { ReleaseSources(); if (Instance == this) Instance = null; }
}
