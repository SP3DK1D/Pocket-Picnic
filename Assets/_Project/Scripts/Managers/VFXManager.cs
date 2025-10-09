// Assets/_Project/Scripts/FX/VFXManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Central visual FX: clear-screen burst, bomb mini explosion, shield ring.
    /// Uses unscaled time so effects ignore Freeze.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Screen Burst for CLEAR (UI)")]
        public Canvas uiCanvas;
        public Image screenBurstPrefab;
        public Color screenBurstColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        [Min(0.05f)] public float screenBurstDuration = 0.35f;
        [Range(0.1f, 6f)] public float screenBurstScale = 3.0f;

        [Header("Bomb mini explosion (world)")]
        public GameObject bombExplosionPrefab;
        [Min(0.05f)] public float bombFxLifetime = 1.0f;

        [Header("Shield field (world)")]
        public GameObject shieldFieldPrefab;
        [Range(0.05f, 1f)] public float shieldAlpha = 0.35f;
        [Min(0f)] public float shieldFade = 0.12f;

        GameObject _activeShield;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnStarted;
            GameEvents.OnPowerupEnded   += OnEnded;
        }
        void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnStarted;
            GameEvents.OnPowerupEnded   -= OnEnded;
            if (Instance == this) Instance = null;
        }

        void OnStarted(PowerupDef def)
        {
            if (!def) return;
            switch (def.kind)
            {
                case PowerupDef.PowerupKind.ClearScreen: PlayClearScreenBurst(); break;
                case PowerupDef.PowerupKind.Shield:      AttachShield(PowerupManager.PlayerTransform); break;
            }
        }

        void OnEnded(PowerupDef def)
        {
            if (!def) return;
            if (def.kind == PowerupDef.PowerupKind.Shield) DetachShield();
        }

        public void PlayBombExplosion(Vector3 worldPos)
        {
            if (!bombExplosionPrefab) return;
            var fx = Instantiate(bombExplosionPrefab, worldPos, Quaternion.identity);
            Destroy(fx, bombFxLifetime);
        }

        public void PlayClearScreenBurst()
        {
            if (!uiCanvas) return;

            if (!screenBurstPrefab)
            {
                // simple white flash fallback
                var go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0f);
                SetupFullScreen(go.transform as RectTransform, uiCanvas.transform);
                StartCoroutine(FlashRoutine(img, screenBurstDuration, new Color(1f, 1f, 1f, 0.85f)));
                return;
            }

            Image burst = Instantiate(screenBurstPrefab, uiCanvas.transform);
            burst.color = screenBurstColor;
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

        IEnumerator BurstRoutine(Image img)
        {
            float t = 0f, d = Mathf.Max(0.05f, screenBurstDuration);
            var startCol = img.color; startCol.a = screenBurstColor.a; img.color = startCol;

            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float k = t / d;
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, screenBurstScale, k);
                var c = img.color; c.a = Mathf.Lerp(screenBurstColor.a, 0f, k); img.color = c;
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

        static void SetupFullScreen(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
