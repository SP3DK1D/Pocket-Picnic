// Assets/_Project/Scripts/Quests/QuestItemUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Simple view for a quest row. Assign references in the prefab.
    /// </summary>
    public class QuestItemUI : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text progressText;
        [SerializeField] private Slider progressBar;

        Quest _bound;

        public void Bind(Quest q)
        {
            _bound = q;
            Refresh();
        }

        void Update()
        {
            // cheap live refresh
            if (_bound != null) Refresh();
        }

        void Refresh()
        {
            if (_bound == null || _bound.def == null) return;

            if (titleText) titleText.text = _bound.def.title;

            int cur = Mathf.Clamp(_bound.progress, 0, _bound.def.target);
            int tar = Mathf.Max(1, _bound.def.target);
            if (progressText) progressText.text = $"{cur}/{tar}";
            if (progressBar)  progressBar.value = Mathf.Clamp01(_bound.Progress01);
        }
    }
}
