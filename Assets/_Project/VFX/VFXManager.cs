using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Central visual FX:
    /// - Freeze overlay (HUD tint that never blocks clicks)
    /// - Clear-screen burst (UI)
    /// - Bomb mini explosion (world)
    /// - Shield field (world) with fade in/out
    ///
    /// Uses unscaled time for predictable visuals while Freeze or Pause are active.
    /// Listens to GameEvents so no other script needs to poke it.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        // ========================== UI: CLEAR BURST ==========================
        [Header("UI — Clear Screen Burst")]
        [Tooltip("Canvas used for all HUD/UI effects.")]
        public Canvas uiCanvas;

        [Tooltip("Optional prefab Image for the clear burst (centered, scaled). If null, a simple white flash is used.")]
        public Image clearBurstPrefab;

        [Tooltip("Tint for the clear burst Image.")]
        public Color clearBurstColor = new Color(1f, 0.9f, 0.3f, 0.9f);

        [Min(0.05f)] public float clearBurstDuration = 0.35f;
        [Range(0.1f, 6f)] public float clearBurstScale = 3.0f;

        // ========================== UI: FREEZE OVERLAY =======================
        [Header("UI — Freeze Overlay")]
        [Tooltip("Optional: assign an Image in your HUD for the freeze overlay. If null, one is auto-created under uiCanvas.")]
        public Image freezeOverlayImage;

        [Tooltip("Tint used while the Freeze power-up is active.")]
        public Color freezeOverlayColor = new Color(0.8f, 0.95f, 1f, 0.35f);

        [Min(0f)] public float freezeFadeIn = 0.10f;
        [Min(0f)] public float freezeFadeOut = 0.10f;

        // ========================== WORLD: EXPLOSION =========================
        [Header("World — Bomb Mini Explosion")]
        public GameObject bombExplosionPrefab;
        [Min(0.05f)] public float bombFxLifetime = 1.0f;

        // ========================== WORLD: SHIELD FIELD ======================
        [Header("World — Shield Field")]
        public GameObject shieldFieldPrefab;
        [Range(0.05f, 1f)] public float shieldAlpha = 0.35f;
        [Min(0f)] public float shieldFade = 0.12f;

        // --- runtime ---
        GameObject _activeShield;
        Coroutine _freezeCo;
        Image _runtimeFreezeImage;  // if we had to auto-create one

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded;
            GameEvents.OnGameOver += OnGameOver;

            // Ensure UI overlay is prepared hidden
            EnsureFreezeOverlay(false, immediate: true);
        }

        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            GameEvents.OnGameOver -= OnGameOver;

            if (Instance == this) Instance = null;
        }

        // ====================================================================
        // GameEvents
        // ====================================================================

        void OnPowerupStarted(PowerupDef def)
        {
            if (!def) return;

            switch (def.kind)
            {
                case PowerupDef.PowerupKind.TimeScale:
                    ShowFreezeOverlay();
                    break;

                case PowerupDef.PowerupKind.ClearScreen:
                    PlayClearScreenBurst();
                    break;

                case PowerupDef.PowerupKind.Shield:
                    AttachShield(PowerupManager.PlayerTransform);
                    break;
            }
        }

        void OnPowerupEnded(PowerupDef def)
        {
            if (!def) return;

            switch (def.kind)
            {
                case PowerupDef.PowerupKind.TimeScale:
                    HideFreezeOverlay();
                    break;

                case PowerupDef.PowerupKind.Shield:
                    DetachShield();
                    break;
            }
        }

        void OnGameOver()
        {
            // Always clean UI/world fx when the run ends
            HideFreezeOverlay(immediate: true);
            DetachShield();
        }

        // ====================================================================
        // PUBLIC API (world/UI helpers)
        // ====================================================================

        public void PlayBombExplosion(Vector3 worldPos)
        {
            if (!bombExplosionPrefab) return;
            var fx = Instantiate(bombExplosionPrefab, worldPos, Quaternion.identity);
            Destroy(fx, bombFxLifetime);
        }

        public void PlayClearScreenBurst()
        {
            if (!uiCanvas)
            {
                // No canvas? nothing to draw
                return;
            }

            if (!clearBurstPrefab)
            {
                // simple white flash fallback
                var go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0f);
                SetupFullScreen(go.transform as RectTransform, uiCanvas.transform);
                StartCoroutine(FlashRoutine(img, clearBurstDuration, new Color(1f, 1f, 1f, 0.85f)));
                return;
            }

            var burst = Instantiate(clearBurstPrefab, uiCanvas.transform);
            burst.color = clearBurstColor;
            var rt = burst.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one * 0.3f;
            StartCoroutine(BurstRoutine(burst));
        }

        public void AttachShield(Transform player)
        {
            if (!player || !shieldFieldPrefab) return;
            if (_activeShield) Destroy(_activeShield);

            _activeShield = Instantiate(shieldFieldPrefab, player);
            _activeShield.transform.localPosition = Vector3.zero;

            // Fade in all sprites under the shield object
            var srs = _activeShield.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs) StartCoroutine(FadeSpriteAlpha(sr, 0f, shieldAlpha, shieldFade));
        }

        public void DetachShield()
        {
            if (!_activeShield) return;
            var srs = _activeShield.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs) StartCoroutine(FadeSpriteAlpha(sr, sr.color.a, 0f, shieldFade));
            Destroy(_activeShield, shieldFade + 0.02f);
            _activeShield = null;
        }

        // ====================================================================
        // Freeze Overlay (integrated PowerupOverlay)
        // ====================================================================

        /// <summary>Ensure overlay exists, configured to never block clicks, and set visible state.</summary>
        void EnsureFreezeOverlay(bool visible, bool immediate = false)
        {
            if (!uiCanvas) return;

            // Use assigned image, else create once
            var img = freezeOverlayImage ? freezeOverlayImage : _runtimeFreezeImage;
            if (!img)
            {
                var go = new GameObject("FreezeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                var rt = go.GetComponent<RectTransform>();
                SetupFullScreen(rt, uiCanvas.transform);

                img = go.GetComponent<Image>();
                img.raycastTarget = false; // allow HUD/buttons to be clickable through overlay
                img.color = freezeOverlayColor;

                var cg = go.GetComponent<CanvasGroup>();
                cg.blocksRaycasts = false; // safety: never block raycasts
                cg.interactable = false;

                _runtimeFreezeImage = img;
            }

            // Make sure color is the configured tint (alpha may be animated)
            var baseCol = freezeOverlayColor;
            if (immediate)
            {
                img.enabled = visible;
                var c = baseCol; c.a = visible ? baseCol.a : 0f;
                img.color = c;
            }
            else
            {
                img.enabled = true; // enable to show fade
            }
        }

        void ShowFreezeOverlay()
        {
            if (_freezeCo != null) StopCoroutine(_freezeCo);
            EnsureFreezeOverlay(true, immediate: freezeFadeIn <= 0f);

            var img = freezeOverlayImage ? freezeOverlayImage : _runtimeFreezeImage;
            if (!img) return;

            _freezeCo = StartCoroutine(FadeImageAlpha(img, from: img.color.a, to: freezeOverlayColor.a, dur: freezeFadeIn));
        }

        void HideFreezeOverlay(bool immediate = false)
        {
            var img = freezeOverlayImage ? freezeOverlayImage : _runtimeFreezeImage;
            if (!img) return;

            if (_freezeCo != null) { StopCoroutine(_freezeCo); _freezeCo = null; }
            if (immediate || freezeFadeOut <= 0f)
            {
                var c = img.color; c.a = 0f; img.color = c;
                img.enabled = false;
                return;
            }

            _freezeCo = StartCoroutine(FadeOutAndDisable(img, img.color.a, 0f, freezeFadeOut));
        }

        // ====================================================================
        // Coroutines (unscaled)
        // ====================================================================

        IEnumerator BurstRoutine(Image img)
        {
            float t = 0f, d = Mathf.Max(0.05f, clearBurstDuration);
            var startCol = img.color; startCol.a = clearBurstColor.a; img.color = startCol;

            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float k = t / d;
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, clearBurstScale, k);
                var c = img.color; c.a = Mathf.Lerp(clearBurstColor.a, 0f, k); img.color = c;
                yield return null;
            }
            Destroy(img.gameObject);
        }

        IEnumerator FlashRoutine(Image img, float duration, Color target)
        {
            float t = 0f;
            while (t < duration * 0.25f)
            {
                t += Time.unscaledDeltaTime;
                var c = img.color; c.a = Mathf.Lerp(0f, target.a, t / (duration * 0.25f)); img.color = c;
                yield return null;
            }
            t = 0f;
            while (t < duration * 0.75f)
            {
                t += Time.unscaledDeltaTime;
                var c = img.color; c.a = Mathf.Lerp(target.a, 0f, t / (duration * 0.75f)); img.color = c;
                yield return null;
            }
            Destroy(img.gameObject);
        }

        IEnumerator FadeSpriteAlpha(SpriteRenderer sr, float from, float to, float dur)
        {
            float t = 0f; var baseCol = sr.color;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                var c = baseCol; c.a = Mathf.Lerp(from, to, t / dur); sr.color = c;
                yield return null;
            }
            var final = baseCol; final.a = to; sr.color = final;
        }

        IEnumerator FadeImageAlpha(Image img, float from, float to, float dur)
        {
            if (dur <= 0f)
            {
                var c = img.color; c.a = to; img.color = c;
                img.enabled = to > 0f;
                yield break;
            }

            float t = 0f;
            img.enabled = true;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, to, t / dur);
                var c = img.color; c.a = a; img.color = c;
                yield return null;
            }
            var cf = img.color; cf.a = to; img.color = cf;
            img.enabled = to > 0f;
            _freezeCo = null;
        }

        IEnumerator FadeOutAndDisable(Image img, float from, float to, float dur)
        {
            yield return FadeImageAlpha(img, from, to, dur);
            img.enabled = false;
        }

        // ====================================================================
        // Utils
        // ====================================================================
        static void SetupFullScreen(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
