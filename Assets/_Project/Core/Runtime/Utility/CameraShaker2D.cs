using UnityEngine;

//
// CameraShaker2D
// -----------------------------------------------------------------------------
// Lightweight 2D screen shake that works with unscaled time (so it also shakes
// during slow-mo / pause overlays if you call it while timeScale=0 but keep
// updating via unscaled time).
//
// New (optional):
// - Deterministic / seedable noise: enable in Inspector for repeatable shakes
//   (useful for trailers or automated tests).
// - Frequency control for a tighter/looser jitter.
// - Decay curve: linear by default, but exposed for quick tuning.
//
// Backward compatibility:
// - Same public Shake(amplitude, duration) signature kept.
//
public class CameraShaker2D : MonoBehaviour
{
    [Header("Defaults (used if no params provided)")]
    [Tooltip("Default peak offset (world units). Typical 0.04–0.12.")]
    public float defaultAmplitude = 0.05f;

    [Tooltip("Default duration (seconds, unscaled).")]
    public float defaultDuration = 0.10f;

    [Tooltip("Samples per second for the pseudo-random jitter.")]
    [Range(10f, 120f)] public float frequency = 60f;

    [Header("Determinism (optional)")]
    [Tooltip("If ON, the noise uses a local seeded PRNG (repeatable shakes).")]
    public bool useSeededNoise = false;

    [Tooltip("Seed used when deterministic noise is enabled.")]
    public uint noiseSeed = 0xCAFE_BABE;

    [Header("Falloff")]
    [Tooltip("Curve applied to the amplitude over the 0..1 lifetime of a shake.")]
    public AnimationCurve amplitudeOverLife = AnimationCurve.Linear(0, 1, 1, 0);

    // --- runtime ---
    Vector3 _baseLocalPos;
    float _endTimeUnscaled;
    float _amp;
    float _freqTimer;

    // tiny xorshift32 for deterministic noise (fast & self-contained)
    uint _state;

    void Awake()
    {
        _baseLocalPos = transform.localPosition;
        _state = noiseSeed;
    }

    /// <summary>
    /// Triggers a shake. If already shaking, this restarts with the new values.
    /// </summary>
    public void Shake(float amplitude = -1f, float duration = -1f)
    {
        if (amplitude <= 0f) amplitude = defaultAmplitude;
        if (duration <= 0f) duration = defaultDuration;

        _amp = Mathf.Max(0f, amplitude);
        _endTimeUnscaled = Time.unscaledTime + Mathf.Max(0.0001f, duration);
        _freqTimer = 0f;

        // re-seed so repeated calls with same params look the same when deterministic
        if (useSeededNoise)
            _state = noiseSeed;
    }

    void LateUpdate()
    {
        float now = Time.unscaledTime;

        if (now < _endTimeUnscaled)
        {
            // normalized life 0..1
            float lifeT = 1f - Mathf.InverseLerp(now, _endTimeUnscaled, now + 0.000001f);
            float k = Mathf.Clamp01(1f - ((_endTimeUnscaled - now) / Mathf.Max(0.000001f, _endTimeUnscaled - (now - Time.unscaledDeltaTime))));
            // simple normalized time across duration
            float t = 1f - Mathf.InverseLerp(_endTimeUnscaled - defaultDuration, _endTimeUnscaled, now);
            float falloff = Mathf.Clamp01(amplitudeOverLife.Evaluate(Mathf.Clamp01(t)));

            // step noise at fixed frequency (prevents ultra-high-freq jitter on fast machines)
            _freqTimer += Time.unscaledDeltaTime;
            bool step = _freqTimer >= (1f / Mathf.Max(1f, frequency));
            if (step) _freqTimer = 0f;

            // random in [-1,1] for x/y
            float rx = (Rand01() * 2f - 1f);
            float ry = (Rand01() * 2f - 1f);

            Vector3 offset = new Vector3(rx, ry, 0f) * (_amp * falloff);
            transform.localPosition = _baseLocalPos + offset;
        }
        else
        {
            transform.localPosition = _baseLocalPos; // hard snap back
        }
    }

    // Returns 0..1 uniform
    float Rand01()
    {
        if (!useSeededNoise)
            return Random.value;

        // xorshift32
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        // map to 0..1
        return (_state & 0xFFFFFF) / 16777215f; // 2^24-1
    }
}
