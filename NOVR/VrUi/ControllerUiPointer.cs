using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NOVR.VrUi;

// Uses the controller ray itself, not a ray reconstructed from the centre eye.
// Standard UGUI pointer events keep buttons, dropdowns, scrolling and dragging working.
internal sealed class ControllerUiPointer
{
    private readonly ControllerButtonGate _trigger = new();
    private PointerEventData? _event;
    private EventSystem? _system;
    private bool _hasPose;
    private Vector3 _position;
    private Quaternion _rotation;
    private bool _left;
    internal Ray Ray { get; private set; }
    internal Vector3 HitPosition { get; private set; }
    internal bool Held => _trigger.Held;
    internal bool Pressed => _trigger.Pressed;
    internal Vector2 Scroll { get; private set; }
    internal bool OverInteractive { get; private set; }
    internal bool OverMap { get; private set; }

    internal bool Update(Camera camera)
    {
        var settings = ModConfiguration.Instance;
        var left = settings.MotionControls.LeftHanded.Value;
        var hand = TrackedControllers.Read(left);
        if (!settings.ControllerBindings.PointerEnabled.Value || !hand.Valid || EventSystem.current == null)
        {
            Cancel();
            return false;
        }
        if (_system != EventSystem.current || _left != left)
        {
            Cancel();
            _system = EventSystem.current;
            _left = left;
            _event = new PointerEventData(_system) { pointerId = -101, button = PointerEventData.InputButton.Left };
        }
        var root = camera.transform.parent;
        var position = hand.AimPosition + CockpitProfiles.SeatOffset;
        var rotation = hand.AimRotation;
        if (root != null) { position = root.TransformPoint(position); rotation = root.rotation * rotation; }
        var response = settings.ControllerBindings.PointerResponse.Value;
        var blend = !_hasPose || response <= 0 ? 1 : FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, response);
        _position = Vector3.Lerp(_position, position, blend);
        _rotation = Quaternion.Slerp(_rotation, rotation, blend);
        _hasPose = true;
        Ray = new Ray(_position, _rotation * Vector3.forward);
        var hit = Raycast(Ray, camera);
        HitPosition = hit.gameObject != null ? hit.worldPosition : Ray.GetPoint(5);
        var screen = (Vector2)camera.WorldToScreenPoint(HitPosition, Camera.MonoOrStereoscopicEye.Mono);
        var data = _event!;
        data.delta = screen - data.position;
        data.position = screen;
        data.pointerCurrentRaycast = hit;
        OverInteractive = hit.gameObject != null &&
            (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null ||
             ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) != null);
        var map = SceneSingleton<DynamicMap>.i;
        OverMap = DynamicMap.mapMaximized && hit.gameObject != null && map != null &&
                  (hit.gameObject.transform.IsChildOf(map.transform) || hit.gameObject == map.mapBackground.gameObject);
        SetHover(hit.gameObject);
        _trigger.Update(true, hand.Trigger);
        // UGUI scroll is in wheel steps. Use elapsed time, not frame count.
        Scroll = new Vector2(0, FlightControlMath.Axis(hand.Axis.y, 0.2f, 1) * 12f * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        data.scrollDelta = Scroll;
        if (_trigger.Pressed) Press();
        if (_trigger.Held) Drag();
        if (_trigger.Released) Release(false);
        if (Scroll.sqrMagnitude > 0 && !OverMap)
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.scrollHandler);
        return true;
    }

    private void SetHover(GameObject? target)
    {
        var data = _event;
        if (data == null || data.pointerEnter == target) return;
        foreach (var hovered in data.hovered)
            if (hovered != null) ExecuteEvents.Execute(hovered, data, ExecuteEvents.pointerExitHandler);
        data.hovered.Clear();
        data.pointerEnter = target;
        for (var current = target != null ? target.transform : null; current != null; current = current.parent)
        {
            ExecuteEvents.Execute(current.gameObject, data, ExecuteEvents.pointerEnterHandler);
            data.hovered.Add(current.gameObject);
        }
    }

    private void Press()
    {
        var data = _event!;
        var target = data.pointerCurrentRaycast.gameObject;
        data.eligibleForClick = true;
        data.dragging = false;
        data.useDragThreshold = true;
        data.pressPosition = data.position;
        data.pointerPressRaycast = data.pointerCurrentRaycast;
        var selected = ExecuteEvents.GetEventHandler<ISelectHandler>(target);
        if (_system!.currentSelectedGameObject != selected) _system.SetSelectedGameObject(null, data);
        data.pointerPress = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler) ??
                            ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
        data.rawPointerPress = target;
        data.clickCount = data.lastPress == data.pointerPress && Time.unscaledTime - data.clickTime < 0.3f ? data.clickCount + 1 : 1;
        data.clickTime = Time.unscaledTime;
        data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(target);
        if (data.pointerDrag != null) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.initializePotentialDrag);
    }

    private void Drag()
    {
        var data = _event!;
        if (data.pointerDrag == null) return;
        var threshold = _system!.pixelDragThreshold;
        if (!data.dragging && (!data.useDragThreshold || (data.position - data.pressPosition).sqrMagnitude >= threshold * threshold))
        {
            ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.beginDragHandler);
            data.dragging = true;
        }
        if (!data.dragging) return;
        if (data.pointerPress != data.pointerDrag)
        {
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            data.eligibleForClick = false;
            data.pointerPress = null;
            data.rawPointerPress = null;
        }
        ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.dragHandler);
    }

    private void Release(bool cancel)
    {
        var data = _event;
        if (data == null) return;
        ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
        if (!cancel)
        {
            var target = data.pointerCurrentRaycast.gameObject;
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (data.pointerPress == click && data.eligibleForClick)
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            else if (data.dragging) ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.dropHandler);
        }
        if (data.dragging) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
        data.eligibleForClick = data.dragging = false;
        data.pointerPress = data.rawPointerPress = data.pointerDrag = null;
    }

    internal void Cancel()
    {
        Release(true);
        SetHover(null);
        _trigger.Reset();
        _hasPose = false;
        OverMap = OverInteractive = false;
        Scroll = Vector2.zero;
    }

    private static RaycastResult Raycast(Ray ray, Camera camera)
    {
        var best = default(RaycastResult);
        foreach (var caster in RaycasterManager.GetRaycasters())
        {
            if (caster is not GraphicRaycaster graphics || !graphics.isActiveAndEnabled) continue;
            var canvas = graphics.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || canvas.gameObject.layer != (int)LayerHelper.GetVrUiLayer()) continue;
            var eventCamera = graphics.eventCamera ?? camera;
            foreach (var graphic in GraphicRegistry.GetGraphicsForCanvas(canvas))
            {
                if (graphic == null || !graphic.isActiveAndEnabled || !graphic.raycastTarget || graphic.depth < 0 || graphic.canvasRenderer.cull) continue;
                if (graphics.ignoreReversedGraphics && Vector3.Dot(ray.direction, graphic.transform.forward) <= 0) continue;
                if (!HitRect(ray, graphic.rectTransform, out var distance, out var point)) continue;
                var screen = (Vector2)eventCamera.WorldToScreenPoint(point, Camera.MonoOrStereoscopicEye.Mono);
                if (!graphic.Raycast(screen, eventCamera)) continue;
                var candidate = new RaycastResult
                {
                    gameObject = graphic.gameObject, module = graphics, distance = distance,
                    worldPosition = point, worldNormal = -graphic.transform.forward, screenPosition = screen,
                    depth = graphic.depth, sortingLayer = canvas.sortingLayerID, sortingOrder = canvas.sortingOrder
                };
                if (InFront(candidate, best)) best = candidate;
            }
        }
        var map = SceneSingleton<DynamicMap>.i;
        if (DynamicMap.mapMaximized && map?.mapBackground != null && map.mapBackground.gameObject.activeInHierarchy &&
            HitRect(ray, map.mapBackground.rectTransform, out var mapDistance, out var mapPoint) &&
            (best.gameObject == null || mapDistance < best.distance - 0.005f))
            best = new RaycastResult { gameObject = map.mapBackground.gameObject, distance = mapDistance, worldPosition = mapPoint };
        return best;
    }

    private static bool InFront(RaycastResult candidate, RaycastResult current)
    {
        if (current.gameObject == null || candidate.distance < current.distance - 0.005f) return true;
        if (candidate.distance > current.distance + 0.005f) return false;
        var candidateLayer = SortingLayer.GetLayerValueFromID(candidate.sortingLayer);
        var currentLayer = SortingLayer.GetLayerValueFromID(current.sortingLayer);
        if (candidateLayer != currentLayer) return candidateLayer > currentLayer;
        if (candidate.sortingOrder != current.sortingOrder) return candidate.sortingOrder > current.sortingOrder;
        return candidate.depth > current.depth;
    }

    internal static bool HitRect(Ray ray, RectTransform rect, out float distance, out Vector3 point)
    {
        var origin = rect.InverseTransformPoint(ray.origin);
        var direction = rect.InverseTransformVector(ray.direction);
        var bounds = rect.rect;
        var hit = PointerGeometry.IntersectRect(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z,
            bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax, out distance);
        point = hit ? ray.GetPoint(distance) : default;
        return hit;
    }
}
