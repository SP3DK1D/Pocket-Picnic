using UnityEngine;

/// <summary>
/// Resizes this RectTransform to exactly match the platform safe area (with optional extra padding).
/// Works in Edit Mode so you can preview on device simulators / Game view aspect presets.
///
/// ORIENTATION NOTES
/// - We recompute on: orientation change, resolution change, or safe area change.
/// - "Safe area" already accounts for notches, island, rounded corners, and some gesture bars.
/// - If your Canvas uses a CanvasScaler, this component still works (it only modifies anchors/offsets).
///
/// USAGE
/// - Put this on a panel you want to be "the safe content root".
/// - Place corner buttons INSIDE this panel and anchor normally, OR use SafeAreaDock for precise corners.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    [Tooltip("Extra padding applied inside the safe area (canvas units).")]
    public Vector2 extraPadding = Vector2.zero;

    [Tooltip("Re-apply every frame in Play Mode (usually unnecessary).")]
    public bool updateEveryFrame = false;

    RectTransform _rt;
    Rect _lastSafe;
    Vector2Int _lastScreen;
    ScreenOrientation _lastOrientation;

    void OnEnable()
    {
        _rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
#if UNITY_EDITOR
        // In Editor we re-apply often so resizing the Game view updates immediately
        if (updateEveryFrame || HasChanged()) Apply();
#else
        if (updateEveryFrame || HasChanged()) Apply();
#endif
    }

    /// <summary>Call this if your UI system changes Canvas hierarchy at runtime.</summary>
    public void Refresh() => Apply();

    bool HasChanged()
    {
        if (_rt == null) return false;

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

    void Apply()
    {
        if (_rt == null) return;

        // Guard: editor can report 0 before layouts settle
        float sw = Mathf.Max(1f, (float)Screen.width);
        float sh = Mathf.Max(1f, (float)Screen.height);
        Rect sa = Screen.safeArea;

        // Convert absolute screen rect -> normalized anchors
        Vector2 amin = sa.position;
        Vector2 amax = sa.position + sa.size;
        amin.x /= sw; amin.y /= sh;
        amax.x /= sw; amax.y /= sh;

        _rt.anchorMin = amin;
        _rt.anchorMax = amax;
        _rt.pivot = new Vector2(0.5f, 0.5f);

        // Expand/shrink inside safe area using offsets
        _rt.sizeDelta = Vector2.zero;
        _rt.offsetMin = new Vector2(+extraPadding.x, +extraPadding.y);
        _rt.offsetMax = new Vector2(-extraPadding.x, -extraPadding.y);
    }
}
