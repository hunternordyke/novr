using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.MotionControls;

// Preserve the native designator transform used by selection. Only replace its
// visual: a quiet center dot, with four short ticks when a candidate is in range.
[DefaultExecutionOrder(110)]
internal sealed class TargetAcquisitionCue : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<CombatHUD, List<HUDUnitMarker>> Markers = AccessTools.FieldRefAccess<CombatHUD, List<HUDUnitMarker>>("markers");
    private Image? _native;
    private CanvasGroup? _nativeGroup;
    private float _originalAlpha;
    private GameObject? _view, _ticks;
    private Canvas? _canvas;
    private CanvasGroup? _group;
    private float _opacity;
    private float _nextCandidateCheck;
    private bool _candidate;

    private void LateUpdate()
    {
        var hud = SceneSingleton<CombatHUD>.i;
        var native = hud?.targetDesignator;
        if (native != _native)
        {
            Restore(); _native = native;
            if (native != null)
            {
                _nativeGroup = native.GetComponent<CanvasGroup>() ?? native.gameObject.AddComponent<CanvasGroup>();
                _originalAlpha = _nativeGroup.alpha;
            }
        }
        if (!XRSettings.enabled || NOUIManager.I == null) { Restore(); Hide(); return; }
        if (_nativeGroup != null) _nativeGroup.alpha = 0;
        var ready = hud != null && hud.aircraft != null && _native != null && _native.isActiveAndEnabled &&
            CameraStateManager.cameraMode == CameraMode.cockpit && hud.aircraft.cockpit != null && !hud.aircraft.cockpit.IsDetached() &&
            GameManager.flightControlsEnabled && Time.timeScale > 0 && !WristMenu.IsOpen && !CockpitLayout.Active && !DynamicMap.mapMaximized &&
            !CursorManager.GetFlag(CursorFlags.GameMenu) && (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            !OpticalTargetingHud.HeadAimActive && !CockpitViewZoom.IsActive;
        if (!ready) _candidate = false;
        if (ready && Time.unscaledTime >= _nextCandidateCheck)
        {
            _nextCandidateCheck = Time.unscaledTime + .08f; _candidate = false;
            var exclusions = SceneSingleton<TargetListSelector>.i;
            foreach (var marker in Markers(hud!))
                if (marker.unit != null && !marker.selected && !marker.unit.disabled && marker.image != null && marker.image.isActiveAndEnabled &&
                    marker.image.canvasRenderer.GetInheritedAlpha() > .01f && exclusions != null && !exclusions.CheckExclusions(marker.unit) &&
                    Vector3.SqrMagnitude(marker.image.transform.position - _native!.transform.position) <= 10000)
                { _candidate = true; break; }
        }
        var candidate = _candidate;
        var selecting = ready && (ControllerActions.Instance?.TargetSelecting == true || GameManager.playerInput.GetButton("Select"));
        var show = TargetCueVisibility.Show(ModConfiguration.Instance.TargetCue.Value, ready, candidate, selecting);
        var desired = show ? candidate || selecting ? .8f : .25f : 0;
        _opacity = Mathf.MoveTowards(_opacity, desired, Time.unscaledDeltaTime * 6);
        if (!ready || _opacity <= .001f) { Hide(); return; }
        var camera = APIBus.CockpitHudCamera;
        var direction = _native!.transform.position - camera.transform.position;
        if (Vector3.Dot(direction, camera.transform.forward) <= 0) { Hide(); return; }
        EnsureView(camera);
        _canvas!.worldCamera = camera; _group!.alpha = _opacity;
        _ticks!.SetActive(candidate || selecting);
        _view!.transform.SetPositionAndRotation(camera.transform.position + direction.normalized * 3, camera.transform.rotation);
        _view.SetActive(true);
    }
    private void EnsureView(Camera camera)
    {
        if (_view != null) return;
        _view = new GameObject("NOVR contextual target cue", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _view.transform.SetParent(transform, false); _view.transform.localScale = Vector3.one * .001f;
        _canvas = _view.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = camera; _canvas.overrideSorting = true; _canvas.sortingOrder = 180;
        _group = _view.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false;
        Image Mark(Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject("Aim cue", typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var mark = rect.gameObject.AddComponent<Image>(); mark.color = new Color(.45f, .95f, .6f, 1); mark.raycastTarget = false;
            return mark;
        }
        Mark(_view.transform, Vector2.zero, new Vector2(3, 3));
        _ticks = new GameObject("Acquisition ticks", typeof(RectTransform)); _ticks.transform.SetParent(_view.transform, false);
        Mark(_ticks.transform, new Vector2(-14, 0), new Vector2(6, 2));
        Mark(_ticks.transform, new Vector2(14, 0), new Vector2(6, 2));
        Mark(_ticks.transform, new Vector2(0, -14), new Vector2(2, 6));
        Mark(_ticks.transform, new Vector2(0, 14), new Vector2(2, 6));
        LayerHelper.SetLayerRecursive(_view.transform, LayerHelper.GetVrUiLayer());
    }
    private void Restore() { if (_nativeGroup != null) _nativeGroup.alpha = _originalAlpha; }
    private void Hide() { if (_view != null) _view.SetActive(false); }
    private void OnDisable() { Restore(); Hide(); }
    private void OnDestroy() { OnDisable(); if (_view != null) Destroy(_view); }
}
