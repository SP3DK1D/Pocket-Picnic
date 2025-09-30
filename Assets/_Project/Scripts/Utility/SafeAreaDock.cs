using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Pins this RectTransform to a Screen.safeArea corner IN ITS CURRENT PARENT,
    /// without reparenting. Works inside layout groups (forces Ignore Layout).
    /// Put this on the small corner button/icon INSIDE each panel that should show it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaDock : MonoBehaviour
    {
        public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

        [Header("Docking")]
        public Corner corner = Corner.TopRight;
        public Vector2 padding = new Vector2(24f, 24f);   // inward from edges
        public bool updateEveryFrame = true;

        [Tooltip("Force LayoutElement.ignoreLayout so LayoutGroups can't reposition this.")]
        public bool forceIgnoreLayout = true;

        RectTransform _rt;
        Canvas _canvas;
        RectTransform _canvasRT;

        void OnEnable() { Init(); ApplyCorner(); }
        void OnValidate() { Init(); ApplyCorner(); }
        void Update() { if (updateEveryFrame) ApplyCorner(); }

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

        void ApplyCorner()
        {
            if (!_rt || !_canvasRT) return;

            var parent = _rt.parent as RectTransform;
            if (!parent) return;

            // 1) pick the safe-area corner in SCREEN space
            Rect sa = Screen.safeArea;
            Vector2 screenPt = new Vector2(
                (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? sa.xMin : sa.xMax,
                (corner == Corner.BottomLeft || corner == Corner.BottomRight) ? sa.yMin : sa.yMax
            );

            // 2) screen -> CANVAS local
            Camera cam = (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screenPt, cam, out var canvasLocal);

            // 3) CANVAS local -> PARENT local
            Vector3 worldOnCanvas = _canvasRT.TransformPoint(canvasLocal);
            Vector2 parentLocal = parent.InverseTransformPoint(worldOnCanvas);

            // 4) compute normalized anchor (0..1) within parent rect at that point
            Rect pr = parent.rect;
            float ax = Mathf.Approximately(pr.width, 0f) ? 0.5f : Mathf.InverseLerp(pr.xMin, pr.xMax, parentLocal.x);
            float ay = Mathf.Approximately(pr.height, 0f) ? 0.5f : Mathf.InverseLerp(pr.yMin, pr.yMax, parentLocal.y);

            // 5) set anchors & pivot to that point, then offset inward by padding
            Vector2 anchor = new Vector2(ax, ay);
            _rt.anchorMin = _rt.anchorMax = anchor;

            // pivot toward the chosen corner (affects the sign of padding)
            Vector2 pivot = new Vector2(
                (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? 0f : 1f,
                (corner == Corner.BottomLeft || corner == Corner.BottomRight) ? 0f : 1f
            );
            _rt.pivot = pivot;

            // inward padding from the edges based on the corner
            float dx = (pivot.x < 0.5f) ? +padding.x : -padding.x;
            float dy = (pivot.y < 0.5f) ? +padding.y : -padding.y;
            _rt.anchoredPosition = new Vector2(dx, dy);
        }
    }
}
