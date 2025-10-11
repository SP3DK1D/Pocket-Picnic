// Assets/_Project/Scripts/FX/VFXManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Single, simple VFX hub:
    /// - Clear power-up burst (UI Image scales & fades, uses unscaled time)
    /// - Bomb explosion (world prefab; auto-return after lifetime)
    /// - Shield ring (world prefab; fades SpriteRenderers in/out)
    ///
    /// Notes:
    /// • Class name is EXACTLY VFXManager so Fruit.cs can call it.
    /// • Lightweight pooling for burst images and bomb explosions.
    /// • No operator '!' on pool types (prevents CS0023).
    /// • Uses unscaled time so Freeze doesn't affect playback.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        // ------------------ UI Burst (CLEAR) ------------------
        [Header("Screen Burst for CLEAR (UI)")]
        [Tooltip("HUD canvas that hosts the burst image.")]
        public Canvas uiCanvas;

        [Tooltip("UI Image prefab (Raycast Target OFF). Any square sprite works.")]
        public Image screenBurstPrefab;

        [Tooltip("Tint of the burst. Alpha is respected and animated to 0.")]
        public Color screenBurstColor = new Color(1f, 0.92f, 0.35f, 0.95f);

        [Min(0.05f)] public float screenBurstDuration = 0.35f;

        [Tooltip("Target scale at the end of the animation (start is ~0.3).")]
        [Range(0.5f, 12f)] public float screenBurstScale = 3.5f;

        [Tooltip("How many burst Images prewarmed in the pool.")]
        [Min(0)] public int burstPoolSize = 6;

        // ------------------ Bomb Explosion --------------------
        [Header("Bomb mini explosion (world)")]
        [Tooltip("Prefab with ParticleSystem/animation. Will be pooled.")]
        public GameObject bombExplosionPrefab;

        [Min(0.05f)] public float bombFxLifetime = 1.0f;

        [Min(0)] public int bombPoolSize = 8;

        // ------------------ Shield Field ----------------------
        [Header("Shield field (world)")]
        [Tooltip("Prefab with one or more SpriteRenderers for the ring/field.")]
        public GameObject shieldFieldPrefab;

        [Tooltip("Peak alpha of shield sprites while active.")]
        [Range(0.05f, 1f)] public float shieldAlpha = 0.35f;

        [Tooltip("Fade in/out seconds for the shield sprites.")]
        [Min(0f)] public float shieldFade = 0.12f;

        // ------------------ Internals -------------------------
        Transform _uiPoolRoot;
        Transform _worldPoolRoot;

        readonly Queue<Image> _burstPool = new Queue<Image>();
        readonly Queue<GameObject> _bombPool = new Queue<GameObject>();

        GameObject _activeShield;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Make tidy holders so pooled objects don’t clutter the hierarchy.
            _uiPoolRoot = new GameObject("~VFXPool_UI").transform;
            _worldPoolRoot = new GameObject("~VFXPool_World").transform;
            _uiPoolRoot.SetParent(transform, false);
            _worldPoolRoot.SetParent(transform, false);

            Prewarm();
        }

        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded;
        }

        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------
        // Event reactions
        // ------------------------------------------------------
        void OnPowerupStarted(PowerupDef def)
        {
            if (def == null) return;
            switch (def.kind)
            {
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
            if (def == null) return;
            if (def.kind == PowerupDef.PowerupKind.Shield)
                DetachShield();
        }

        // ------------------------------------------------------
        // Public API: used by Fruit.cs
        // ------------------------------------------------------
        /// <summary>Called by Fruit when a bomb is caught.</summary>
        public void PlayBombExplosion(Vector3 worldPos)
        {
            if (bombExplosionPrefab == null)
                return; // harmless no-op if prefab not set

            var go = GetBombFromPool();
            go.transform.SetParent(null, false);
            go.transform.position = worldPos;
            go.SetActive(true);

            // If it has a particle system, restart it
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }

            StartCoroutine(ReturnAfter(go, bombFxLifetime));
        }

        // ------------------------------------------------------
        // CLEAR: UI burst
        // ------------------------------------------------------
        void PlayClearScreenBurst()
        {
            if (uiCanvas == null)
                return; // no canvas → no burst

            if (screenBurstPrefab == null)
            {
                // Simple white flash fallback
                var go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0f);
                SetupFullScreen(go.transform as RectTransform, uiCanvas.transform);
                StartCoroutine(FlashRoutine(img, screenBurstDuration, new Color(1f, 1f, 1f, 0.9f)));
                return;
            }

            var burst = GetBurstFromPool();
            var rt = burst.rectTransform;
            burst.transform.SetParent(uiCanvas.transform, false);
            burst.color = screenBurstColor;
            burst.raycastTarget = false; // ensure it never blocks UI
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one * 0.3f;

            burst.gameObject.SetActive(true);
            StartCoroutine(CoBurstAnim(burst));
        }

        IEnumerator CoBurstAnim(Image img)
        {
            float t = 0f;
            float dur = Mathf.Max(0.05f, screenBurstDuration);
            Color start = screenBurstColor;
            start.a = screenBurstColor.a;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);

                // Scale 0.3 -> screenBurstScale
                float s = Mathf.Lerp(0.3f, screenBurstScale, k);
                img.rectTransform.localScale = Vector3.one * s;

                // Fade alpha to 0
                Color c = start;
                c.a = Mathf.Lerp(start.a, 0f, k);
                img.color = c;

                yield return null;
            }

            // Return to pool
            ReturnBurstToPool(img);
        }

        // Fallback one-shot flash when no prefab is provided
        IEnumerator FlashRoutine(Image img, float duration, Color target)
        {
            float t = 0f;
            duration = Mathf.Max(0.05f, duration);

            // Fade up quick
            while (t < duration * 0.25f)
            {
                t += Time.unscaledDeltaTime;
                var c = img.color; c.a = Mathf.Lerp(0f, target.a, t / (duration * 0.25f)); img.color = c;
                yield return null;
            }

            // Fade down longer
            t = 0f;
            while (t < duration * 0.75f)
            {
                t += Time.unscaledDeltaTime;
                var c = img.color; c.a = Mathf.Lerp(target.a, 0f, t / (duration * 0.75f)); img.color = c;
                yield return null;
            }

            if (img) Destroy(img.gameObject);
        }

        // ------------------------------------------------------
        // Shield attach / detach
        // ------------------------------------------------------
        public void AttachShield(Transform player)
        {
            if (player == null || shieldFieldPrefab == null) return;

            // Recreate fresh so materials are always in a clean state.
            if (_activeShield) Destroy(_activeShield);

            _activeShield = Instantiate(shieldFieldPrefab, player);
            _activeShield.transform.localPosition = Vector3.zero;

            // Make sure all sprites can fade via color.a
            var srs = _activeShield.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (!sr) continue;
                Color c = sr.color; c.a = 0f; sr.color = c; // start transparent
                // Ensure default sprite shader (transparent) so alpha works
                if (sr.sharedMaterial == null || sr.sharedMaterial.shader == null ||
                    sr.sharedMaterial.shader.name.Contains("Sprites/Default"))
                {
                    // OK (Unity default supports alpha)
                }
            }
            StartCoroutine(FadeSpritesTo(srs, shieldAlpha, shieldFade));
        }

        public void DetachShield()
        {
            if (_activeShield == null) return;

            var srs = _activeShield.GetComponentsInChildren<SpriteRenderer>(true);
            StartCoroutine(FadeSpritesTo(srs, 0f, shieldFade));
            Destroy(_activeShield, Mathf.Max(0.01f, shieldFade) + 0.02f);
            _activeShield = null;
        }

        IEnumerator FadeSpritesTo(SpriteRenderer[] srs, float targetA, float dur)
        {
            dur = Mathf.Max(0.01f, dur);
            // Snapshot start alphas
            var startA = new float[srs.Length];
            for (int i = 0; i < srs.Length; i++)
                startA[i] = srs[i] ? srs[i].color.a : 0f;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                for (int i = 0; i < srs.Length; i++)
                {
                    var sr = srs[i];
                    if (!sr) continue;
                    Color c = sr.color;
                    c.a = Mathf.Lerp(startA[i], targetA, k);
                    sr.color = c;
                }
                yield return null;
            }

            // Snap to target
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (!sr) continue;
                Color c = sr.color; c.a = targetA; sr.color = c;
            }
        }

        // ------------------------------------------------------
        // Pooling
        // ------------------------------------------------------
        void Prewarm()
        {
            // Burst Images
            if (uiCanvas != null && screenBurstPrefab != null)
            {
                for (int i = 0; i < burstPoolSize; i++)
                {
                    var img = Instantiate(screenBurstPrefab, _uiPoolRoot);
                    img.gameObject.SetActive(false);
                    // safety: never block clicks
                    img.raycastTarget = false;
                    _burstPool.Enqueue(img);
                }
            }

            // Bomb explosions
            if (bombExplosionPrefab != null)
            {
                for (int i = 0; i < bombPoolSize; i++)
                {
                    var go = Instantiate(bombExplosionPrefab, _worldPoolRoot);
                    go.SetActive(false);
                    _bombPool.Enqueue(go);
                }
            }
        }

        Image GetBurstFromPool()
        {
            if (_burstPool.Count > 0)
            {
                var img = _burstPool.Dequeue();
                // parent will be set on use; keep inactive until then
                return img;
            }
            // Pool exhausted → instantiate one more
            return Instantiate(screenBurstPrefab, _uiPoolRoot);
        }

        void ReturnBurstToPool(Image img)
        {
            if (img == null) return;
            img.gameObject.SetActive(false);
            img.transform.SetParent(_uiPoolRoot, false);
            img.rectTransform.localScale = Vector3.one;
            _burstPool.Enqueue(img);
        }

        GameObject GetBombFromPool()
        {
            if (_bombPool.Count > 0)
                return _bombPool.Dequeue();

            return Instantiate(bombExplosionPrefab, _worldPoolRoot);
        }

        IEnumerator ReturnAfter(GameObject go, float seconds)
        {
            float t = 0f;
            seconds = Mathf.Max(0.02f, seconds);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            go.SetActive(false);
            go.transform.SetParent(_worldPoolRoot, false);
            _bombPool.Enqueue(go);
        }

        // ------------------------------------------------------
        // Helpers
        // ------------------------------------------------------
        static void SetupFullScreen(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
