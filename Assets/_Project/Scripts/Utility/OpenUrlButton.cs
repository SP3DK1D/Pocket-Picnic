using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Opens a URL and plays the UI click SFX. Attach to a Button and wire
    /// the OnClick to call Open(). No AudioHub dependency.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenUrlButton : MonoBehaviour
    {
        [SerializeField] private string url = "https://sites.google.com/view/pocket-picnic/home";

        public void Open()
        {
            AudioManager.I?.PlayUIButton();   // was AudioHub.I.PlayButton();
            if (!string.IsNullOrEmpty(url))
                Application.OpenURL(url);
            else
                Debug.LogWarning("[OpenUrlButton] URL is empty.", this);
        }
    }
}
