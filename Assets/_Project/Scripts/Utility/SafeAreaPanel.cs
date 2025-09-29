using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Resizes this RectTransform to match the device's Screen.safeArea.
    /// Place corner buttons (Options, Settings, etc.) INSIDE this panel and anchor them normally
    /// (e.g., top-right with small positive offsets). Works with any Canvas render mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        [Tooltip("Extra padding applied inside the safe area (canvas units).")]
        public Vector2 extraPadding = Vector2.zero;

        RectTransform _rt;
        ScreenOrientation _lastOrientation;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;

        void OnEnable()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
#if UNITY_EDITOR
            // In the editor / Device Simulator, safe area can change without orientation change
            if (_rt && (ScreenHasChanged() || SafeAreaChanged()))
                Apply();
#else
            if (_rt && (ScreenHasChanged() || SafeAreaChanged()))
                Apply();
#endif
        }

        bool ScreenHasChanged()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _lastScreenSize) { _lastScreenSize = size; return true; }
            if (Screen.orientation != _lastOrientation) { _lastOrientation = Screen.orientation; return true; }
            return false;
        }

        bool SafeAreaChanged()
        {
            var sa = Screen.safeArea;
            if (sa != _lastSafeArea) { _lastSafeArea = sa; return true; }
            return false;
        }

        void Apply()
        {
            if (_rt == null) return;

            // Get current safe area (in screen pixels)
            Rect sa = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize.x <= 0f || screenSize.y <= 0f) return;

            // Convert safe area rect (pixels) to anchor coords (0..1)
            Vector2 anchorMin = sa.position;
            Vector2 anchorMax = sa.position + sa.size;
            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            // Apply anchors to stretch this panel to the safe area
            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
            _rt.pivot = new Vector2(0.5f, 0.5f);

            // Zero sizeDelta so it exactly matches anchors; use extra padding as offsets
            _rt.sizeDelta = Vector2.zero;

            // Apply padding *inside* the safe area: positive values push inward from edges
            // We do this via offsetMin/offsetMax (left/bottom, right/top).
            _rt.offsetMin = new Vector2(+extraPadding.x, +extraPadding.y);
            _rt.offsetMax = new Vector2(-extraPadding.x, -extraPadding.y);
        }
    }
}
