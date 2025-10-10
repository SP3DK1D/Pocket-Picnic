using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
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

        // Legacy OptionManager keys (kept for compatibility)
        const string K_MUTE_SFX = "opt_mute_sfx";
        const string K_MUTE_MUSIC = "opt_mute_music";

        void Awake()
        {
            if (!muteSfxToggle) muteSfxToggle = GetComponentInChildren<Toggle>(true);
        }

        void OnEnable()
        {
            var A = AudioManager.Instance;

            // Truth: prefer AudioManager (already merged with saved prefs).
            bool muteSfx = A ? A.IsSfxMuted : PlayerPrefs.GetInt(K_MUTE_SFX, 0) == 1;
            bool muteMusic = A ? A.IsMusicMuted : PlayerPrefs.GetInt(K_MUTE_MUSIC, 0) == 1;

            // Initialize toggles WITHOUT firing callbacks
            if (muteSfxToggle)
            {
                muteSfxToggle.onValueChanged.RemoveListener(OnToggleMuteSfx);
                muteSfxToggle.SetIsOnWithoutNotify(muteSfx);
                muteSfxToggle.onValueChanged.AddListener(OnToggleMuteSfx);
            }
            else if (verboseLogs) Debug.LogWarning("[Options] SFX toggle not assigned.", this);

            if (muteMusicToggle)
            {
                muteMusicToggle.onValueChanged.RemoveListener(OnToggleMuteMusic);
                muteMusicToggle.SetIsOnWithoutNotify(muteMusic);
                muteMusicToggle.onValueChanged.AddListener(OnToggleMuteMusic);
            }
            else if (verboseLogs) Debug.LogWarning("[Options] Music toggle not assigned.", this);

            // Ensure AudioManager actually reflects these (no-ops if already same)
            ApplyMuteSfx(muteSfx);
            ApplyMuteMusic(muteMusic);
        }

        // ====== Toggle handlers ======
        public void OnToggleMuteSfx(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] SFX muted={isMuted}", this);
            PlayerPrefs.SetInt(K_MUTE_SFX, isMuted ? 1 : 0); // keep legacy flag updated
            PlayerPrefs.Save();
            ApplyMuteSfx(isMuted);
        }

        public void OnToggleMuteMusic(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] MUSIC muted={isMuted}", this);
            PlayerPrefs.SetInt(K_MUTE_MUSIC, isMuted ? 1 : 0); // keep legacy flag updated
            PlayerPrefs.Save();
            ApplyMuteMusic(isMuted);
        }

        void ApplyMuteSfx(bool mute)
        {
            // New: drive the single source of truth
            AudioManager.Instance?.SetMuteSFX(mute);

            // Legacy compatibility (if anything still reads AudioHub volumes)
            if (AudioHub.I) AudioHub.I.SetSfxVolume(mute ? 0f : AudioManager.Instance?.SfxVolume ?? 0.45f);
        }

        void ApplyMuteMusic(bool mute)
        {
            AudioManager.Instance?.SetMuteMusic(mute);
            if (AudioHub.I) AudioHub.I.SetMusicVolume(mute ? 0f : AudioManager.Instance?.MusicVolume ?? 0.05f);
        }

        // ====== Links ======
        public void OpenTerms() { if (!string.IsNullOrEmpty(termsOfServiceUrl)) Application.OpenURL(termsOfServiceUrl); }
        public void OpenPrivacy() { if (!string.IsNullOrEmpty(privacyPolicyUrl)) Application.OpenURL(privacyPolicyUrl); }

        // ====== Close ======
        public void Close()
        {
            var mf = FindController<MenuFlowController>(true);
            if (mf) { mf.OnOptionsClose(); return; }
            gameObject.SetActive(false);
        }

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
