using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace CatchTheFruit
{
    /// <summary>
    /// Simple pause menu controller. Shows a panel, hooks up Pause/Resume/Restart.
    /// </summary>
    public class PauseUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel;   // the pause panel root (set inactive by default)
        public Button btnPause;    // button on HUD to open pause
        public Button btnResume;   // button inside panel
        public Button btnRestart;  // button inside panel

        void Awake()
        {
            if (btnPause) btnPause.onClick.AddListener(Open);
            if (btnResume) btnResume.onClick.AddListener(Close);
            if (btnRestart) btnRestart.onClick.AddListener(Restart);

            Show(false);
        }

        void Open()
        {
            PauseManager.Instance?.Pause();
            Show(true);
        }

        void Close()
        {
            PauseManager.Instance?.Resume();
            Show(false);
        }

        void Restart()
        {
            // always resume before reload so editor doesn't stay frozen
            PauseManager.Instance?.Resume();
            Show(false);
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }

        void Show(bool show)
        {
            if (panel) panel.SetActive(show);
        }
    }
}
