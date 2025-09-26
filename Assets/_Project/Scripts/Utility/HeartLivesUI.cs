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

        void Awake()
        {
            if (!heartsParent) heartsParent = transform;
            _hearts.Clear();
            for (int i = 0; i < heartsParent.childCount; i++)
            {
                var img = heartsParent.GetChild(i).GetComponent<Image>();
                if (img) _hearts.Add(img);
            }
            // initialize from whatever LifeManager set before start, or default all full
            SetHearts(_hearts.Count);
        }

        void OnEnable() { GameEvents.OnLivesChanged += SetHearts; }
        void OnDisable() { GameEvents.OnLivesChanged -= SetHearts; }

        void SetHearts(int currentLives)
        {
            for (int i = 0; i < _hearts.Count; i++)
            {
                bool full = i < currentLives;
                if (_hearts[i]) _hearts[i].sprite = full ? fullHeart : emptyHeart;
            }
        }
    }
}
