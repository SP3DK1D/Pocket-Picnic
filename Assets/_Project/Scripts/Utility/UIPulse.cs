using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CatchTheFruit
{
    /// <summary>
    /// Add to a Button (or any UI object) to pulse scale/alpha.
    /// - Uses unscaled time (keeps pulsing on menus).
    /// - Pauses while hovered/pressed so the button feels responsive.
    /// - If a CanvasGroup is present, alpha pulsing uses it; otherwise it fades Graphics.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPulse : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum PulseTarget { Scale, Alpha, ScaleAndAlpha }

        [Header("Mode")]
        public PulseTarget target = PulseTarget.Scale;

        [Header("Timing")]
        [Tooltip("Pulses per second (1.2 = gentle).")]
        [Min(0.01f)] public float speed = 1.2f;
        [Tooltip("Use unscaled time so pulse works while game is paused.")]
        public bool useUnscaledTime = true;
        [Tooltip("Pause the pulse when the pointer hovers the button.")]
        public bool pauseOnHover = true;
        [Tooltip("Pause the pulse while the button is pressed.")]
        public bool pauseOnPress = true;

        [Header("Scale Pulse")]
        [Tooltip("Min overall scale (multiplier).")]
        public float scaleMin = 0.94f;
        [Tooltip("Max overall scale (multiplier).")]
        public float scaleMax = 1.06f;

        [Header("Alpha Pulse")]
        [Tooltip("Min alpha when pulsing (0..1).")]
        [Range(0f, 1f)] public float alphaMin = 0.6f;
        [Tooltip("Max alpha when pulsing (0..1).")]
        [Range(0f, 1f)] public float alphaMax = 1f;
        [Tooltip("If no CanvasGroup, fade all Graphics on this object (and children).")]
        public bool includeChildrenGraphics = true;

        // Runtime
        RectTransform _rt;
        Vector3 _baseScale = Vector3.one;
        CanvasGroup _cg;
        Graphic[] _graphics;
        float _t;
        int _pointerInside;
        bool _pressed;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (_rt) _baseScale = _rt.localScale;

            _cg = GetComponent<CanvasGroup>();
            if (!_cg && (target == PulseTarget.Alpha || target == PulseTarget.ScaleAndAlpha))
            {
                _graphics = includeChildrenGraphics
                    ? GetComponentsInChildren<Graphic>(true)
                    : GetComponents<Graphic>();
            }
        }

        void OnEnable()
        {
            _t = 0f;
        }

        void OnDisable()
        {
            // restore defaults when disabled
            if (_rt) _rt.localScale = _baseScale;

            if (target == PulseTarget.Alpha || target == PulseTarget.ScaleAndAlpha)
            {
                if (_cg) _cg.alpha = 1f;
                else if (_graphics != null)
                {
                    for (int i = 0; i < _graphics.Length; i++)
                    {
                        var g = _graphics[i];
                        if (!g) continue;
                        var c = g.color; c.a = 1f; g.color = c;
                    }
                }
            }
        }

        void Update()
        {
            // pause while hovered/pressed if requested
            if ((pauseOnHover && _pointerInside > 0) || (pauseOnPress && _pressed))
                return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _t += dt * speed;

            // 0..1 ease based on sine wave
            float k = 0.5f + 0.5f * Mathf.Sin(_t * 2f * Mathf.PI);

            if (target == PulseTarget.Scale || target == PulseTarget.ScaleAndAlpha)
            {
                if (_rt)
                {
                    float s = Mathf.Lerp(scaleMin, scaleMax, k);
                    _rt.localScale = _baseScale * s;
                }
            }

            if (target == PulseTarget.Alpha || target == PulseTarget.ScaleAndAlpha)
            {
                float a = Mathf.Lerp(alphaMin, alphaMax, k);
                if (_cg) _cg.alpha = a;
                else if (_graphics != null)
                {
                    for (int i = 0; i < _graphics.Length; i++)
                    {
                        var g = _graphics[i];
                        if (!g) continue;
                        var c = g.color; c.a = a; g.color = c;
                    }
                }
            }
        }

        // --- pointer handlers to pause pulse while interacting ---
        public void OnPointerEnter(PointerEventData eventData) { _pointerInside++; }
        public void OnPointerExit(PointerEventData eventData) { _pointerInside = Mathf.Max(0, _pointerInside - 1); }
        public void OnPointerDown(PointerEventData eventData) { _pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { _pressed = false; }
    }
}
