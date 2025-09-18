using UnityEngine;

namespace CatchTheFruit
{
    public class StartButtonPlayerActivator : MonoBehaviour
    {
        [Header("Assign in Inspector")]
        [SerializeField] private GameObject player;          // your Player GO (starts disabled)
        [SerializeField] private GameObject startPanel;      // Start menu panel
        [SerializeField] private GameObject difficultyPanel; // Difficulty picker panel

        private void Awake()
        {
            // Ensure correct boot state
            if (player) player.SetActive(false);
            if (startPanel) startPanel.SetActive(true);
            if (difficultyPanel) difficultyPanel.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            GameEvents.OnGameOver -= HandleGameOver;
        }

        // Hook this to the Start button OnClick
        public void OnPressStart()
        {
            // Move to difficulty screen; keep player hidden
            if (startPanel) startPanel.SetActive(false);
            if (difficultyPanel) difficultyPanel.SetActive(true);
            if (player) player.SetActive(false);
        }

        // Hook these to the three difficulty buttons
        public void OnPickEasy() => ApplyDifficultyAndStart(DifficultyChoice.Easy);
        public void OnPickMedium() => ApplyDifficultyAndStart(DifficultyChoice.Medium);
        public void OnPickHard() => ApplyDifficultyAndStart(DifficultyChoice.Hard);

        private enum DifficultyChoice { Easy, Medium, Hard }

        private void ApplyDifficultyAndStart(DifficultyChoice choice)
        {
            // If you added DifficultyManager, set the preset here (UI stays managed by THIS script)
            if (DifficultyManagerExists())
            {
                switch (choice)
                {
                    case DifficultyChoice.Easy: DifficultyManagerPickEasy(); break;
                    case DifficultyChoice.Medium: DifficultyManagerPickMedium(); break;
                    case DifficultyChoice.Hard: DifficultyManagerPickHard(); break;
                }
            }

            // Now actually start gameplay: show player, hide difficulty, raise event
            if (player) player.SetActive(true);
            if (difficultyPanel) difficultyPanel.SetActive(false);

            GameEvents.RaiseGameStart();
        }

        private void HandleGameOver()
        {
            // Hide player on lose/quit; show start panel so they can play again
            if (player) player.SetActive(false);
            if (startPanel) startPanel.SetActive(true);
            if (difficultyPanel) difficultyPanel.SetActive(false);

            // Optional: clear chosen difficulty if you want to force re-pick next time
            if (DifficultyManagerExists()) DifficultyManagerClear();
        }

        // --- DifficultyManager shims (safe if you didn't add it) ---
        private bool DifficultyManagerExists()
        {
            // If you have a DifficultyManager in scene, we’ll call its static API.
            // If not, these calls no-op and everything still works.
            return typeof(DifficultyManager) != null;
        }
        private void DifficultyManagerPickEasy()
        {
            // If your DifficultyManager already also activates the player or raises GameStart,
            // disable those lines there to avoid double-activations.
            try { DifficultyManager dm = FindObjectOfType<DifficultyManager>(); dm?.PickEasy(); } catch { }
        }
        private void DifficultyManagerPickMedium()
        {
            try { DifficultyManager dm = FindObjectOfType<DifficultyManager>(); dm?.PickMedium(); } catch { }
        }
        private void DifficultyManagerPickHard()
        {
            try { DifficultyManager dm = FindObjectOfType<DifficultyManager>(); dm?.PickHard(); } catch { }
        }
        private void DifficultyManagerClear()
        {
            try { DifficultyManager.ClearCurrent(); } catch { }
        }
    }
}
