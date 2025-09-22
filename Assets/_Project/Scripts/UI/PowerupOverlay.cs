using UnityEngine;
using UnityEngine.UI;
using static CatchTheFruit.PowerupDef;

namespace CatchTheFruit
{
    /// <summary>
    /// Shows a fullscreen tint only while Freeze (TimeScale) is active.
    /// Does NOT depend on extra fields in PowerupDef.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PowerupOverlay : MonoBehaviour
    {
        [Header("Freeze overlay color")]
        [SerializeField] Color freezeColor = new Color(0.8f, 0.95f, 1f, 0.35f);

        Image _img;

        void Awake()
        {
            _img = GetComponent<Image>();
            _img.color = new Color(freezeColor.r, freezeColor.g, freezeColor.b, 0f);
            _img.enabled = false;
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
            _img.enabled = true;
            _img.color = freezeColor;
        }

        void OnPowerupEnded(PowerupDef def)
        {
            if (def == null || def.kind != PowerupKind.TimeScale) return;
            Hide();
        }

        void OnGameOver() => Hide();

        void Hide()
        {
            if (!_img) return;
            var c = freezeColor; c.a = 0f;
            _img.color = c;
            _img.enabled = false;
        }
    }
}
