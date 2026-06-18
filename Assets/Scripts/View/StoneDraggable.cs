// StoneDraggable.cs
// Allows a stone GameObject to be dragged with mouse/touch.
// When the drag ends, it optionally invokes a callback (e.g., to check for matches).

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StoneDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // Original position so we can revert if needed.
    private Vector3 _originalWorldPos;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    // Optional action called when drag ends. Assign via inspector or code.
    public System.Action<GameObject> OnDragEnd;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        // Find the root canvas for proper screen‑space conversion.
        _parentCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Store the starting world position.
        _originalWorldPos = _rectTransform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Convert the drag delta to canvas space.
        if (_parentCanvas == null) return;
        Vector2 moveDelta;
        // For ScreenSpace‑Overlay canvas, we can use eventData.delta directly.
        // For other render modes, convert using the canvas scale factor.
        moveDelta = eventData.delta / _parentCanvas.scaleFactor;
        _rectTransform.anchoredPosition += moveDelta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Invoke external logic (e.g., match checking). If none, keep at end position.
        OnDragEnd?.Invoke(gameObject);

        // Example fallback: if you want to revert when not swapped, uncomment:
        // if (!validSwap) _rectTransform.position = _originalWorldPos;
    }
}
