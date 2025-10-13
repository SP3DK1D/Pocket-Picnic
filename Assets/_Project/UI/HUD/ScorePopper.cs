using UnityEngine;
using TMPro;

/// <summary>
/// Simple, allocation-free “+X” score popper.
/// - You drive it by calling <see cref="PushScoreDelta(int)"/> each time a (non-bomb) fruit is caught.
/// - It accumulates while visible (e.g., +5 then +10 quickly shows "+15" once).
/// - Uses unscaled time so the effect looks the same during Freeze or while game is paused.
/// - No Banner mode; all event/listener code intentionally removed to keep this tiny and predictable.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[RequireComponent(typeof(CanvasGroup))]
public class ScorePopper : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long the pop stays fully visible before fading (seconds, unscaled).")]
    [Min(0f)] public float holdSeconds = 0.45f;

    [Tooltip("Fade out seconds after the hold (seconds, unscaled).")]
    [Min(0f)] public float fadeSeconds = 0.25f;

    [Header("Motion")]
    [Tooltip("How far the text floats upward during its life (in rect/pixels).")]
    public Vector2 floatUp = new Vector2(0f, 18f);

    [Header("Text")]
    [Tooltip("Prefix for the text. Usually '+'")]
    public string prefix = "+";

    // --- runtime ---
    TMP_Text _txt;
    CanvasGroup _cg;
    RectTransform _rt;
    Vector2 _basePos;
    Color _baseColor;

    int _pending;            // accumulated amount waiting to display
    bool _visible;           // currently showing?
    float _t;                // timer into the hold+fade timeline

    void Awake()
    {
        _txt = GetComponent<TMP_Text>();
        _cg = GetComponent<CanvasGroup>();
        _rt = GetComponent<RectTransform>();

        _basePos = _rt.anchoredPosition;
        _baseColor = _txt.color;

        // start hidden
        _txt.text = "";
        _cg.alpha = 0f;
        _visible = false;
        _pending = 0;
        _t = 0f;
    }

    /// <summary>
    /// Adds to the current delta. Call once per normal-fruit catch.
    /// If the popper is already visible, it resets the timeline and keeps accumulating.
    /// </summary>
    public void PushScoreDelta(int amount)
    {
        if (amount == 0) return;

        _pending += amount;

        // (Re)start the display timeline
        _t = 0f;
        _visible = true;

        // Snap to base state at the moment new points arrive
        _rt.anchoredPosition = _basePos;
        _cg.alpha = 1f;

        // Update text once here; we don't allocate every frame
        _txt.text = prefix + _pending.ToString();
    }

    /// <summary>Instantly hide and clear the pop text.</summary>
    public void Clear()
    {
        _visible = false;
        _pending = 0;
        _txt.text = "";
        _cg.alpha = 0f;
        _rt.anchoredPosition = _basePos;
        _t = 0f;
    }

    void Update()
    {
        if (!_visible) return;

        // progress using unscaled time so Freeze/pause don't change feel
        _t += Time.unscaledDeltaTime;

        float total = holdSeconds + fadeSeconds;
        float k = (total > 0f) ? Mathf.Clamp01(_t / total) : 1f;

        // Move upward smoothly over the full life
        _rt.anchoredPosition = Vector2.Lerp(_basePos, _basePos + floatUp, k);

        // Fade only after hold
        if (_t > holdSeconds)
        {
            float fk = (fadeSeconds > 0f)
                ? Mathf.Clamp01((_t - holdSeconds) / fadeSeconds)
                : 1f;
            _cg.alpha = 1f - fk;
        }

        // End of life
        if (_t >= total)
        {
            // If nothing new arrived during the fade, hide fully and reset.
            // (If new points come in later, PushScoreDelta will restart everything.)
            _visible = false;
            _pending = 0;
            _txt.text = "";
            _cg.alpha = 0f;
            _rt.anchoredPosition = _basePos;
            _t = 0f;
        }
    }
}
