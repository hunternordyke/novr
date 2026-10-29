using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

internal sealed class RiggedGlove : IDisposable
{
    internal sealed class Bone { public string name = ""; public int parent = -1; public float[] position = Array.Empty<float>(); }
    internal sealed class Asset
    {
        public float[] vertices = Array.Empty<float>(), weights = Array.Empty<float>(), uv = Array.Empty<float>();
        public int[] triangles = Array.Empty<int>(), indices = Array.Empty<int>();
        public Bone[] bones = Array.Empty<Bone>();
        public float[][] open = Array.Empty<float[]>(), fist = Array.Empty<float[]>();
    }
    private static readonly Asset?[] Assets = new Asset?[2];
    internal readonly Transform Root;
    internal readonly SkinnedMeshRenderer Renderer;
    private readonly Transform[] _bones;
    private readonly Quaternion[] _open, _closed;
    private readonly int[] _fingers;
    private readonly Mesh _mesh;
    private readonly bool _left;
    private float _grip, _trigger, _thumb;
    internal static Asset Load(bool left)
    {
        var index = left ? 0 : 1;
        if (Assets[index] != null) return Assets[index]!;
        using var stream = typeof(RiggedGlove).Assembly.GetManifestResourceStream("NOVR.Glove." + (left ? "left" : "right") + ".json");
        using var reader = new StreamReader(stream ?? throw new InvalidDataException("Missing glove asset."));
        var a = JsonConvert.DeserializeObject<Asset>(reader.ReadToEnd()) ?? throw new InvalidDataException("Empty glove asset.");
        var count = a.vertices.Length / 3;
        if (count < 3 || count > 10000 || a.vertices.Length % 3 != 0 || a.bones.Length != 31 || a.triangles.Length % 3 != 0 ||
            a.uv.Length != count * 2 || a.uv.Any(v => !FlightControlMath.IsFinite(v)) || a.weights.Length != count * 4 || a.indices.Length != count * 4 || a.open.Length != 31 || a.fist.Length != 31 ||
            a.vertices.Any(v => !FlightControlMath.IsFinite(v)) || a.triangles.Any(i => i < 0 || i >= count) ||
            a.indices.Any(i => i < 0 || i >= 31) || a.weights.Any(w => !FlightControlMath.IsFinite(w) || w < 0 || w > 1)) throw new InvalidDataException("Invalid glove geometry.");
        for (var i = 0; i < 31; i++)
            if (a.bones[i].parent < -1 || a.bones[i].parent >= i || a.bones[i].position.Length != 3 || a.bones[i].position.Any(v => !FlightControlMath.IsFinite(v)) ||
                a.open[i].Length != 4 || a.fist[i].Length != 4 || a.open[i].Concat(a.fist[i]).Any(v => !FlightControlMath.IsFinite(v))) throw new InvalidDataException("Invalid glove rig.");
        for (var i = 0; i < count; i++) if (Mathf.Abs(a.weights[i*4]+a.weights[i*4+1]+a.weights[i*4+2]+a.weights[i*4+3]-1) > .001f) throw new InvalidDataException("Invalid glove skin weights.");
        return Assets[index] = a;
    }
    internal RiggedGlove(bool left, Transform parent, Material material, Rect patch)
    {
        _left = left; var asset = Load(left);
        Root = new GameObject(left ? "NOVR Left Glove" : "NOVR Right Glove").transform; Root.SetParent(parent, false);
        _bones = new Transform[asset.bones.Length]; _open = new Quaternion[_bones.Length]; _closed = new Quaternion[_bones.Length]; _fingers = new int[_bones.Length];
        var bind = new Matrix4x4[_bones.Length];
        for (var i = 0; i < _bones.Length; i++)
        {
            var b = asset.bones[i]; var t = new GameObject(b.name).transform; t.SetParent(b.parent < 0 ? Root : _bones[b.parent], false);
            t.localPosition = V(b.position, 0); _bones[i] = t; bind[i] = t.worldToLocalMatrix * Root.localToWorldMatrix;
            _open[i] = Q(asset.open[i]); _closed[i] = Q(asset.fist[i]);
            _fingers[i] = b.name.Contains("thumb") ? 1 : b.name.Contains("index") ? 2 : b.name.Contains("finger") ? 3 : 0;
        }
        var vertices = new Vector3[asset.vertices.Length / 3]; var weights = new BoneWeight[vertices.Length]; var uv = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = V(asset.vertices, i * 3); var j = i * 4;
            weights[i] = new BoneWeight { boneIndex0=asset.indices[j],boneIndex1=asset.indices[j+1],boneIndex2=asset.indices[j+2],boneIndex3=asset.indices[j+3],weight0=asset.weights[j],weight1=asset.weights[j+1],weight2=asset.weights[j+2],weight3=asset.weights[j+3] };
            uv[i] = new Vector2(asset.uv[i*2],asset.uv[i*2+1]);
        }
        _mesh = new Mesh { name = "Valve slim glove / pilot material", vertices=vertices, uv=uv, boneWeights=weights, bindposes=bind, triangles=asset.triangles };
        _mesh.RecalculateNormals(); _mesh.RecalculateTangents(); _mesh.RecalculateBounds();
        Renderer = Root.gameObject.AddComponent<SkinnedMeshRenderer>(); Renderer.sharedMesh=_mesh; Renderer.bones=_bones; Renderer.rootBone=_bones[1]; Renderer.sharedMaterial=material;
        Renderer.localBounds = new Bounds(new Vector3(0,0,.06f),Vector3.one*.45f); Renderer.shadowCastingMode=ShadowCastingMode.Off; Renderer.receiveShadows=true; Renderer.updateWhenOffscreen=false;
        LayerHelper.SetLayerRecursive(Root, LayerHelper.Layers.Cockpit);
    }
    internal void Pose(ControllerSample sample, Pose? attachment, bool stick, bool collective)
    {
        Root.gameObject.SetActive(sample.Valid);
        if (!sample.Valid) return;
        var settings=ModConfiguration.Instance.MotionControls;
        var offset=settings.GloveOffset.Value; if (!_left) offset.x=-offset.x;
        var held = attachment.HasValue;
        if (held)
        {
            // Asset axes: fingers +Z, back of hand +Y, thumb toward +/-X.
            // The socket is inside the partially curled fingers, not at the wrist.
            // Collective cuffs point up and inboard toward the shoulder; rolling
            // around the handle axis preserves the finger wrap as it is lifted.
            var grip = attachment!.Value;
            var rotation = grip.rotation * (stick ? Quaternion.Euler(0, 0, _left ? 90 : -90)
                : collective ? Quaternion.Euler(0, 0, _left ? 60 : -60) * Quaternion.Euler(0, _left ? -90 : 90, 0) : Quaternion.identity);
            Root.SetPositionAndRotation(grip.position - rotation * new Vector3(0, -.018f, .078f), rotation);
        }
        else
        {
            Root.localPosition=sample.Position + sample.Rotation * offset;
            var angles=settings.GloveRotation.Value; if (!_left) { angles.y=-angles.y; angles.z=-angles.z; }
            Root.localRotation=sample.Rotation * Quaternion.Euler(angles);
        }
        var amount=1-Mathf.Exp(-22*Time.unscaledDeltaTime);
        // Leave space for a roughly 5 cm grip instead of crushing a full fist
        // through the handle. The index moves a little with the real trigger.
        _grip=Mathf.Lerp(_grip,held ? .5f : Mathf.Clamp01(sample.Grip),amount);
        _trigger=Mathf.Lerp(_trigger,held ? Mathf.Lerp(.48f, .62f, Mathf.Clamp01(sample.Trigger)) : Mathf.Clamp01(sample.Trigger),amount);
        _thumb=Mathf.Lerp(_thumb,held ? .65f : sample.Primary || sample.Secondary || sample.StickClick ? 1 : _grip*.75f,amount);
        for(var i=0;i<_bones.Length;i++) _bones[i].localRotation=Quaternion.Slerp(_open[i],_closed[i],_fingers[i]==1?_thumb:_fingers[i]==2?_trigger:_fingers[i]==3?_grip:0);
    }
    private static Vector3 V(float[] a,int i)=>new(a[i],a[i+1],a[i+2]);
    private static Quaternion Q(float[] a)=>new Quaternion(a[0],a[1],a[2],a[3]).normalized;
    public void Dispose() { Object.Destroy(Root.gameObject); Object.Destroy(_mesh); }
}
