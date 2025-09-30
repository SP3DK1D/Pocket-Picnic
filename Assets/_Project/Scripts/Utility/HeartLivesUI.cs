using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CatchTheFruit
{
    /// <summary>Shows lives as hearts; listens to GameEvents.OnLivesChanged.</summary>
    public class HeartLivesUI : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite fullHeart;
        [SerializeField] private Sprite emptyHeart;

        [Header("Hearts Container")]
        [SerializeField] private Transform heartsParent;   // LivesBar (this) if left null

        private readonly List<Image> _hearts = new();

        // Track live instances so reset can touch all HUDs safely.
        private static readonly HashSet<HeartLivesUI> _instances = new();

        void Awake()
        {
            if (!heartsParent) heartsParent = transform;

            _hearts.Clear();
            for (int i = 0; i < heartsParent.childCount; i++)
            {
                var img = heartsParent.GetChild(i).GetComponent<Image>();
                if (img) _hearts.Add(img);
            }

            // Safe default visual at boot = all full.
            SetHearts(_hearts.Count);
        }

        void OnEnable()
        {
            _instances.Add(this);
            GameEvents.OnLivesChanged += SetHearts;
        }

        void OnDisable()
        {
            GameEvents.OnLivesChanged -= SetHearts;
            _instances.Remove(this);
        }

        /// <summary>Public: instantly fill all hearts on this widget.</summary>
        public void SetAllFull()
        {
            for (int i = 0; i < _hearts.Count; i++)
                if (_hearts[i]) _hearts[i].sprite = fullHeart;
        }

        /// <summary>Public static: fill all visible HeartLivesUI in the scene.</summary>
        public static void ResetAllToFull()
        {
            // Iterate a copy in case UIs are being enabled/disabled during reset.
            var temp = new List<HeartLivesUI>(_instances);
            for (int i = 0; i < temp.Count; i++)
                if (temp[i]) temp[i].SetAllFull();
        }

        // Event hook
        void SetHearts(int currentLives)
        {
            int n = Mathf.Clamp(currentLives, 0, _hearts.Count);
            for (int i = 0; i < _hearts.Count; i++)
            {
                bool full = i < n;
                if (_hearts[i]) _hearts[i].sprite = full ? fullHeart : emptyHeart;
            }
        }
    }
}
