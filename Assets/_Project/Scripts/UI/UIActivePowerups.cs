using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CatchTheFruit
{
    /// <summary>
    /// Simple badge row that shows active power-ups with icons and countdown.
    /// Creates UI elements at runtime (no prefab). Uses unscaled time.
    /// </summary>
    public class UIActivePowerups : MonoBehaviour
    {
        [Header("Parent for badges (a RectTransform under HUD)")]
        public RectTransform container;

        [Header("Badge visuals")]
        public Vector2 badgeSize = new Vector2(80, 80);
        public Color iconTint = Color.black;
        public Color ringColor = new Color(1f, 1f, 1f, 0.9f);
        public TMP_FontAsset font; // optional; if null it uses default

        private class Entry
        {
            public PowerupDef def;
            public GameObject go;
            public Image icon;
            public Image ring;
            public TMP_Text label;
            public float endTime;   // unscaled
        }

        private readonly Dictionary<string, Entry> _active = new();

        private void OnEnable()
        {
            GameEvents.OnPowerupStarted += OnStart;
            GameEvents.OnPowerupEnded += OnEnd;
        }
        private void OnDisable()
        {
            GameEvents.OnPowerupStarted -= OnStart;
            GameEvents.OnPowerupEnded -= OnEnd;
            foreach (var e in _active.Values) if (e.go) Destroy(e.go);
            _active.Clear();
        }

        private void Update()
        {
            // Update countdown fills
            float now = Time.unscaledTime;
            foreach (var e in _active.Values)
            {
                if (e.def.duration > 0f)
                {
                    float remain = Mathf.Max(0f, e.endTime - now);
                    float t = Mathf.Clamp01(remain / e.def.duration);
                    if (e.ring) e.ring.fillAmount = t;
                    if (e.label) e.label.text = Mathf.CeilToInt(remain).ToString();
                }
                else
                {
                    if (e.ring) e.ring.fillAmount = 0f;
                    if (e.label) e.label.text = ""; // no timer
                }
            }
        }

        private void OnStart(PowerupDef def)
        {
            if (!container || def == null) return;

            // If exists, refresh its end time; else create a new badge.
            if (_active.TryGetValue(def.id, out var entry))
            {
                entry.endTime = Time.unscaledTime + Mathf.Max(0.01f, def.duration);
                return;
            }

            var go = new GameObject($"PU_{def.id}", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(container, false);
            rt.sizeDelta = badgeSize;

            // Icon
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.SetParent(rt, false);
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = badgeSize * 0.8f;
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = def.icon;
            iconImg.color = iconTint;
            iconImg.preserveAspect = true;

            // Ring (radial fill)
            var ringGO = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            var ringRT = ringGO.GetComponent<RectTransform>();
            ringRT.SetParent(rt, false);
            ringRT.anchorMin = ringRT.anchorMax = new Vector2(0.5f, 0.5f);
            ringRT.sizeDelta = badgeSize;
            var ringImg = ringGO.GetComponent<Image>();
            ringImg.sprite = null;                  // simple filled circle
            ringImg.color = ringColor;
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillClockwise = false;
            ringImg.fillAmount = 1f;

            // Label (seconds)
            var labelGO = new GameObject("Sec", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.SetParent(rt, false);
            labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0.5f);
            labelRT.sizeDelta = badgeSize;
            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.fontSize = badgeSize.y * 0.35f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = "";

            _active[def.id] = new Entry
            {
                def = def,
                go = go,
                icon = iconImg,
                ring = ringImg,
                label = tmp,
                endTime = Time.unscaledTime + Mathf.Max(0.01f, def.duration)
            };
        }

        private void OnEnd(PowerupDef def)
        {
            if (def == null) return;
            if (_active.TryGetValue(def.id, out var e))
            {
                if (e.go) Destroy(e.go);
                _active.Remove(def.id);
            }
        }
    }
}
/*
Unity:
Canvas → Panel_HUD → Create Empty "HUD_Badges" (add HorizontalLayoutGroup if you like).
Add UIActivePowerups to a new object "UI_PowerupsHUD" and drag HUD_Badges to 'container'.
Optionally assign a TMP Font asset. Done — badges appear when power-ups start.
*/
