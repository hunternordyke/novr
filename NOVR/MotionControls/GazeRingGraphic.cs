using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Both gaze views use the same angular cue. At their 3 m stereo depth and
// 0.001 canvas scale, 84 units spans 1.6 degrees. Keep the center completely
// empty; a dark rim maintains contrast against sky and bright sensor imagery.
internal sealed class GazeRingGraphic : MaskableGraphic
{
    internal static GameObject Create(Transform parent)
    {
        var go = new GameObject("Hollow gaze circle", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * 84;
        var ring = go.AddComponent<GazeRingGraphic>();
        ring.color = Color.white; ring.raycastTarget = false;
        return go;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        var radius = Mathf.Min(rect.width, rect.height) * .5f;
        if (radius <= 0) return;
        Ring(vh, rect.center, radius * (36f / 42), radius, new Color(0, 0, 0, .85f));
        Ring(vh, rect.center, radius * (37.8f / 42), radius * (40.2f / 42), color);
    }

    private static void Ring(VertexHelper vh, Vector2 center, float inner, float outer, Color color)
    {
        const int segments = 64;
        var start = vh.currentVertCount;
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * Mathf.PI * 2 / segments;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(center + direction * inner, color, Vector2.zero);
            vh.AddVert(center + direction * outer, color, Vector2.zero);
            if (i == segments) continue;
            var index = start + i * 2;
            vh.AddTriangle(index, index + 2, index + 1);
            vh.AddTriangle(index + 1, index + 2, index + 3);
        }
    }
}
