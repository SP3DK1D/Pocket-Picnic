// Assets/_Project/Scripts/Quests/QuestScreenController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Minimal quest screen controller:
    /// - Two tabs: Daily & Achievements (optional: assign both).
    /// - Instantiates a simple item prefab with: Text title, Text progress, Slider bar.
    /// </summary>
    public class QuestScreenController : MonoBehaviour
    {
        [Header("Tabs (optional)")]
        [SerializeField] private GameObject dailyTabRoot;
        [SerializeField] private GameObject achievementsTabRoot;

        [Header("Lists")]
        [SerializeField] private Transform dailyListParent;
        [SerializeField] private Transform achListParent;

        [Header("Item Prefab")]
        [SerializeField] private QuestItemUI itemPrefab;

        [Header("Tab Buttons (optional)")]
        [SerializeField] private Button dailyButton;
        [SerializeField] private Button achButton;

        readonly List<QuestItemUI> _spawned = new();

        void OnEnable()
        {
            if (dailyButton) dailyButton.onClick.AddListener(ShowDaily);
            if (achButton)   achButton.onClick.AddListener(ShowAchievements);

            BuildUI();
            ShowDaily();
        }

        void OnDisable()
        {
            if (dailyButton) dailyButton.onClick.RemoveListener(ShowDaily);
            if (achButton)   achButton.onClick.RemoveListener(ShowAchievements);
            ClearUI();
        }

        void BuildUI()
        {
            ClearUI();
            var qm = QuestManager.Instance;
            if (!qm || !itemPrefab) return;

            // Daily
            if (dailyListParent)
            {
                foreach (var q in qm.DailyQuests)
                {
                    var ui = Instantiate(itemPrefab, dailyListParent);
                    ui.Bind(q);
                    _spawned.Add(ui);
                }
            }

            // Achievements
            if (achListParent)
            {
                foreach (var a in qm.Achievements)
                {
                    var ui = Instantiate(itemPrefab, achListParent);
                    ui.Bind(a);
                    _spawned.Add(ui);
                }
            }
        }

        void ClearUI()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            _spawned.Clear();
        }

        public void ShowDaily()
        {
            if (dailyTabRoot) dailyTabRoot.SetActive(true);
            if (achievementsTabRoot) achievementsTabRoot.SetActive(false);
        }

        public void ShowAchievements()
        {
            if (dailyTabRoot) dailyTabRoot.SetActive(false);
            if (achievementsTabRoot) achievementsTabRoot.SetActive(true);
        }
    }
}
