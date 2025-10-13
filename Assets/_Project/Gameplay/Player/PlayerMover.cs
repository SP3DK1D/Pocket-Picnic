using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CatchTheFruit
{
    /// <summary>
    /// Mobile-friendly horizontal mover for the player basket.
    /// - Reads touch/mouse X, clamps to arena width, moves a kinematic Rigidbody2D.
    /// - Snap: teleports under pointer. Smooth: eased with speed.
    /// - Uses Time.unscaledDeltaTime so Freeze doesn’t change input feel.
    /// - Robust clamps for extreme aspect ratios: prefers GameConfig.arenaHalfWidth,
    ///   but can fall back to camera width if config is missing.
    /// - Zero GC per frame in Update.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMover : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional. If null, clamps fall back to Camera width.")]
        [SerializeField] private GameConfig config;

        public enum FollowMode { Snap, Smooth }

        [Header("Follow Mode")]
        [SerializeField] private FollowMode follow = FollowMode.Snap;

        [Tooltip("Used only in Smooth mode: world-units/second toward the pointer.")]
        [Min(0f)] public float smoothSpeed = 20f;

        [Header("Input")]
        [Tooltip("If true, you must be actively touching/holding to move. If false, follows last known pointer position.")]
        public bool requirePress = true;

        [Header("Clamping & Safety")]
        [Tooltip("Extra inward margin from arena edges (world units).")]
        [Min(0f)] public float edgePadding = 0.0f;

        [Tooltip("If true, recompute camera-based clamp when resolution/aspect changes.")]
        public bool recalcClampOnResize = true;

        // ---- runtime (cached) ----
        Rigidbody2D _rb;
        Camera _cam;

        // current target x we try to reach every frame
        float _targetX;
        bool _hasTarget;

        // cached clamp
        float _halfWidthClamp;          // computed playable half-width
        int _lastScreenW, _lastScreenH; // detect size change

        // Movement speed source (Config wins when present)
        float MoveSpeed =>
            (config != null && config.playerMoveSpeed > 0f)
            ? config.playerMoveSpeed
            : (smoothSpeed > 0f ? smoothSpeed : 0f);

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.isKinematic = true; // MovePosition control, stable trigger interactions
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _cam = Camera.main;
            if (!_cam)
                Debug.LogError("[PlayerMover] No MainCamera found. Tag your camera 'MainCamera'.");

            RecomputeClamp();
        }

        void OnEnable()
        {
            // Initialize target so Smooth doesn't jump on first frame
            _targetX = transform.position.x;
            _hasTarget = true;
        }

        void Update()
        {
            // Recompute clamp when resolution/aspect changes
            if (recalcClampOnResize && (Screen.width != _lastScreenW || Screen.height != _lastScreenH))
                RecomputeClamp();

            // 1) Read pointer every frame (touch first; mouse fallback)
            float screenX;
            if (TryGetPointerScreenX(requirePress, out screenX))
            {
                float worldX = ScreenToWorldX(screenX);
                _targetX = Mathf.Clamp(worldX, -_halfWidthClamp + edgePadding, _halfWidthClamp - edgePadding);
                _hasTarget = true;
            }
            else if (!requirePress)
            {
                _hasTarget = true; // keep following last known target
            }
            else
            {
                _hasTarget = false;
            }

            // 2) Apply movement using unscaled delta so Freeze doesn't affect feel
            if (!_hasTarget) return;

            Vector2 p = _rb.position;

            if (follow == FollowMode.Snap)
            {
                p.x = _targetX;
            }
            else
            {
                float step = MoveSpeed * Time.unscaledDeltaTime;
                p.x = Mathf.MoveTowards(p.x, _targetX, step);
            }

            _rb.MovePosition(p);
        }

        // -------------------------------------------------------
        // Clamp computation
        // -------------------------------------------------------
        void RecomputeClamp()
        {
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;

            if (config != null)
            {
                _halfWidthClamp = Mathf.Max(0.1f, config.arenaHalfWidth);
            }
            else
            {
                var cam = _cam ? _cam : Camera.main;
                if (cam != null && cam.orthographic)
                    _halfWidthClamp = Mathf.Max(0.1f, cam.orthographicSize * cam.aspect);
                else
                    _halfWidthClamp = 3f; // safe fallback
            }

            // Ensure padding cannot invert range
            float maxPad = Mathf.Max(0f, _halfWidthClamp - 0.05f);
            if (edgePadding > maxPad) edgePadding = maxPad;
        }

        // -------------------------------------------------------
        // Input helpers (no allocations)
        // -------------------------------------------------------
        float ScreenToWorldX(float screenX)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return screenX / 100f; // editor safety fallback

            float z = -_cam.transform.position.z; // camera typically at -10 for 2D
            Vector3 pt = new Vector3(screenX, 0f, z);
            return _cam.ScreenToWorldPoint(pt).x;
        }

        bool TryGetPointerScreenX(bool mustBePressed, out float screenX)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null)
            {
                bool pressed = touch.press.isPressed;
                if (!mustBePressed || pressed)
                {
                    if (pressed || !mustBePressed)
                    {
                        screenX = touch.position.ReadValue().x;
                        return true;
                    }
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool pressed = mouse.leftButton.isPressed;
                if (!mustBePressed || pressed)
                {
                    screenX = mouse.position.ReadValue().x;
                    return true;
                }
            }
#else
            if (Input.touchSupported && Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (!mustBePressed || t.phase != TouchPhase.Ended)
                {
                    screenX = t.position.x;
                    return true;
                }
            }

            if (!mustBePressed || Input.GetMouseButton(0))
            {
                screenX = Input.mousePosition.x;
                return true;
            }
#endif
            screenX = 0f;
            return false;
        }

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------
        public void SetFollowMode(FollowMode mode) => follow = mode;
        public void ForceRecomputeClamp() => RecomputeClamp();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (smoothSpeed < 0f) smoothSpeed = 0f;
            if (edgePadding < 0f) edgePadding = 0f;

            if (!Application.isPlaying)
            {
                if (_cam == null) _cam = Camera.main;
                RecomputeClamp();
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            float L = -_halfWidthClamp + edgePadding;
            float R =  _halfWidthClamp - edgePadding;
            Vector3 a = new Vector3(L, transform.position.y - 10f, 0f);
            Vector3 b = new Vector3(L, transform.position.y + 10f, 0f);
            Vector3 c = new Vector3(R, transform.position.y - 10f, 0f);
            Vector3 d = new Vector3(R, transform.position.y + 10f, 0f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(c, d);
        }
#endif
    }
}
