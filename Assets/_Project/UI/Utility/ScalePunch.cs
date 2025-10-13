using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Tiny "pop" animation: briefly scales the object up/down using **unscaled time**
    /// so it looks crisp even when the game is paused or slowed.
    /// 
    /// Call <see cref="Play"/> to trigger. Safe to spam; it restarts the cycle.
    /// </summary>
    public class ScalePunch : MonoBehaviour
    {
        [Tooltip("Target scale multiplier for the 'pop' (x,y). 1.0 = no change.")]
        public Vector2 punch = new Vector2(1.18f, 0.88f);

        [Tooltip("Seconds (unscaled) for the entire pop cycle.")]
        [Min(0.01f)] public float duration = 0.15f;

        [Tooltip("How strong the return overshoots (0..1). 0 = linear back, 1 = big overshoot.")]
        [Range(0f, 1f)] public float ease = 0.35f;

        Vector3 _base;
        float _t;
        bool _playing;

        private void Awake()
        {
            _base = transform.localScale;
        }

        /// <summary>Triggers the pop from the current frame.</summary>
        public void Play()
        {
            _t = 0f;
            _playing = true;

            // Kick off at the "punched" size for instant feedback.
            transform.localScale = new Vector3(_base.x * punch.x, _base.y * punch.y, _base.z);
        }

        private void OnDisable()
        {
            // Always restore scale so disabling during an animation doesn't leave artifacts.
            transform.localScale = _base;
            _playing = false;
            _t = 0f;
        }

        private void Update()
        {
            if (!_playing) return;

            _t += Time.unscaledDeltaTime;
            float t01 = Mathf.Clamp01(_t / Mathf.Max(0.0001f, duration));

            // Ease curve: start at punch, gently overshoot back to base.
            float overshoot = 1f + (-Mathf.Cos(t01 * Mathf.PI) * 0.5f + 0.5f) * ease;

            Vector3 start = new Vector3(_base.x * punch.x, _base.y * punch.y, _base.z);
            transform.localScale = Vector3.Lerp(start, _base, overshoot);

            if (t01 >= 1f)
            {
                transform.localScale = _base;
                _playing = false;
            }
        }
    }
}
