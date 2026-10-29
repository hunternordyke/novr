using System;
using System.Collections.Generic;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Touch targets share the same rectangles as the drawn buttons. The cockpit
// interaction owner supplies press/release gating for both direct and ray input.
internal sealed class MfdPageMenu
{
    private sealed class Key
    {
        internal string Id = "";
        internal RectTransform Rect = null!;
        internal Image Background = null!;
        internal Text Label = null!;
        internal int Page;
    }
    private readonly bool _left;
    private readonly List<Key> _keys = new();
    private readonly RectTransform _menu;
    private readonly Text _header;
    private MfdPage _current = (MfdPage)(-1);
    private int _available = -1;
    private bool _open, _dirty = true;
    internal bool Open => _open;
    private string Side => _left ? "LEFT" : "RIGHT";

    internal MfdPageMenu(bool left, Text header, RectTransform screen)
    {
        _left = left; _header = header;
        var bar = Rect(header.transform.parent, "MFD page selector", new Vector2(left ? -130 : 130, 127), new Vector2(212, 26));
        bar.localPosition += Vector3.back * 46;
        header.transform.SetParent(bar, false); header.rectTransform.anchoredPosition3D = Vector3.zero; header.rectTransform.sizeDelta = new Vector2(208, 24); header.fontSize = 13;
        AddKey(bar, header, -1);
        _menu = Rect(screen, "MFD page choices", Vector2.zero, new Vector2(215, 180));
        _menu.localPosition += Vector3.back * 2;
        var visibility = _menu.gameObject.AddComponent<CanvasGroup>();
        visibility.ignoreParentGroups = true; visibility.blocksRaycasts = false;
        _menu.gameObject.AddComponent<Image>().color = new Color(.015f, .035f, .04f, 1);
        for (var i = 0; i <= MfdPages.Count; i++)
        {
            var button = Rect(_menu, "MFD page " + i, new Vector2(i % 2 == 0 ? -53 : 53, 63 - i / 2 * 42), new Vector2(100, 38));
            var textRect = Rect(button, "Page name", Vector2.zero, new Vector2(96, 36));
            var text = textRect.gameObject.AddComponent<Text>(); text.font = header.font;
            text.fontSize = 13; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            AddKey(button, text, i);
        }
        _menu.gameObject.SetActive(false);
    }
    private void AddKey(RectTransform rect, Text label, int page)
    {
        var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
        _keys.Add(new Key { Id = "mfd_ui_" + (_left ? "l_" : "r_") + page, Rect = rect, Background = image, Label = label, Page = page });
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; rect.gameObject.layer = parent.gameObject.layer;
        return rect;
    }
    internal void Close() { if (!_open) return; _open = false; _menu.gameObject.SetActive(false); _dirty = true; }
    internal void Refresh(MfdPage current, int available)
    {
        if (_open) _menu.SetAsLastSibling();
        if (!_dirty && current == _current && available == _available) return;
        _current = current; _available = available; _dirty = false;
        _header.text = _open ? Side + " MFD · CLOSE" : MfdPages.Title(current) + " · PAGES";
        foreach (var key in _keys)
        {
            if (key.Page < 0) continue;
            key.Label.text = key.Page == MfdPages.Count ? "BACK" : MfdPages.ShortTitle((MfdPage)key.Page) +
                (!Available(key.Page) ? "\nNOT FITTED" : key.Page == (int)current ? "\nSELECTED" : "");
        }
    }
    private bool Available(int page) => page < 0 || page >= MfdPages.Count || (_available & (1 << page)) != 0;
    internal bool Activate(string id, out MfdPage? page, out string feedback)
    {
        page = null; feedback = "";
        foreach (var key in _keys)
        {
            if (key.Id != id || !key.Rect.gameObject.activeInHierarchy) continue;
            if (key.Page < 0) { _open = !_open; _menu.gameObject.SetActive(_open); if (_open) _menu.SetAsLastSibling(); _dirty = true; }
            else if (key.Page == MfdPages.Count) Close();
            else if (!Available(key.Page)) feedback = MfdPages.Title((MfdPage)key.Page) + " · NOT FITTED TO THIS AIRCRAFT";
            else { page = (MfdPage)key.Page; Close(); feedback = Side + " MFD · " + MfdPages.Title(page.Value); }
            return true;
        }
        return false;
    }
    internal string? Hint(string? id)
    {
        foreach (var key in _keys)
            if (key.Id == id)
                return key.Page < 0 ? Side + " MFD · " + (_open ? "CLOSE PAGE CHOICES" : "CHOOSE A DISPLAY PAGE") :
                    key.Page == MfdPages.Count ? "RETURN TO " + MfdPages.Title(_current) :
                    MfdPages.Title((MfdPage)key.Page) + (Available(key.Page) ? " · SELECT ON " + Side + " MFD" : " · NOT FITTED TO THIS AIRCRAFT");
        return null;
    }
    internal bool TryPoint(string id, out Vector3 point)
    {
        foreach (var key in _keys) if (key.Id == id) { point = key.Rect.TransformPoint(key.Rect.rect.center); return true; }
        point = default; return false;
    }
    internal bool RayPoint(string id, Ray ray, out Vector3 point)
    {
        foreach (var key in _keys) if (key.Id == id)
            return ControllerUiPointer.HitRect(ray, key.Rect, out _, out point);
        point = default; return false;
    }
    internal string? Near(Vector3 position, ref float distance)
    {
        string? selected = null;
        foreach (var key in _keys)
        {
            if (!key.Rect.gameObject.activeInHierarchy) continue;
            var local = key.Rect.InverseTransformPoint(position);
            if (!key.Rect.rect.Contains(local)) continue;
            var gap = Mathf.Abs(Vector3.Dot(position - key.Rect.position, key.Rect.forward));
            if (gap >= distance) continue;
            distance = gap; selected = key.Id;
        }
        return selected;
    }
    internal string? RayTarget(Ray ray, ref float distance)
    {
        string? selected = null;
        foreach (var key in _keys)
            if (key.Rect.gameObject.activeInHierarchy && Vector3.Dot(ray.direction, key.Rect.forward) > 0 &&
                ControllerUiPointer.HitRect(ray, key.Rect, out var hit, out _) && hit < distance)
            { distance = hit; selected = key.Id; }
        return selected;
    }
    internal void Highlight(string? active, string? left, string? right, string? ray)
    {
        foreach (var key in _keys)
        {
            var unavailable = !Available(key.Page);
            var selected = key.Page == (int)_current;
            var hover = key.Id == left || key.Id == right || key.Id == ray;
            var color = unavailable ? new Color(.04f, .045f, .05f) : key.Id == active ? new Color(.18f, .45f, .34f) :
                hover ? new Color(.12f, .3f, .25f) : selected ? new Color(.06f, .19f, .16f) : new Color(.035f, .085f, .09f);
            if (key.Background.color != color) key.Background.color = color;
            var ink = unavailable ? new Color(.5f, .53f, .54f) : new Color(.7f, 1, .85f);
            if (key.Label.color != ink) key.Label.color = ink;
        }
    }
}
