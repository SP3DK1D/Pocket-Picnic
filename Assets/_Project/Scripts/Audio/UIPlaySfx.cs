using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Attach to a Button or Toggle to auto-play the button click SFX from AudioHub.
    /// Works with both clicks (Button) and state changes (Toggle).
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIPlaySfx : MonoBehaviour
    {
        private void Awake()
        {
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(Play);
                return;
            }

            var toggle = GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(_ => Play());
                return;
            }

            Debug.LogWarning("[UIPlaySfx] Attached to unsupported UI element.", this);
        }

        private void Play()
        {
            if (AudioHub.I != null)
                AudioHub.I.PlayButton();
        }
    }
}
