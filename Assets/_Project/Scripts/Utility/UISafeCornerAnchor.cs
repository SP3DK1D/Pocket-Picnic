using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Pins a RectTransform to a safe-area corner regardless of parent layout.
    /// Attach to the small corner button (NOT whole panels).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class UISafeCornerAnchor : MonoBehaviour
    {
        public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

        [Header("Corner & Padding")]
        public Corner corner = Corner.TopRight;
        public Vector2 padding = new Vector2(16f, 16f); // canvas units

        [Header("Behaviour")]
        public bool updateEveryFrame = true;
        public bool snapAnchors = true;

        [Tooltip("Force a LayoutElement(ignoreLayout=true) so LayoutGroups won't reposition this.")]
        public bool forceIgnoreLayout = true;

        RectTransform _rt;
        Canvas _rootCanvas;
        RectTransform _canvasRT;
        LayoutElement _le;

        void OnEnable() { EnsureIgnoreLayout(); Apply(); }
        void Update() { if (updateEveryFrame) Apply(); }
        void OnValidate() { EnsureIgnoreLayout(); Apply(); }

        void EnsureIgnoreLayout()
        {
            if (!forceIgnoreLayout) return;
            if (!_le) _le = GetComponent<LayoutElement>();
            if (!_le) _le = gameObject.AddComponent<LayoutElement>();
            _le.ignoreLayout = true;
        }

        void Apply()
        {
            if (!InitRefs()) return;

            // 1) screen safe-area corner (pixels)
            Rect sa = Screen.safeArea;
            Vector2 screenPt = new Vector2(
                (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? sa.xMin : sa.xMax,
                (corner == Corner.BottomLeft || corner == Corner.BottomRight) ? sa.yMin : sa.yMax
            );

            // 2) screen -> canvas local
            Camera cam = (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screenPt, cam, out var canvasLocal);

            // 3) canvas local -> parent local
            var parentRT = _rt.parent as RectTransform;
            Vector3 worldOnCanvas = _canvasRT.TransformPoint(canvasLocal);
            Vector2 parentLocal = parentRT ? (Vector2)parentRT.InverseTransformPoint(worldOnCanvas) : canvasLocal;

            // 4) pivot per corner
            Vector2 pivot = new Vector2(
                (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? 0f : 1f,
                (corner == Corner.BottomLeft || corner == Corner.BottomRight) ? 0f : 1f
            );
            _rt.pivot = pivot;

            // 5) snapshot to anchors or absolute position
            if (snapAnchors && parentRT)
            {
                Rect pr = parentRT.rect;
                float ax = Mathf.Approximately(pr.width, 0f) ? 0.5f : Mathf.InverseLerp(pr.xMin, pr.xMax, parentLocal.x);
                float ay = Mathf.Approximately(pr.height, 0f) ? 0.5f : Mathf.InverseLerp(pr.yMin, pr.yMax, parentLocal.y);
                _rt.anchorMin = _rt.anchorMax = new Vector2(ax, ay);
                _rt.anchoredPosition = new Vector2(
                    pivot.x == 0 ? +padding.x : -padding.x,
                    pivot.y == 0 ? +padding.y : -padding.y
                );
            }
            else
            {
                Vector2 anchoredPos = parentLocal + new Vector2(
                    pivot.x == 0 ? +padding.x : -padding.x,
                    pivot.y == 0 ? +padding.y : -padding.y
                );
                _rt.anchoredPosition = anchoredPos;
            }
        }

        bool InitRefs()
        {
            if (!_rt) _rt = GetComponent<RectTransform>();
            if (!_rt) return false;
            if (!_rootCanvas) _rootCanvas = _rt.GetComponentInParent<Canvas>();
            if (!_rootCanvas) return false;
            if (!_canvasRT) _canvasRT = _rootCanvas.transform as RectTransform;
            return _canvasRT;
        }
    }
}
