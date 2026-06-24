using UnityEngine;

/// <summary>
/// Adjusts the RectTransform anchors to sit inside the device's physical safe area.
/// Automatically handles screen rotation, resizing, and aspect ratio changes in real time.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
    private Vector2 _lastScreenSize = new Vector2(0, 0);
    private ScreenOrientation _lastOrientation = ScreenOrientation.Unknown;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            Destroy(this);
            return;
        }
        ApplySafeArea();
    }

    private void Update()
    {
        if (_lastSafeArea != Screen.safeArea || 
            _lastScreenSize.x != Screen.width || 
            _lastScreenSize.y != Screen.height || 
            _lastOrientation != Screen.orientation)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        
        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2(Screen.width, Screen.height);
        _lastOrientation = Screen.orientation;

        // Convert safe area rectangle from screen pixels to normalized anchor coordinates
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        float screenW = Screen.width;
        float screenH = Screen.height;

        if (screenW > 0 && screenH > 0)
        {
            anchorMin.x /= screenW;
            anchorMin.y /= screenH;
            anchorMax.x /= screenW;
            anchorMax.y /= screenH;

            // Apply anchors
            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            
            // Reset offsets to match anchors exactly
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
