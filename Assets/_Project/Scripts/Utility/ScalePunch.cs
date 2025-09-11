using UnityEngine;

public class ScalePunch : MonoBehaviour
{
    [Tooltip("Target scale multiplier for the 'pop' (x,y).")]
    public Vector2 punch = new Vector2(1.18f, 0.88f);
    [Tooltip("Seconds (unscaled) for the whole pop cycle.")]
    public float duration = 0.15f;
    [Tooltip("How strong the return overshoots (0..1).")]
    [Range(0f, 1f)] public float ease = 0.35f;

    Vector3 _base; float _t; bool _playing;

    void Awake() => _base = transform.localScale;

    public void Play()
    {
        _t = 0f; _playing = true;
        transform.localScale = new Vector3(_base.x * punch.x, _base.y * punch.y, _base.z);
    }

    void Update()
    {
        if (!_playing) return;
        _t += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_t / duration);
        // ease back to base with a tiny overshoot
        float s = 1f + (-Mathf.Cos(t * Mathf.PI) * 0.5f + 0.5f) * ease; // 1..(1+ease)..1 curve
        transform.localScale = Vector3.Lerp(
            new Vector3(_base.x * punch.x, _base.y * punch.y, _base.z),
            _base, s);
        if (t >= 1f) { transform.localScale = _base; _playing = false; }
    }
}
