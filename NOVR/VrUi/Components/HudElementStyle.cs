using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

internal enum HudElementKind { Marker, Label, Reticle, Weapon, HorizontalLine, VerticalLine, WorldGeometry }

// Change drawn vertices, not transforms used by the game's aim/lead calculations.
// TMP generates its own meshes, so handle it at its pre-render callback instead
// of scaling its RectTransform or compounding changes to last frame's vertices.
internal sealed class HudElementStyle : BaseMeshEffect
{
    internal static readonly HashSet<HudElementStyle> Elements = new();
    internal HudElementKind Kind;
    internal Transform? Group;
    internal Graphic Source => graphic;
    private TMP_Text? _text;
    private float _lastScale = -1;
    private Vector3 _lastAnchor;
    internal float Scale => Kind switch
    {
        HudElementKind.Marker => ModConfiguration.Instance.MarkerScale.Value,
        HudElementKind.Label => ModConfiguration.Instance.MarkerLabelScale.Value,
        HudElementKind.Weapon => ModConfiguration.Instance.WeaponHudScale.Value,
        HudElementKind.WorldGeometry => 1,
        _ => ModConfiguration.Instance.ReticleScale.Value
    };
    internal Vector3 Anchor => Group == null ? Vector3.zero : transform.InverseTransformPoint(Group.position);
    internal static void Add(Graphic? graphic, HudElementKind kind, Transform? group = null)
    {
        if (graphic == null) return;
        var style = graphic.GetComponent<HudElementStyle>();
        if (style == null) style = graphic.gameObject.AddComponent<HudElementStyle>();
        style.Kind = kind; style.Group = group;
    }
    protected override void OnEnable()
    {
        base.OnEnable(); Elements.Add(this);
        _text = GetComponent<TMP_Text>();
        if (_text != null) _text.OnPreRenderText += StyleText;
        _lastScale = -1;
    }
    protected override void OnDisable()
    {
        if (_text != null) { _text.OnPreRenderText -= StyleText; _text.havePropertiesChanged = true; }
        Elements.Remove(this); base.OnDisable();
    }
    private void LateUpdate()
    {
        var anchor = Anchor; var scale = Scale;
        if (scale == _lastScale && (anchor - _lastAnchor).sqrMagnitude < .0001f) return;
        _lastScale = scale; _lastAnchor = anchor;
        if (_text != null) _text.havePropertiesChanged = true;
        else graphic.SetVerticesDirty();
    }
    private Vector3 Style(Vector3 vertex)
    {
        var anchor = Anchor; var delta = vertex - anchor; var scale = Scale;
        if (Kind != HudElementKind.HorizontalLine) delta.x = HudPresentationMath.ScaleCoordinate(vertex.x, anchor.x, scale) - anchor.x;
        if (Kind != HudElementKind.VerticalLine) delta.y = HudPresentationMath.ScaleCoordinate(vertex.y, anchor.y, scale) - anchor.y;
        return anchor + delta;
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || _text != null) return;
        var v = new UIVertex();
        for (var i = 0; i < vertices.currentVertCount; i++)
        { vertices.PopulateUIVertex(ref v, i); v.position = Style(v.position); vertices.SetUIVertex(v, i); }
    }
    private void StyleText(TMP_TextInfo info)
    {
        if (!IsActive()) return;
        for (var m = 0; m < info.meshInfo.Length; m++)
        {
            var mesh = info.meshInfo[m];
            for (var i = 0; i < mesh.vertexCount; i++) mesh.vertices[i] = Style(mesh.vertices[i]);
        }
    }
}
