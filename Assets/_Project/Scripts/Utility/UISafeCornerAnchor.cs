using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Pins a RectTransform to a corner inside the device safe area (iPhone/iPad notches).
    /// Keeps the element in the corner on all aspect ratios & orientations.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class UISafeCornerAnchor : MonoBehaviour
    {
        public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

        [Header("Position")]
        public Corner corner = Corner.TopRight;
        public Vector2 padding = new Vector2(16f, 16f); // in canvas units (pre-scaled)

        [Header("Update")]
        public bool updateEveryFrame = true; // set true if your UI can resize/rotate at runtime

        RectTransform _rt;
        Canvas _canvas;

        void OnEnable() { Apply(); }
        void Update() { if (updateEveryFrame) Apply(); }
        void OnRectTransformDimensionsChange() { Apply(); }

        void Apply()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

            if (_rt == null || _canvas == null) return;

            Rect sa = Screen.safeArea;
            Vector2 anchor;

            switch (corner)
            {
                case Corner.TopLeft: anchor = new Vector2(sa.xMin / Screen.width, sa.yMax / Screen.height); _rt.pivot = new Vector2(0f, 1f); _rt.anchorMin = _rt.anchorMax = anchor; _rt.anchoredPosition = new Vector2(+padding.x, -padding.y); break;
                case Corner.TopRight: anchor = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height); _rt.pivot = new Vector2(1f, 1f); _rt.anchorMin = _rt.anchorMax = anchor; _rt.anchoredPosition = new Vector2(-padding.x, -padding.y); break;
                case Corner.BottomLeft: anchor = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height); _rt.pivot = new Vector2(0f, 0f); _rt.anchorMin = _rt.anchorMax = anchor; _rt.anchoredPosition = new Vector2(+padding.x, +padding.y); break;
                default: /* BottomRight*/anchor = new Vector2(sa.xMax / Screen.width, sa.yMin / Screen.height); _rt.pivot = new Vector2(1f, 0f); _rt.anchorMin = _rt.anchorMax = anchor; _rt.anchoredPosition = new Vector2(-padding.x, +padding.y); break;
            }
        }
    }
}
