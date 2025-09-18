using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    Rect _last;
    RectTransform _rt;

    void Awake() { _rt = (RectTransform)transform; Apply(); }
    void OnEnable() => Apply();
#if UNITY_EDITOR
    void Update() { Apply(); } // helps in Device Simulator / editor resize
#endif
    void Apply()
    {
        var sa = Screen.safeArea;
        if (sa == _last) return;
        _last = sa;

        Vector2 anchorMin = sa.position;
        Vector2 anchorMax = sa.position + sa.size;
        anchorMin.x /= Screen.width; anchorMax.x /= Screen.width;
        anchorMin.y /= Screen.height; anchorMax.y /= Screen.height;

        _rt.anchorMin = anchorMin;
        _rt.anchorMax = anchorMax;
        _rt.offsetMin = _rt.offsetMax = Vector2.zero;
    }
}

