using UnityEngine;

public class CameraShaker2D : MonoBehaviour
{
    Vector3 _basePos; float _endTime; float _amp;

    void Awake() => _basePos = transform.localPosition;

    public void Shake(float amplitude = 0.05f, float duration = 0.1f)
    {
        _amp = amplitude;
        _endTime = Time.unscaledTime + Mathf.Max(0.01f, duration);
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < _endTime)
        {
            float x = (Random.value * 2f - 1f) * _amp;
            float y = (Random.value * 2f - 1f) * _amp;
            transform.localPosition = _basePos + new Vector3(x, y, 0f);
        }
        else
        {
            transform.localPosition = _basePos;
        }
    }
}
