using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Attach to any UI Button to open a web URL (e.g., Privacy Policy).
    /// Works on iOS/Android/PC/WebGL. Use HTTPS to satisfy iOS ATS.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenUrlButton : MonoBehaviour
    {
        [Header("URL to open (HTTPS)")]
        [SerializeField] private string url = "https://sites.google.com/view/pocket-picnic/home";

        [Header("Optional")]
        [Tooltip("Play a click sound if your audio systems are present.")]
        [SerializeField] private bool playClickSfx = true;

        Button _btn;

        void Awake()
        {
            _btn = GetComponent<Button>();
            _btn.onClick.AddListener(OpenUrl);
        }

        void OnDestroy()
        {
            if (_btn) _btn.onClick.RemoveListener(OpenUrl);
        }

        public void OpenUrl()
        {
            if (playClickSfx)
            {
                // Safely support either of your audio singletons if present
                CatchTheFruit.AudioManager.Instance?.PlayUIButtonClick();
                AudioHub.I?.PlayButton();
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("[OpenUrlButton] URL is empty.");
                return;
            }

            // Basic guard: iOS prefers HTTPS (ATS). Non-HTTPS may be blocked.
            if (!url.StartsWith("https://"))
                Debug.LogWarning("[OpenUrlButton] Use HTTPS URLs to satisfy App Transport Security on iOS.");

            Application.OpenURL(url.Trim());
        }

        // Helper for setting from code, e.g., per-locale
        public void SetUrl(string newUrl) => url = newUrl;
    }
}
