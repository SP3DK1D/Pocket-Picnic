using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CatchTheFruit
{
    /// <summary>
    /// Shows lives as hearts; listens to GameEvents.OnLivesChanged.
    /// - Discovers child <see cref="Image"/>s at Awake (or on first enable if needed).
    /// - Safe if sprites are not assigned (does nothing instead of spamming errors).
    /// - Static ResetAllToFull() fills every visible instance (handy after GameStart/Restart).
    /// </summary>
    public class HeartLivesUI : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite fullHeart;
        [SerializeField] private Sprite emptyHeart;

        [Header("Hearts Container")]
        [Tooltip("If empty, children of this GameObject are used.")]
        [SerializeField] private Transform heartsParent;

        // We keep a local list for this widget + a static set of live instances for global resets.
        readonly List<Image> _hearts = new();
        static readonly HashSet<HeartLivesUI> _instances = new();

        bool _initialized;

        void Awake()
        {
            EnsureInit();
            // Safe default visual at boot = all full.
            SetAllFull();
        }

        void OnEnable()
        {
            _instances.Add(this);
            GameEvents.OnLivesChanged += SetHearts;
            EnsureInit(); // handle cases where this enabled before Awake (rare but safe)
        }

        void OnDisable()
        {
            GameEvents.OnLivesChanged -= SetHearts;
            _instances.Remove(this);
        }

        void EnsureInit()
        {
            if (_initialized) return;

            if (!heartsParent) heartsParent = transform;

            _hearts.Clear();
            for (int i = 0; i < heartsParent.childCount; i++)
            {
                var img = heartsParent.GetChild(i).GetComponent<Image>();
                if (img) _hearts.Add(img);
            }

            _initialized = true;
        }

        /// <summary>Public: instantly fill all hearts on this widget.</summary>
        public void SetAllFull()
        {
            // If sprites are missing, just leave existing visuals alone.
            if (fullHeart == null) return;
            EnsureInit();

            for (int i = 0; i < _hearts.Count; i++)
                if (_hearts[i]) _hearts[i].sprite = fullHeart;
        }

        /// <summary>Public static: fill all visible HeartLivesUI in the scene.</summary>
        public static void ResetAllToFull()
        {
            // Iterate a copy in case UIs enable/disable during reset.
            var temp = new List<HeartLivesUI>(_instances);
            for (int i = 0; i < temp.Count; i++)
                if (temp[i]) temp[i].SetAllFull();
        }

        // Event hook
        void SetHearts(int currentLives)
        {
            EnsureInit();
            if (_hearts.Count == 0) return;

            // If sprites are missing, just skip updating.
            bool hasFull = fullHeart != null;
            bool hasEmpty = emptyHeart != null;

            int n = Mathf.Clamp(currentLives, 0, _hearts.Count);
            for (int i = 0; i < _hearts.Count; i++)
            {
                var img = _hearts[i];
                if (!img) continue;

                bool full = i < n;
                if (full && hasFull) img.sprite = fullHeart;
                else if (!full && hasEmpty) img.sprite = emptyHeart;
                // If either sprite is missing, we keep whatever was there.
            }
        }
    }
}
