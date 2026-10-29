using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Reuse the native HUD's already-generated meshes, sprites and text. No HUD
// scripts or target selection run twice, and no recursive UI camera is needed.
internal sealed class ZoomTargetingHud : MonoBehaviour
{
    private readonly Dictionary<HudElementStyle, ZoomHudGraphic> _copies = new();
    private readonly List<HudElementStyle> _removed = new();
    internal float Magnification = 2;
    private const float Distance = 3;
    private void OnEnable() => Canvas.willRenderCanvases += Draw;
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= Draw;
        foreach (var copy in _copies.Values) if (copy != null) copy.canvasRenderer.Clear();
    }
    private void Draw()
    {
        var camera = APIBus.CockpitHudCamera;
        if (camera == null || !isActiveAndEnabled) return;
        _removed.Clear();
        foreach (var pair in _copies)
            if (pair.Key == null || !HudElementStyle.Elements.Contains(pair.Key)) _removed.Add(pair.Key!);
        foreach (var key in _removed) { Destroy(_copies[key].gameObject); _copies.Remove(key); }
        foreach (var source in HudElementStyle.Elements)
        {
            if (source == null || !source.isActiveAndEnabled || !source.Source.isActiveAndEnabled) continue;
            if (!_copies.ContainsKey(source))
            {
                var go = new GameObject("Zoom " + source.Source.name, typeof(RectTransform));
                go.transform.SetParent(transform, false); go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(1200, 750);
                var copy = go.AddComponent<ZoomHudGraphic>(); copy.raycastTarget = false;
                _copies.Add(source, copy);
            }
        }
        // Compact weapon instruments have a stable slot at the right edge of
        // the magnifier. Their user scale still controls the allotted area.
        var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min; var widgets = false;
        foreach (var pair in _copies)
        {
            var live = pair.Key != null && pair.Key.isActiveAndEnabled && pair.Key.Source.isActiveAndEnabled &&
                       pair.Key.Source.canvasRenderer.GetInheritedAlpha() > .001f;
            pair.Value.Prepare(live ? pair.Key : null, camera.transform, Magnification, Distance);
            if (!live || pair.Key!.Kind != HudElementKind.Weapon) continue;
            if (pair.Value.TryBounds(out var low, out var high))
            { min = Vector2.Min(min, low); max = Vector2.Max(max, high); widgets = true; }
        }
        var span = max - min;
        var widgetScale = widgets ? HudPresentationMath.WidgetFit(span.x, span.y, ModConfiguration.Instance.WeaponHudScale.Value) : 1;
        foreach (var pair in _copies)
            pair.Value.Draw(pair.Key != null && pair.Key.Kind == HudElementKind.Weapon, (min + max) * .5f, widgetScale);
    }
}

internal sealed class ZoomHudGraphic : MaskableGraphic
{
    private Mesh? _mesh;
    internal readonly List<Vector3> Points = new();
    private bool _ready;
    private readonly List<Vector3> _normals = new();
    private readonly List<Vector4> _tangents = new(), _uv = new();
    private readonly List<Color32> _colors = new();
    private readonly List<int> _indices = new();
    protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    internal void Prepare(HudElementStyle? source, Transform camera, float magnification, float distance)
    {
        Points.Clear(); _ready = false;
        if (source == null) { canvasRenderer.Clear(); return; }
        _mesh ??= new Mesh { name = "NOVR zoom HUD mesh" };
        var generated = source.Source.canvasRenderer.GetMesh();
        if (generated == null || generated.vertexCount == 0) { canvasRenderer.Clear(); return; }
        generated.GetVertices(Points);
        _mesh.Clear(); _mesh.SetVertices(Points);
        generated.GetNormals(_normals); _mesh.SetNormals(_normals);
        generated.GetTangents(_tangents); _mesh.SetTangents(_tangents);
        generated.GetColors(_colors); _mesh.SetColors(_colors);
        for (var channel = 0; channel < 4; channel++) { generated.GetUVs(channel, _uv); _mesh.SetUVs(channel, _uv); }
        generated.GetTriangles(_indices, 0); _mesh.SetTriangles(_indices, 0, false);
        var anchor = camera.InverseTransformPoint(source.transform.TransformPoint(source.Anchor));
        if (anchor.z < .1f) { Points.Clear(); canvasRenderer.Clear(); return; }
        Vector3 Project(Vector3 p, float zoom) => new Vector3(HudPresentationMath.Project(p.x, p.z, distance, zoom), HudPresentationMath.Project(p.y, p.z, distance, zoom), 0);
        var projectedAnchor = Project(anchor, magnification);
        for (var i = 0; i < Points.Count; i++)
        {
            var p = camera.InverseTransformPoint(source.transform.TransformPoint(Points[i]));
            if (source.Kind == HudElementKind.Weapon) Points[i] = Project(p, 1);
            else if (source.Kind == HudElementKind.WorldGeometry) Points[i] = Project(p, magnification);
            else if (source.Kind == HudElementKind.HorizontalLine || source.Kind == HudElementKind.VerticalLine)
            {
                // Only the line's thickness is held at its normal apparent size.
                var v = Points[i]; var a = source.Anchor;
                if (source.Kind == HudElementKind.HorizontalLine) v.y = a.y + (v.y - a.y) / magnification;
                else v.x = a.x + (v.x - a.x) / magnification;
                Points[i] = Project(camera.InverseTransformPoint(source.transform.TransformPoint(v)), magnification);
            }
            else
            {
                var projected = Project(p, magnification);
                Points[i] = new Vector3(HudPresentationMath.Symbol(projected.x, projectedAnchor.x, magnification),
                    HudPresentationMath.Symbol(projected.y, projectedAnchor.y, magnification), 0);
            }
        }
        material = source.Source.material;
        canvasRenderer.SetMaterial(materialForRendering, 0);
        canvasRenderer.SetTexture(source.Source.mainTexture);
        var color = source.Source.canvasRenderer.GetColor(); color.a = source.Source.canvasRenderer.GetInheritedAlpha();
        canvasRenderer.SetColor(color);
        _ready = true;
    }
    internal bool TryBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue); max = -min; var found = false;
        if (!_ready) return false;
        // TMP keeps spare zeroed vertices in its buffers. Ignore degenerate
        // triangles so text capacity does not change a weapon widget's size.
        for (var i = 0; i + 2 < _indices.Count; i += 3)
        {
            var a = Points[_indices[i]]; var b = Points[_indices[i + 1]]; var c = Points[_indices[i + 2]];
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < .000001f) continue;
            min = Vector2.Min(min, Vector2.Min(a, Vector2.Min(b, c)));
            max = Vector2.Max(max, Vector2.Max(a, Vector2.Max(b, c))); found = true;
        }
        return found;
    }
    internal void Draw(bool widget, Vector2 center, float scale)
    {
        if (!_ready || _mesh == null) return;
        if (widget)
            for (var i = 0; i < Points.Count; i++) Points[i] = (Points[i] - (Vector3)center) * scale + new Vector3(420, 0, 0);
        _mesh.SetVertices(Points); _mesh.RecalculateBounds(); canvasRenderer.SetMesh(_mesh);
    }
    protected override void OnDestroy() { if (_mesh != null) Destroy(_mesh); base.OnDestroy(); }
}
