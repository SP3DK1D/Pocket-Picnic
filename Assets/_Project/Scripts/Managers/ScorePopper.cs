using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static CatchTheFruit.GameEvents;

namespace CatchTheFruit
{
    /// <summary>
    /// Dual-purpose UI popper:
    ///  - ScorePopper: shows "+X" bursts that accumulate while visible (does NOT touch your main score label).
    ///  - Banner:      shows queued announcements (fade/slide) for WaveMessage + Challenges ONLY
    ///                 (no power-up banners; no game start/over banners).
    /// Includes simple de-duplication to prevent repeated banners.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [RequireComponent(typeof(CanvasGroup))]
    public class ScorePopper : MonoBehaviour
    {
        public enum Mode { ScorePopper, Banner }

        [Header("Mode")]
        public Mode mode = Mode.ScorePopper;

        // ------- shared refs -------
        TMP_Text _txt;
        CanvasGroup _cg;
        RectTransform _rt;
        Vector2 _basePos;

        // ===================== SCORE POPPER =====================
        [Header("Score Popper")]
        [Tooltip("How long '+X' stays fully visible before fading.")]
        [Min(0f)] public float sp_hold = 0.45f;
        [Tooltip("Fade out seconds for +X.")]
        [Min(0f)] public float sp_fade = 0.25f;
        [Tooltip("How far the '+X' floats up while showing.")]
        public Vector2 sp_floatUp = new Vector2(0f, 18f);
        [Tooltip("Prefix for score pop text.")]
        public string sp_prefix = "+";

        int _pending;           // accumulated amount
        Coroutine _spRunner;

        // ======================= BANNER =========================
        [Header("Banner (Toast)")]
        [Min(0f)] public float bn_fadeIn = 0.12f;
        [Min(0f)] public float bn_fadeOut = 0.25f;
        [Min(0f)] public float bn_pad = 0.05f;   // extra hold time
        public Vector2 bn_slide = new Vector2(0f, 28f);

        readonly Queue<(string msg, float sec)> _bnQueue = new();
        Coroutine _bnRunner;

        [Tooltip("If ON (Banner mode), auto-listen to WaveMessage + Challenges only.")]
        public bool listenToEvents = true;

        // de-duplication
        string _lastMsg = null;
        float _lastMsgTime = -999f;
        [SerializeField] float dedupeSeconds = 1.0f;

        void Awake()
        {
            _txt = GetComponent<TMP_Text>();
            _cg = GetComponent<CanvasGroup>();
            _rt = GetComponent<RectTransform>();
            _basePos = _rt.anchoredPosition;

            _txt.text = "";
            _cg.alpha = 0f;
        }

        void OnEnable()
        {
            if (mode == Mode.Banner && listenToEvents)
            {
                // ONLY these two sources:
                GameEvents.OnWaveMessage += EvWave;         // e.g., "⚡ Speeding up!"
                GameEvents.OnChallengeStarted += EvChStart;      // BananaBlitz/BombStorm/GoldenTime
                GameEvents.OnChallengeEnded += EvChEnd;
            }
        }

        void OnDisable()
        {
            if (mode == Mode.Banner && listenToEvents)
            {
                GameEvents.OnWaveMessage -= EvWave;
                GameEvents.OnChallengeStarted -= EvChStart;
                GameEvents.OnChallengeEnded -= EvChEnd;
            }

            // stop coroutines to avoid lingering UI state between scene swaps
            if (_spRunner != null) StopCoroutine(_spRunner);
            if (_bnRunner != null) StopCoroutine(_bnRunner);
            _spRunner = null; _bnRunner = null;

            _bnQueue.Clear();
            _txt.text = "";
            _cg.alpha = 0f;
            _rt.anchoredPosition = _basePos;
        }

        // =======================================================
        // PUBLIC API
        // =======================================================

        /// <summary>Score mode: add a delta that will accumulate (e.g., +15).</summary>
        public void PushScoreDelta(int amount)
        {
            if (mode != Mode.ScorePopper) return;
            _pending += amount;
            if (_spRunner == null) _spRunner = StartCoroutine(RunScorePop());
        }

