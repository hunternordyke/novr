using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Runtime primitives keep the prototype self-contained: no Unity editor project or asset bundle.
internal sealed class MotionControlVisuals : IDisposable
{
    public Transform Root { get; }
    private readonly Transform _stickBase, _stickPivot, _throttleBase, _throttleHandle;
    private readonly Transform _stickHand, _throttleHand, _rail;
    private readonly Renderer _stickGrip, _throttleGrip;
    private readonly Material _baseMaterial, _idleMaterial, _hoverMaterial, _heldMaterial, _handMaterial;
    private readonly TextMesh _stickLabel, _throttleLabel;
    private int _lastThrottlePercent = -1;
    private bool? _lastStickHeld, _lastThrottleHeld;

    public MotionControlVisuals(Transform trackingRoot)
    {
        Root = Child("NOVR Motion Flight Controls", trackingRoot);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        _baseMaterial = Material(shader, new Color(0.12f, 0.15f, 0.19f));
        _idleMaterial = Material(shader, new Color(0.35f, 0.65f, 0.8f));
        _hoverMaterial = Material(shader, new Color(1f, 0.75f, 0.18f));
        _heldMaterial = Material(shader, new Color(0.2f, 1f, 0.5f));
        _handMaterial = Material(shader, new Color(0.85f, 0.9f, 1f));

        _stickBase = Child("Stick", Root);
        Shape("Base", PrimitiveType.Cylinder, _stickBase, Vector3.zero, new Vector3(0.12f, 0.015f, 0.12f), _baseMaterial);
        _stickPivot = Child("Pivot", _stickBase);
        Shape("Shaft", PrimitiveType.Cylinder, _stickPivot, new Vector3(0, 0.05f, 0), new Vector3(0.025f, 0.05f, 0.025f), _idleMaterial);
        _stickGrip = Shape("Grip", PrimitiveType.Capsule, _stickPivot, new Vector3(0, 0.12f, 0), new Vector3(0.05f, 0.04f, 0.05f), _idleMaterial);
        Shape("Top", PrimitiveType.Sphere, _stickPivot, new Vector3(0, 0.155f, 0), new Vector3(0.025f, 0.012f, 0.025f), _baseMaterial);
        _stickLabel = Label("STICK / GRIP", _stickBase, new Vector3(0, 0.02f, 0.10f));

        _throttleBase = Child("Throttle", Root);
        _rail = Shape("Rail", PrimitiveType.Cube, _throttleBase, new Vector3(0, -0.05f, 0), new Vector3(0.1f, 0.02f, 0.3f), _baseMaterial).transform;
        _throttleHandle = Child("Handle", _throttleBase);
        Shape("Post", PrimitiveType.Cube, _throttleHandle, new Vector3(0, -0.025f, 0), new Vector3(0.025f, 0.05f, 0.025f), _idleMaterial);
        _throttleGrip = Shape("Grip", PrimitiveType.Cube, _throttleHandle, Vector3.zero, new Vector3(0.1f, 0.045f, 0.05f), _idleMaterial);
        _throttleLabel = Label("THROTTLE / GRIP", _throttleBase, new Vector3(0, 0.02f, 0.20f));

        _stickHand = Shape("Stick Hand", PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one * 0.025f, _handMaterial).transform;
        _throttleHand = Shape("Throttle Hand", PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one * 0.025f, _handMaterial).transform;
    }

    public void Update(Vector3 stick, Vector3 throttle, float travel, float angle, float pitch, float roll,
        float thrust, bool stickHeld, bool throttleHeld, bool stickNear, bool throttleNear,
        bool stickTracked, Vector3 stickHand, bool throttleTracked, Vector3 throttleHand)
    {
        _stickBase.localPosition = stick - Vector3.up * 0.12f;
        _stickPivot.localRotation = Quaternion.Euler(pitch * angle, 0f, -roll * angle);
        _throttleBase.localPosition = throttle;
        _rail.localScale = new Vector3(0.1f, 0.02f, travel + 0.05f);
        _throttleHandle.localPosition = Vector3.forward * ((thrust - 0.5f) * travel);
        _stickGrip.sharedMaterial = stickHeld ? _heldMaterial : stickNear ? _hoverMaterial : _idleMaterial;
        _throttleGrip.sharedMaterial = throttleHeld ? _heldMaterial : throttleNear ? _hoverMaterial : _idleMaterial;
        _stickHand.gameObject.SetActive(stickTracked);
        _stickHand.localPosition = stickHand;
        _throttleHand.gameObject.SetActive(throttleTracked);
        _throttleHand.localPosition = throttleHand;
        if (_lastStickHeld != stickHeld)
        {
            _stickLabel.text = stickHeld ? "STICK / HELD" : "STICK / GRIP";
            _lastStickHeld = stickHeld;
        }
        var percent = Mathf.RoundToInt(thrust * 100);
        if (_lastThrottlePercent != percent || _lastThrottleHeld != throttleHeld)
        {
            _throttleLabel.text = $"THROTTLE {percent}%\n{(throttleHeld ? "HELD" : "GRIP")}";
            _lastThrottlePercent = percent;
            _lastThrottleHeld = throttleHeld;
        }
    }

    private static Transform Child(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.layer = (int)LayerHelper.Layers.Cockpit;
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Renderer Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.layer = (int)LayerHelper.Layers.Cockpit;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        var collider = shape.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        var renderer = shape.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Material Material(Shader shader, Color color)
    {
        var material = new Material(shader) { enableInstancing = true };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    private static TextMesh Label(string value, Transform parent, Vector3 position)
    {
        var text = Child("Label", parent).gameObject.AddComponent<TextMesh>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.fontSize = 40;
        text.characterSize = 0.008f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        text.transform.localPosition = position;
        text.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        return text;
    }

    public void Dispose()
    {
        if (Root != null) Object.Destroy(Root.gameObject);
        Object.Destroy(_baseMaterial);
        Object.Destroy(_idleMaterial);
        Object.Destroy(_hoverMaterial);
        Object.Destroy(_heldMaterial);
        Object.Destroy(_handMaterial);
    }
}
