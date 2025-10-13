using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pins this RectTransform to a chosen corner of the *platform safe area*
/// but keeps it in its current parent (no reparent). Great for small corner
/// buttons like Pause, Back, Options, etc., even when the parent uses Layout Groups.
///
/// ORIENTATION NOTES
/// - Uses Screen.safeArea corner in *screen space*, converts to Canvas space, then to parent-local.
/// - Updates on orientation/resolution/safe area changes; can also run every frame if you want.
///
/// TIPS
/// - If the element gets moved by a Layout Group, enable "forceIgnoreLayout".
/// - If your canvas is ScreenSpace-Overlay, camera is unnecessary; otherwise we use the canvas camera.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaDock : MonoBehaviour
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Docking")]
    public Corner corner = Corner.TopRight;

    [Tooltip("Inward padding from the chosen safe-area corner (canvas units).")]
    public Vector2 padding = new Vector2(24f, 24f);

    [Tooltip("Re-apply every frame (handy during animated layout changes or editor preview).")]
    public bool updateEveryFrame = true;

    [Tooltip("Force LayoutElement.ignoreLayout so parent Layout Groups can't reposition this.")]
    public bool forceIgnoreLayout = true;

    RectTransform _rt;
    Canvas _canvas;
    RectTransform _canvasRT;

    Rect _lastSafe;
    Vector2Int _lastScreen;
    ScreenOrientation _lastOrientation;

    void OnEnable() { Init(); ApplyCorner(true); }
    void OnValidate() { Init(); ApplyCorner(true); }
    void Update()
    {
        if (updateEveryFrame || HasChanged())
            ApplyCorner(false);
    }

    void Init()
    {
        if (!_rt) _rt = GetComponent<RectTransform>();
        if (_rt && !_canvas) _canvas = _rt.GetComponentInParent<Canvas>();
        if (_canvas && !_canvasRT) _canvasRT = _canvas.transform as RectTransform;

        if (forceIgnoreLayout)
        {
            var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
    }

    bool HasChanged()
    {
        var sa = Screen.safeArea;
        var sz = new Vector2Int(Screen.width, Screen.height);
        bool changed =
            sa != _lastSafe ||
            sz != _lastScreen ||
            Screen.orientation != _lastOrientation;

        if (changed)
        {
            _lastSafe = sa;
            _lastScreen = sz;
            _lastOrientation = Screen.orientation;
        }
        return changed;
    }

    void ApplyCorner(bool force)
    {
        if (_rt == null || _canvasRT == null) return;

        var parent = _rt.parent as RectTransform;
        if (!parent) return;

        // 1) Get safe-area corner in SCREEN space
        Rect sa = Screen.safeArea;
        Vector2 screenPt = new Vector2(
            (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? sa.xMin : sa.xMax,
            (corner == Corner.BottomLeft || corner == Corner.BottomRight) ? sa.yMin : sa.yMax
        );

        // 2) Screen -> CANVAS local
        Camera cam = (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screenPt, cam, out var canvasLocal);

        // 3) CANVAS local -> PARENT local
        Vector3 worldOnCanvas = _canvasRT.TransformPoint(canvasLocal);
        Vector2 parentLocal = parent.InverseTransformPoint(worldOnCanvas);

        // 4) Compute normalized anchor (0..1) within parent rect for that point
        Rect pr = parent.rect;
        float ax = Mathf.Approximately(pr.width, 0f) ? 0.5f : Mathf.InverseLerp(pr.xMin, pr.xMax, parentLocal.x);
        float ay = Mathf.Approximately(pr.height, 0f) ? 0.5f : Mathf.InverseLerp(pr.yMin, pr.yMax, parentLocal.y);
        Vector2 anchor = new Vector2(ax, ay);

        // 5) Set anchors & pivot to that point, then offset inward by padding
        _rt.anchorMin = _rt.anchorMax = anchor;

        // Pivot toward the chosen corner (affects padding sign)
        Vector2 pivot = new Vector2(
            (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? 0f : 1f,
            (corner == Corner.BottomLeft || corner == Corner.BottomRight) ? 0f : 1f
        );
        _rt.pivot = pivot;

        // Inward padding from edges based on the corner
        float dx = (pivot.x < 0.5f) ? +padding.x : -padding.x;
        float dy = (pivot.y < 0.5f) ? +padding.y : -padding.y;

        // Keep size; only move it
        _rt.anchoredPosition = new Vector2(dx, dy);
    }
}