        /// <summary>Banner mode: show an announcement immediately (queued if busy).</summary>
        public void ShowBanner(string message, float seconds = 1.4f)
        {
            if (mode != Mode.Banner) return;

            // de-duplicate repeated calls (same message within short window)
            if (_lastMsg == message && (Time.unscaledTime - _lastMsgTime) < dedupeSeconds)
                return;

            _lastMsg = message;
            _lastMsgTime = Time.unscaledTime;

            _bnQueue.Enqueue((message, Mathf.Max(0.05f, seconds + bn_pad)));
            if (_bnRunner == null) _bnRunner = StartCoroutine(RunBanner());
        }

        // =======================================================
        // SCORE POPPER IMPLEMENTATION
        // =======================================================
        IEnumerator RunScorePop()
        {
            // Snap into starting state
            _rt.anchoredPosition = _basePos;
            _cg.alpha = 1f;

            float t = 0f;
            int shown = 0;

            while (true)
            {
                // Take all pending and display
                if (_pending != 0)
                {
                    shown += _pending;
                    _pending = 0;
                    _txt.text = sp_prefix + shown.ToString();
                    _rt.anchoredPosition = _basePos; // reset float
                    t = 0f; // restart hold+fade timeline on update
                }

                // Advance timer
                t += Time.unscaledDeltaTime;

                // Float upward a bit over the (hold+fade) time
                float total = sp_hold + sp_fade;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, total));
                _rt.anchoredPosition = Vector2.Lerp(_basePos, _basePos + sp_floatUp, k);

                // Fade only after hold
                if (t > sp_hold)
                {
                    float fk = Mathf.InverseLerp(sp_hold, sp_hold + sp_fade, t);
                    _cg.alpha = 1f - fk;
                }

                // Done?
                if (t >= sp_hold + sp_fade)
                {
                    if (_pending == 0) break;   // nothing more to show
                    // else loop will immediately refresh text and restart timing
                }

                yield return null;
            }

            // Clean up
            _txt.text = "";
            _cg.alpha = 0f;
            _rt.anchoredPosition = _basePos;
            _spRunner = null;
        }

        // =======================================================
        // BANNER IMPLEMENTATION
        // =======================================================
        IEnumerator RunBanner()
        {
            while (_bnQueue.Count > 0)
            {
                var (msg, dur) = _bnQueue.Dequeue();

                _txt.text = msg;
                _rt.anchoredPosition = _basePos - bn_slide;

                // fade in + slide
                float t = 0f;
                while (t < bn_fadeIn)
                {
                    t += Time.unscaledDeltaTime;
                    float a = Mathf.Clamp01(t / bn_fadeIn);
                    _cg.alpha = a;
                    _rt.anchoredPosition = Vector2.Lerp(_basePos - bn_slide, _basePos, a);
                    yield return null;
                }
                _cg.alpha = 1f;
                _rt.anchoredPosition = _basePos;

                // hold
                float h = 0f;
                while (h < dur) { h += Time.unscaledDeltaTime; yield return null; }

                // fade out
                t = 0f;
                while (t < bn_fadeOut)
                {
                    t += Time.unscaledDeltaTime;
                    _cg.alpha = 1f - Mathf.Clamp01(t / bn_fadeOut);
                    yield return null;
                }
                _cg.alpha = 0f;
                _txt.text = "";
            }
            _bnRunner = null;
        }

        // =======================================================
        // EVENT HOOKS (Banner mode) — limited to special game events only
        // =======================================================
        void EvWave(string m, float s) => ShowBanner(m, s);           // e.g., "⚡ Speeding up!"
        void EvChStart(ChallengeKind k)
        {
            switch (k)
            {
                case ChallengeKind.BananaBlitz: ShowBanner("🍌 Banana Blitz!"); break;
                case ChallengeKind.BombStorm: ShowBanner("💣 Bomb Storm!"); break;
                case ChallengeKind.GoldenTime: ShowBanner("⭐ Golden Time!"); break;
            }
        }
        void EvChEnd(ChallengeKind k) => ShowBanner("✔ Challenge complete", 1.2f);

        // NOTE: We intentionally DO NOT subscribe to power-ups, streaks, or lifecycle banners.
    }
}
