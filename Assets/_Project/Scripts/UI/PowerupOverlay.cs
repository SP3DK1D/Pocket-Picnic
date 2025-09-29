using UnityEngine;
using UnityEngine.UI;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Fullscreen tint shown only while Freeze (TimeScale) is active.
    /// Now force-ignores pointer raycasts so HUD buttons (e.g., Pause) remain clickable.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class PowerupOverlay : MonoBehaviour
    {
        [Header("Freeze Overlay")]
        [SerializeField] private Color freezeColor = new Color(0.8f, 0.95f, 1f, 0.35f);

        [Header("Input Passthrough")]
        [Tooltip("If ON, this overlay will never block clicks/touches.")]
        [SerializeField] private bool passthrough = true;

        [Tooltip("If ON, ensures any CanvasGroup on this object won't block raycasts.")]
        [SerializeField] private bool relaxCanvasGroup = true;

        Image _img;
        CanvasGroup _cg;

        void Awake()
        {
            _img = GetComponent<Image>();
            if (relaxCanvasGroup)
            {
                _cg = GetComponent<CanvasGroup>();
                if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();
                _cg.blocksRaycasts = false;  // <- critical for not blocking HUD
                _cg.interactable = false;
            }
            if (passthrough && _img) _img.raycastTarget = false; // <- also ensures no blocking

            // Start hidden
            SetVisible(false);
        }

        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded;
            GameEvents.OnGameOver += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            GameEvents.OnGameOver -= OnGameOver;
        }

        void OnPowerupStarted(PowerupDef def)
        {
            if (def == null || def.kind != PowerupKind.TimeScale) return;
            ShowFreeze();
        }

        void OnPowerupEnded(PowerupDef def)
        {
            if (def == null || def.kind != PowerupKind.TimeScale) return;
            Hide();
        }

        void OnGameOver() => Hide();

        // ---------- Visual control ----------
        void ShowFreeze()
        {
            if (!_img) return;
            _img.color = freezeColor;
            SetVisible(true);
        }

        void Hide()
        {
            if (!_img) return;
            // Clear alpha to avoid any residual tint on enable/disable churn
            var c = freezeColor; c.a = 0f;
            _img.color = c;
            SetVisible(false);
        }

        void SetVisible(bool on)
        {
            // We keep both alpha and enabled in sync. Since raycasts are disabled,
            // enabled true will NOT block clicks.
            if (_img) _img.enabled = on;
        }
    }
}
