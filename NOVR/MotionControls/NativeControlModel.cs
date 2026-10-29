using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Render-only copies of the installed aircraft's own meshes/materials. No game
// assets are exported, no native scripts/colliders are cloned or destroyed.
internal sealed class NativeControlModel : IDisposable
{
    private readonly Transform _root;
    private readonly List<(Renderer renderer, bool hidden)> _originals = new();
    private readonly List<MeshRenderer> _copies = new();
    private readonly List<MaterialPropertyBlock> _originalBlocks = new();
    private readonly MaterialPropertyBlock _highlight = new();
    private int _lastHighlight = -1;
    private bool _visible;

    private NativeControlModel(Transform parent)
    {
        _root = new GameObject("Native aircraft control").transform;
        _root.SetParent(parent, false);
    }

    internal static NativeControlModel? TryCreate(Aircraft aircraft, bool stick, Transform parent)
    {
        NativeControlModel? model = null;
        try
        {
            var cockpit = aircraft.cockpit.GetComponentInChildren<Cockpit>(true);
            if (cockpit == null) return null;
            var controls = AccessTools.Field(typeof(Cockpit), stick ? "joysticks" : "throttles").GetValue(cockpit) as IEnumerable;
            if (controls == null) return null;
            var sources = new List<Transform>();
            foreach (var control in controls)
            {
                var source = AccessTools.Field(control.GetType(), "transform").GetValue(control) as Transform;
                if (source != null && SeparateMesh(source)) sources.Add(source);
            }
            if (sources.Count == 0) return null;
            model = new NativeControlModel(parent);
            var selected = sources[0];
            var geometry = new GameObject("Aircraft handle geometry").transform;
            geometry.SetParent(model._root, false); geometry.localScale = selected.lossyScale;
            foreach (var renderer in selected.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var go = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(geometry, false);
                go.transform.localPosition = selected.InverseTransformPoint(renderer.transform.position);
                go.transform.localRotation = Quaternion.Inverse(selected.rotation) * renderer.transform.rotation;
                var scale = renderer.transform.lossyScale; var sourceScale = selected.lossyScale;
                go.transform.localScale = new Vector3(scale.x / sourceScale.x, scale.y / sourceScale.y, scale.z / sourceScale.z);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var copy = go.GetComponent<MeshRenderer>(); copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = renderer.receiveShadows;
                copy.lightProbeUsage = renderer.lightProbeUsage; copy.reflectionProbeUsage = renderer.reflectionProbeUsage;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); copy.SetPropertyBlock(block);
                model._copies.Add(copy); model._originalBlocks.Add(block);
            }
            if (model._copies.Count == 0) { model.Dispose(); return null; }
            var bounds = model.Bounds();
            // The stick grab point sits in the upper grip. Throttles use the
            // handle's center; very remote native animation pivots are excluded.
            var grip = bounds.center;
            if (stick) grip.y = bounds.max.y - Mathf.Min(.055f, bounds.size.y * .25f);
            geometry.localPosition = -grip;
            if (stick) model._root.localPosition = Vector3.up * .12f;
            foreach (var source in sources)
                foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.GetComponent<MeshFilter>()?.sharedMesh != null)
                        model._originals.Add((renderer, renderer.forceRenderingOff));
            LayerHelper.SetLayerRecursive(model._root, LayerHelper.Layers.Cockpit);
            Debug.Log($"[NOVR] Reused native {(stick ? "stick" : "throttle")} model from {selected.name}; {model._copies.Count} mesh renderers, {model._originals.Count} stationary renderers managed.");
            return model;
        }
        catch (Exception exception)
        {
            model?.Dispose();
            Debug.LogWarning($"[NOVR] Native {(stick ? "stick" : "throttle")} model unavailable; retaining the fallback and original cockpit geometry. {exception.Message}");
            return null;
        }
    }

    private static bool SeparateMesh(Transform source)
    {
        var renderers = source.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return false;
        var bounds = new Bounds(); var found = false;
        foreach (var renderer in renderers)
        {
            // Do not partially hide a skinned/combined cockpit that cannot be
            // reproduced as an isolated control with ordinary mesh renderers.
            if (renderer is not MeshRenderer || renderer.GetComponent<MeshFilter>()?.sharedMesh == null) return false;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        return found && bounds.size.magnitude > .01f && bounds.size.magnitude < 1.5f &&
            Mathf.Abs(source.lossyScale.x) > .001f && Mathf.Abs(source.lossyScale.y) > .001f && Mathf.Abs(source.lossyScale.z) > .001f;
    }
    private Bounds Bounds()
    {
        var result = new Bounds(); var found = false;
        foreach (var renderer in _copies)
        {
            var bounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var point = _root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                        if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
                    }
        }
        return result;
    }
    internal void SetVisible(bool visible)
    {
        _visible = visible;
        foreach (var original in _originals)
            if (original.renderer != null) original.renderer.forceRenderingOff = visible || original.hidden;
    }
    internal void Highlight(int state)
    {
        if (_lastHighlight == state) return;
        _lastHighlight = state;
        for (var i = 0; i < _copies.Count; i++)
        {
            var renderer = _copies[i];
            renderer.SetPropertyBlock(_originalBlocks[i]);
            if (state == 0) continue;
            renderer.GetPropertyBlock(_highlight);
            var material = renderer.sharedMaterial;
            var baseColor = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            _highlight.SetColor("_BaseColor", Color.Lerp(baseColor, state == 2 ? new Color(.25f, 1, .5f) : new Color(1, .75f, .2f), .4f));
            renderer.SetPropertyBlock(_highlight);
        }
    }
    public void Dispose()
    {
        if (_visible) SetVisible(false);
        if (_root != null) Object.Destroy(_root.gameObject);
    }
}
