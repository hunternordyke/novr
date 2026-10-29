using NOVR.VrUi;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.MotionControls;

// A mono inspection image on a stereo, fixed-depth surface. Headset projection,
// eye separation and the surrounding cockpit view remain runtime-controlled.
[DefaultExecutionOrder(-1025)]
internal sealed class CockpitViewZoom : MonoBehaviour
{
    private readonly ControllerButtonGate _button = new();
    private Aircraft? _aircraft;
    private Vector3 _calibration, _anchor;
    private Quaternion _rotation;
    private Camera? _camera;
    private RenderTexture? _texture;
    private GameObject? _window;
    private Text? _label;
    private bool _held;
    private const float Distance = 3f, Width = 1.2f, Height = .75f;

    private void Update()
    {
        var settings = ModConfiguration.Instance.ControllerBindings;
        var right = TrackedControllers.Read(false);
        GameManager.GetLocalAircraft(out var aircraft);
        if (_aircraft != aircraft || _calibration != NOVRHeadsetData.TranslationCalibrationOffset ||
            _anchor != NOVRHeadsetData.TranslationAnchor || _rotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Reset(); _aircraft = aircraft; _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
            _anchor = NOVRHeadsetData.TranslationAnchor; _rotation = NOVRHeadsetData.RotationCalibrationOffset;
        }
        var available = settings.RightBumperZoom.Value && XRSettings.enabled && TrackedControllers.Available && right.Valid &&
            aircraft != null && aircraft.cockpit != null && !aircraft.cockpit.IsDetached() &&
            CameraStateManager.cameraMode == CameraMode.cockpit && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
            !WristMenu.IsOpen && !CockpitLayout.Active && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse());
        _button.Update(available, right.Shoulder ? 1 : 0);
        _held = _button.Held;
        if (!_held) Hide();
    }
    private void LateUpdate()
    {
        if (!_held) return;
        var main = APIBus.MainCamera; var ui = APIBus.CockpitHudCamera;
        if (main == null || ui == null) { Reset(); return; }
        EnsureView(main, ui);
        var magnification = ModConfiguration.Instance.ControllerBindings.ViewZoomFactor.Value;
        _camera!.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        _camera.fieldOfView = 2 * Mathf.Atan(Height / (2 * Distance * magnification)) * Mathf.Rad2Deg;
        _camera.farClipPlane = main.farClipPlane;
        _camera.enabled = true;
        _window!.transform.SetPositionAndRotation(ui.transform.position + ui.transform.forward * Distance, ui.transform.rotation);
        _window.SetActive(true);
        _label!.text = $"INSPECTION  {magnification:0.0}×   •   RELEASE RB TO CLOSE";
    }
    private void EnsureView(Camera main, Camera ui)
    {
        if (_camera != null) return;
        _texture = new RenderTexture(1024, 640, 24, RenderTextureFormat.ARGB32) { name = "NOVR inspection view", filterMode = FilterMode.Bilinear };
        _texture.Create();
        var cameraObject = new GameObject("NOVR inspection camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.transform.SetParent(transform, false); _camera = cameraObject.GetComponent<Camera>();
        _camera.CopyFrom(main); _camera.ResetProjectionMatrix(); _camera.stereoTargetEye = StereoTargetEyeMask.None;
        _camera.targetTexture = _texture; _camera.allowHDR = false; _camera.allowMSAA = false;
        _camera.nearClipPlane = .15f; _camera.depth = -50;
        // Exclude cockpit geometry and all projected UI; this is a view outside,
        // never a recursive copy of the magnifier or another native MFD feed.
        _camera.cullingMask = main.cullingMask & ~((1 << (int)LayerHelper.Layers.Cockpit) | (1 << (int)LayerHelper.GetVrUiLayer()) | (1 << 5));
        var data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        data.allowXRRendering = false; data.renderType = CameraRenderType.Base;
        var sourceData = main.GetComponent<UniversalAdditionalCameraData>();
        if (sourceData != null) { data.renderPostProcessing = sourceData.renderPostProcessing; data.volumeLayerMask = sourceData.volumeLayerMask; }
        _window = new GameObject("NOVR inspection window", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _window.transform.SetParent(transform, false); _window.transform.localScale = Vector3.one * .001f;
        var canvas = _window.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = ui;
        _window.GetComponent<CanvasGroup>().blocksRaycasts = false;
        RectTransform Rect(string name, Vector2 size, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(_window.transform, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        var background = Rect("Frame", new Vector2(Width * 1000 + 14, Height * 1000 + 56), new Vector2(0, -17)).gameObject.AddComponent<Image>();
        background.color = new Color(.015f, .025f, .035f, 1); background.raycastTarget = false;
        var picture = Rect("Magnified scene", new Vector2(Width * 1000, Height * 1000), Vector2.zero).gameObject.AddComponent<RawImage>();
        picture.texture = _texture; picture.raycastTarget = false;
        _label = Rect("Instructions", new Vector2(Width * 1000, 35), new Vector2(0, -Height * 500 - 21)).gameObject.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); _label.fontSize = 22; _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(.65f, .9f, .8f); _label.raycastTarget = false;
        LayerHelper.SetLayerRecursive(_window.transform, LayerHelper.GetVrUiLayer());
    }
    private void Hide()
    { if (_camera != null) _camera.enabled = false; if (_window != null) _window.SetActive(false); }
    private void Reset() { _button.Reset(); _held = false; Hide(); }
    private void OnDisable() => Reset();
    private void OnDestroy()
    {
        Reset(); if (_camera != null) Destroy(_camera.gameObject); if (_window != null) Destroy(_window);
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
    }
}
