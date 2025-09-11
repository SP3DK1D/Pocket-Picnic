using TMPro;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Toggles Menu/HUD/GameOver panels; relays button clicks to events.
    /// </summary>
    public class UIMenuController : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject menuPanel;
        public GameObject hudPanel;
        public GameObject overPanel;

        [Header("Final Score (GameOver)")]
        public TMP_Text finalScoreText;

        private int _lastScore;

        private void OnEnable()
        {
            GameEvents.OnScoreChanged += CacheScore;
            GameEvents.OnGameStart += ShowHUD;
            GameEvents.OnGameOver += ShowOver;
        }
        private void OnDisable()
        {
            GameEvents.OnScoreChanged -= CacheScore;
            GameEvents.OnGameStart -= ShowHUD;
            GameEvents.OnGameOver -= ShowOver;
        }

        private void Start() => ShowMenu();

        private void CacheScore(int s) => _lastScore = s;

        public void ShowMenu()
        {
            menuPanel?.SetActive(true);
            hudPanel?.SetActive(false);
            overPanel?.SetActive(false);
        }

        private void ShowHUD()
        {
            menuPanel?.SetActive(false);
            hudPanel?.SetActive(true);
            overPanel?.SetActive(false);
        }

        private void ShowOver()
        {
            menuPanel?.SetActive(false);
            hudPanel?.SetActive(false);
            overPanel?.SetActive(true);
            if (finalScoreText) finalScoreText.text = $"Score: {_lastScore}";
        }

        // Button hooks (assign in Inspector)
        public void Btn_Start() => GameEvents.RaiseGameStart();
        public void Btn_Menu() => ShowMenu();
    }
}
/*
UNITY IMPLEMENTATION
1) Canvas children: Panel_Menu, Panel_HUD, Panel_GameOver (Menu active, others inactive).
2) Create 'UI_Menu' → add UIMenuController → assign the 3 panels + FinalScore text.
3) Buttons:
   - Start button OnClick → UI_Menu.UIMenuController.Btn_Start()
   - Menu  button OnClick → UI_Menu.UIMenuController.Btn_Menu()
*/
