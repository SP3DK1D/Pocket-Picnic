using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Full-screen overlay that (optionally) reacts only to Freeze (TimeScale).
    /// Put this on a UI Image that fills the screen. Source Image can be None.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class PowerupOverlay : MonoBehaviour
    {
        [Header("Auto-bind if left empty")]
        public Image overlayImage;

        [Header("Behavior")]
        [Tooltip("If ON, overlay responds only to Freeze (TimeScale) power-ups.")]
        public bool onlyForFreeze = true;

        int _activeCount;
        Coroutine _fadeRoutine;
        Color _currentColor = new Color(0.47f, 0.78f, 1f, 0f);
        float _currentFade = 0.15f;

        void Reset() { AutoBind(); ForceFullScreen(); InitImage(); }
        void Awake() { AutoBind(); ForceFullScreen(); InitImage(); }

        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnStarted;
            GameEvents.OnPowerupEnded += OnEnded;
        }
        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnStarted;
            GameEvents.OnPowerupEnded -= OnEnded;
            SetAlpha(0f); _activeCount = 0;
        }

        void AutoBind()
        {
            if (!overlayImage) overlayImage = GetComponent<Image>();
        }

        void ForceFullScreen()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void InitImage()
        {
            if (!overlayImage) return;
            var c = overlayImage.color; c.a = 0f; overlayImage.color = c;
            overlayImage.raycastTarget = false;
            overlayImage.preserveAspect = false;
        }

        void OnStarted(PowerupDef def)
        {
            if (!overlayImage || def == null) return;

            // Filter: only respond to Freeze if requested
            if (onlyForFreeze && def.kind != PowerupDef.PowerupKind.TimeScale) return;

            if (def.overlayAlpha <= 0f) return; // nothing to show

            _activeCount++;
            _currentColor = def.overlayColor;
            _currentFade = Mathf.Max(0.01f, def.overlayFade);

            var c = _currentColor; c.a = overlayImage.color.a;
            overlayImage.color = c;

            FadeTo(def.overlayAlpha, _currentFade);
        }

        void OnEnded(PowerupDef def)
        {
            if (!overlayImage || def == null) return;

            if (onlyForFreeze && def.kind != PowerupDef.PowerupKind.TimeScale) return;
            if (def.overlayAlpha <= 0f) return;

            _activeCount = Mathf.Max(0, _activeCount - 1);
            if (_activeCount == 0)
                FadeTo(0f, _currentFade);
        }

        void FadeTo(float alpha, float duration)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(alpha, duration));
        }

        IEnumerator FadeRoutine(float target, float duration)
        {
            float start = overlayImage.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, target, t / duration);
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(target);
            _fadeRoutine = null;
        }

        void SetAlpha(float a)
        {
            var c = overlayImage.color; c.a = Mathf.Clamp01(a); overlayImage.color = c;
        }
    }
}
/*
Unity:
- Select PowerupOverlayImg (your full-screen Image).
- Ensure Source Image = None, Color alpha = 0, Raycast Target OFF.
- In PowerupOverlay component, keep "Only For Freeze" = ON.
*/
