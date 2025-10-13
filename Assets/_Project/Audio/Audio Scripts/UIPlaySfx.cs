using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    [RequireComponent(typeof(Selectable))]
    public class UIPlaySfx : MonoBehaviour
    {
        void Awake()
        {
            if (TryGetComponent<Button>(out var button))
            {
                button.onClick.AddListener(Play);
                return;
            }
            if (TryGetComponent<Toggle>(out var toggle))
            {
                toggle.onValueChanged.AddListener(_ => Play());
                return;
            }
            Debug.LogWarning("[UIPlaySfx] Attached to unsupported UI element.", this);
        }

        void Play()
        {
            AudioManager.I?.PlayUIButton();
        }
    }
}
