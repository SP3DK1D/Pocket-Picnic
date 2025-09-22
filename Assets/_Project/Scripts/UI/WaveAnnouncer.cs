using UnityEngine;
using TMPro;

namespace CatchTheFruit
{
    /// <summary>
    /// Shows temporary on-screen text when GameEvents.RaiseWaveMessage is called.
    /// Attach to a TMP_Text in the HUD.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class WaveAnnouncer : MonoBehaviour
    {
        TMP_Text _txt;
        float _t;

        void Awake() { _txt = GetComponent<TMP_Text>(); _txt.text = ""; }
        void OnEnable() { GameEvents.OnWaveMessage += Show; }
        void OnDisable() { GameEvents.OnWaveMessage -= Show; }

        void Show(string msg, float seconds)
        {
            _txt.text = msg;
            _t = seconds;
        }

        void Update()
        {
            if (_t > 0f)
            {
                _t -= Time.unscaledDeltaTime;
                if (_t <= 0f) _txt.text = "";
            }
        }
    }
}
