// Assets/_Project/Scripts/Quests/CoinWalletUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Simple coins label. Assign a Text (or TMP_Text; swap as needed).
    /// </summary>
    public class CoinWalletUI : MonoBehaviour
    {
        [SerializeField] private Text coinsText;

        void OnEnable()
        {
            Refresh();
            QuestManager.OnCoinsChanged += HandleCoinsChanged;
        }
        void OnDisable()
        {
            QuestManager.OnCoinsChanged -= HandleCoinsChanged;
        }

        void HandleCoinsChanged(int total) => Refresh();

        void Refresh()
        {
            if (!coinsText) return;
            int c = QuestManager.Instance ? QuestManager.Instance.GetCoins() : 0;
            coinsText.text = c.ToString();
        }
    }
}
