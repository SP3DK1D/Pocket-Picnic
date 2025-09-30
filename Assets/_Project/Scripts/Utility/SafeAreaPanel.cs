using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Resizes this RectTransform to match Screen.safeArea.
    /// Place corner UI under this object and anchor normally.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        [Tooltip("Extra padding applied inside the safe area (canvas units).")]
        public Vector2 extraPadding = Vector2.zero;

        RectTransform _rt;
        Rect _lastSafe;
        Vector2Int _lastSize;
        ScreenOrientation _lastOrient;

        void OnEnable()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Changed()) Apply();
        }

        bool Changed()
        {
            if (_rt == null) return false;
            var sa = Screen.safeArea;
            var sz = new Vector2Int(Screen.width, Screen.height);
            if (sa != _lastSafe || sz != _lastSize || Screen.orientation != _lastOrient)
            {
                _lastSafe = sa;
                _lastSize = sz;
                _lastOrient = Screen.orientation;
                return true;
            }
            return false;
        }

        void Apply()
        {
            if (_rt == null) return;

            Rect sa = Screen.safeArea;
            Vector2 ss = new Vector2(Screen.width, Screen.height);
            if (ss.x <= 0f || ss.y <= 0f) return;

            // convert to normalized anchors
            Vector2 amin = sa.position;
            Vector2 amax = sa.position + sa.size;
            amin.x /= ss.x; amin.y /= ss.y;
            amax.x /= ss.x; amax.y /= ss.y;

            _rt.anchorMin = amin;
            _rt.anchorMax = amax;
            _rt.pivot = new Vector2(0.5f, 0.5f);

            _rt.sizeDelta = Vector2.zero;
            _rt.offsetMin = new Vector2(+extraPadding.x, +extraPadding.y);
            _rt.offsetMax = new Vector2(-extraPadding.x, -extraPadding.y);
        }
    }
}
