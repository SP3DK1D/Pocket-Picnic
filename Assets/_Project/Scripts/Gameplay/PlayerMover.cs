using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CatchTheFruit
{
    /// <summary>
    /// Mobile-friendly basket mover.
    /// - Reads touch/mouse X, clamps to arena, and moves a Rigidbody2D.
    /// - Snap: teleports under pointer. Smooth: eased with speed (unscaled).
    /// - Uses FixedUpdate + MovePosition for reliable trigger hits.
    /// - No collider requirement (so you can use a child CatchZone).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        public enum FollowMode { Snap, Smooth }

        [Header("Follow")]
        [SerializeField] private FollowMode follow = FollowMode.Snap;

        [Tooltip("Used only in Smooth mode: world-units/second toward the pointer.")]
        [Min(0f)] public float smoothSpeed = 20f;   // good for mobile

        [Header("Input")]
        [Tooltip("If true, you must be actively touching/holding to move. If false, will follow last known pointer position.")]
        public bool requirePress = true;

        Rigidbody2D _rb;
        Camera _cam;

        // Target x computed in Update, applied in FixedUpdate
        float _targetX;
        bool _hasTarget;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.isKinematic = true;              // kinematic for MovePosition control
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _cam = Camera.main;
            if (!_cam) Debug.LogError("[PlayerMover] Main Camera not found (tag your camera 'MainCamera').");
            if (!config) Debug.LogWarning("[PlayerMover] GameConfig not assigned (will clamp using current camera only).");
        }

        void OnEnable()
        {
            // Initialize target to current position so Smooth doesn't jump on first frame
            _targetX = transform.position.x;
            _hasTarget = true;
        }

        void Update()
        {
            if (!_cam) return;

            // Read input each frame (unscaled time behavior is handled in movement step)
            if (TryGetPointerScreenX(requirePress, out float screenX))
            {
                float worldX = ScreenToWorldX(screenX);
                _targetX = (config != null)
                    ? Mathf.Clamp(worldX, -config.arenaHalfWidth, config.arenaHalfWidth)
                    : worldX;
                _hasTarget = true;
            }
            else if (!requirePress)
            {
                // No new input but we keep the last target (follow stays active)
                _hasTarget = true;
            }
            else
            {
                // Requiring press and none present -> no movement update
                _hasTarget = false;
            }
        }

        void FixedUpdate()
        {
            if (!_hasTarget) return;

            Vector2 p = _rb.position;

            if (follow == FollowMode.Snap)
            {
                p.x = _targetX;
            }
            else
            {
                // Smooth toward target using unscaled time so Freeze/TimeScale don't affect feel
                float step = (config ? config.playerMoveSpeed : smoothSpeed) * Time.unscaledDeltaTime;
                p.x = Mathf.MoveTowards(p.x, _targetX, step);
            }

            _rb.MovePosition(p);
        }

        float ScreenToWorldX(float screenX)
        {
            // Convert a screen X to world X at z-plane of the player relative to camera
            float z = -_cam.transform.position.z; // camera is usually at -10
            Vector3 pt = new Vector3(screenX, 0f, z);
            return _cam.ScreenToWorldPoint(pt).x;
        }

        bool TryGetPointerScreenX(bool mustBePressed, out float screenX)
        {
#if ENABLE_INPUT_SYSTEM
            // Touch has priority
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

            // Mouse fallback (useful in editor)
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
            // Legacy Input
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
    }
}
