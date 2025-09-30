using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Options menu controller:
    /// - Mute SFX / Mute Music toggles (persisted)
    /// - Terms of Service / Privacy links
    /// - Close returns to Pause (if opened from Pause) or Main Menu.
    /// Works with AudioManager (preferred) and legacy AudioHub (if present).
    /// </summary>
    public class OptionManager : MonoBehaviour
    {
        [Header("Toggles (assign)")]
        [SerializeField] private Toggle muteSfxToggle;    // isOn == muted
        [SerializeField] private Toggle muteMusicToggle;  // isOn == muted

        [Header("Links")]
        [SerializeField] private string termsOfServiceUrl = "https://example.com/terms";
        [SerializeField] private string privacyPolicyUrl = "https://example.com/privacy";

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // --- Volume defaults ---
        private const float DEFAULT_SFX_VOL = 0.45f;
        private const float DEFAULT_MUSIC_VOL = 0.05f;

        const string K_MUTE_SFX = "opt_mute_sfx";
        const string K_MUTE_MUSIC = "opt_mute_music";

        void Awake()
        {
            // Make sure we can’t null-ref if toggles aren’t assigned yet
            if (!muteSfxToggle) muteSfxToggle = GetComponentInChildren<Toggle>(true);
        }

        void OnEnable()
        {
            // Read persisted mute flags
            bool muteSfx = PlayerPrefs.GetInt(K_MUTE_SFX, 0) == 1;
            bool muteMusic = PlayerPrefs.GetInt(K_MUTE_MUSIC, 0) == 1;

            // Initialize toggles without firing callbacks
            if (muteSfxToggle)
            {
                muteSfxToggle.onValueChanged.RemoveListener(OnToggleMuteSfx);
                muteSfxToggle.isOn = muteSfx; // ON means "muted"
                muteSfxToggle.onValueChanged.AddListener(OnToggleMuteSfx);
            }
            else if (verboseLogs) Debug.LogWarning("[Options] SFX toggle not assigned.", this);

            if (muteMusicToggle)
            {
                muteMusicToggle.onValueChanged.RemoveListener(OnToggleMuteMusic);
                muteMusicToggle.isOn = muteMusic; // ON means "muted"
                muteMusicToggle.onValueChanged.AddListener(OnToggleMuteMusic);
            }
            else if (verboseLogs) Debug.LogWarning("[Options] Music toggle not assigned.", this);

            // Apply current state to audio backends right away
            ApplyMuteSfx(muteSfx);
            ApplyMuteMusic(muteMusic);
        }

        // ====== Toggle handlers ======
        public void OnToggleMuteSfx(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] SFX muted={isMuted}", this);
            PlayerPrefs.SetInt(K_MUTE_SFX, isMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMuteSfx(isMuted);
        }

        public void OnToggleMuteMusic(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] MUSIC muted={isMuted}", this);
            PlayerPrefs.SetInt(K_MUTE_MUSIC, isMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMuteMusic(isMuted);
        }

        void ApplyMuteSfx(bool mute)
        {
            float vol = mute ? 0f : DEFAULT_SFX_VOL;

            // Prefer AudioManager if present
            if (AudioManager.Instance)
            {
                AudioManager.Instance.SetSFX(vol);
                if (verboseLogs) Debug.Log($"[Options] AudioManager.SetSFX({vol})", this);
            }

            // Mirror to legacy AudioHub if present (JuiceAudioListeners uses AudioHub)
            if (AudioHub.I)
            {
                AudioHub.I.SetSfxVolume(vol);
                if (verboseLogs) Debug.Log($"[Options] AudioHub.SetSfxVolume({vol})", this);
            }

            // Optional: broadcast to SettingsManager (if other systems listen)
            if (SettingsManager.SfxOn != !mute)
                SettingsManager.SetSfx(!mute);
        }

        void ApplyMuteMusic(bool mute)
        {
            float vol = mute ? 0f : DEFAULT_MUSIC_VOL;

            if (AudioManager.Instance)
            {
                AudioManager.Instance.SetMusic(vol);
                if (verboseLogs) Debug.Log($"[Options] AudioManager.SetMusic({vol})", this);
            }
            if (AudioHub.I)
            {
                AudioHub.I.SetMusicVolume(vol);
                if (verboseLogs) Debug.Log($"[Options] AudioHub.SetMusicVolume({vol})", this);
            }
        }

        // ====== Links ======
        public void OpenTerms()  { if (!string.IsNullOrEmpty(termsOfServiceUrl)) Application.OpenURL(termsOfServiceUrl); }
        public void OpenPrivacy(){ if (!string.IsNullOrEmpty(privacyPolicyUrl)) Application.OpenURL(privacyPolicyUrl); }

        // ====== Close ======
        public void Close()
        {
            // We merged to a single controller; only reference MenuFlowController.
            var mf = FindController<MenuFlowController>(true);
            if (mf) { mf.OnOptionsClose(); return; }

            // Fallback: just hide this panel
            gameObject.SetActive(false);
        }

        // Version-safe find helper
        static T FindController<T>(bool includeInactive) where T : MonoBehaviour
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
#pragma warning disable 618
            return Object.FindObjectOfType<T>(includeInactive);
#pragma warning restore 618
#endif
        }
    }
}
