using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CatchTheFruit
{
    /// <summary>
    /// Follows the pointer X. Default = Snap (instant, no lag).
    /// Smooth mode is available if you ever want easing.
    /// Uses unscaled time so freeze power-up does NOT slow the player.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        public enum FollowMode { Snap, Smooth }
        [Header("Follow")]
        [SerializeField] private FollowMode follow = FollowMode.Snap;

        [Tooltip("Used only in Smooth mode: world-units/second toward the pointer.")]
        [Min(0f)] public float smoothSpeed = 12f; // fallback if config is missing

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            if (!_cam) Debug.LogError("[PlayerMover] Main Camera not found (tag your camera 'MainCamera').");
            if (!config) Debug.LogWarning("[PlayerMover] GameConfig not assigned (will use local smoothSpeed).");
        }

        private void Update()
        {
            if (!_cam) return;

            // Get current pointer (mouse or touch) X in SCREEN space
            if (!TryGetPointerScreenX(out float screenX)) return;

            // Convert to WORLD X and clamp to arena
            float worldX = ScreenToWorldX(screenX);
            float clampedX = (config != null)
                ? Mathf.Clamp(worldX, -config.arenaHalfWidth, config.arenaHalfWidth)
                : worldX;

            // Apply movement
            Vector3 p = transform.position;

            if (follow == FollowMode.Snap)
            {
                // Instant: stick directly under pointer (no lag)
                p.x = clampedX;
            }
            else // Smooth
            {
                float speed = (config ? config.playerMoveSpeed : smoothSpeed);
                // Use unscaled time so power-ups that change Time.timeScale don't affect player feel
                p.x = Mathf.MoveTowards(p.x, clampedX, speed * Time.unscaledDeltaTime);
            }

            transform.position = p;
        }

        private float ScreenToWorldX(float screenX)
        {
            // We only care about X; use camera distance on Z so ScreenToWorldPoint works in 2D
            Vector3 pt = new Vector3(screenX, 0f, -_cam.transform.position.z);
            return _cam.ScreenToWorldPoint(pt).x;
        }

        private bool TryGetPointerScreenX(out float screenX)
        {
            // Default behavior: move while pressed (touch or LMB). 
            // If you want movement even when not pressed, you can return last known X instead.
#if ENABLE_INPUT_SYSTEM
            // Touch has priority
            if (Touchscreen.current?.primaryTouch.press.isPressed == true)
            {
                screenX = Touchscreen.current.primaryTouch.position.ReadValue().x;
                return true;
            }
            // Mouse fallback
            if (Mouse.current?.leftButton.isPressed == true)
            {
                screenX = Mouse.current.position.ReadValue().x;
                return true;
            }
#else
            if (Input.touchSupported && Input.touchCount > 0)
            {
                screenX = Input.GetTouch(0).position.x;
                return true;
            }
            if (Input.GetMouseButton(0))
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
/*
How to implement in Unity:
1) Replace your existing PlayerMover.cs with this file and let Unity recompile.
2) Select the Player object → make sure it has:
   - Tag = Player
   - BoxCollider2D (IsTrigger = ON)
   - PlayerMover (this script) and the GameConfig assigned (recommended).
3) Test: Hold LMB in Game view (or touch on device) — the player should now "stick" instantly under the pointer.
   - If you ever want easing, set Follow = Smooth in the Inspector.
*/
