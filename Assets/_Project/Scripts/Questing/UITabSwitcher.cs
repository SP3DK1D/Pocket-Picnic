using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    public class UITabSwitcher : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button btnDaily;
        [SerializeField] private Button btnAchievements;

        [Header("Roots")]
        [SerializeField] private GameObject dailyRoot;
        [SerializeField] private GameObject achRoot;

        void Awake()
        {
            btnDaily.onClick.AddListener(() => ShowTab(true));
            btnAchievements.onClick.AddListener(() => ShowTab(false));
            ShowTab(true); // start on Daily
        }

        void ShowTab(bool daily)
        {
            if (dailyRoot) dailyRoot.SetActive(daily);
            if (achRoot)   achRoot.SetActive(!daily);
        }
    }
}
