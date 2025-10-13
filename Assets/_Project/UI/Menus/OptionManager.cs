using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Centralizes SFX/Music mute toggles for both Options and Pause panels.
    /// Mirrors UI from AudioManager and keeps both panels in sync.
    /// Also provides an Exit button for the Options panel.
    ///
    /// IMPORTANT: These toggles are *Mute* toggles, so:
    /// Toggle.isOn == true  => Muted
    /// Toggle.isOn == false => Not muted (audio audible)
    ///
    /// Hookup (Inspector):
    /// - Drop this on a persistent UI object (Options/Pause canvas root is fine).
    /// - Assign: optionsMuteSfxToggle, optionsMuteMusicToggle, pauseMuteSfxToggle, pauseMuteMusicToggle.
    /// - (New) Assign: optionsExitButton (the "Close" / "Back" button in Options panel).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionManager : MonoBehaviour
    {
        public static OptionManager Instance { get; private set; }

        [Header("Options Panel Toggles (assign)")]
        [SerializeField] private Toggle optionsMuteSfxToggle;    // isOn == muted
        [SerializeField] private Toggle optionsMuteMusicToggle;  // isOn == muted

        [Header("Pause Panel Toggles (assign)")]
        [SerializeField] private Toggle pauseMuteSfxToggle;      // isOn == muted
        [SerializeField] private Toggle pauseMuteMusicToggle;    // isOn == muted

        [Header("Options Panel Exit (assign)")]
        [SerializeField] private Button optionsExitButton;       // NEW: close Options panel

        [Header("Links (optional)")]
        [SerializeField] private string termsOfServiceUrl = "https://example.com/terms";
        [SerializeField] private string privacyPolicyUrl = "https://example.com/privacy";

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // Legacy keys kept for compatibility with any old code (harmless to keep writing them)
        const string K_MUTE_SFX = "opt_mute_sfx";
        const string K_MUTE_MUSIC = "opt_mute_music";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Keep enabled between panel opens so we continue mirroring events.
        }

        void OnEnable()
        {
            // Subscribe to AudioManager events so any change (from either panel)
            // mirrors across both sets of toggles.
            var A = AudioManager.I;
            if (A != null)
            {
                A.OnMuteSfxChanged += HandleMuteSfxChanged;
                A.OnMuteMusicChanged += HandleMuteMusicChanged;
            }

            // Wire UI listeners (Options + Pause)
            WireToggle(optionsMuteSfxToggle, OnToggleMuteSfx);
            WireToggle(optionsMuteMusicToggle, OnToggleMuteMusic);
            WireToggle(pauseMuteSfxToggle, OnToggleMuteSfx);
            WireToggle(pauseMuteMusicToggle, OnToggleMuteMusic);

            // Wire the Exit button (Options)
            if (optionsExitButton != null)
            {
                optionsExitButton.onClick.RemoveListener(OnOptionsExitClicked);
                optionsExitButton.onClick.AddListener(OnOptionsExitClicked);
            }

            // Refresh visuals from current audio state
            SyncUIFromAudio();
        }

        void OnDisable()
        {
            var A = AudioManager.I;
            if (A != null)
            {
                A.OnMuteSfxChanged -= HandleMuteSfxChanged;
                A.OnMuteMusicChanged -= HandleMuteMusicChanged;
            }

            UnwireToggle(optionsMuteSfxToggle, OnToggleMuteSfx);
            UnwireToggle(optionsMuteMusicToggle, OnToggleMuteMusic);
            UnwireToggle(pauseMuteSfxToggle, OnToggleMuteSfx);
            UnwireToggle(pauseMuteMusicToggle, OnToggleMuteMusic);

            if (optionsExitButton != null)
                optionsExitButton.onClick.RemoveListener(OnOptionsExitClicked);

            if (Instance == this) Instance = null;
        }

        // ========= Public: called by MenuFlowController when opening panels =========
        /// <summary>
        /// Keeps all toggles in both panels in sync with AudioManager.
        /// SAFE: Uses SetIsOnWithoutNotify to avoid recursive callbacks.
        /// </summary>
        public static void SyncUIFromAudio()
        {
            var inst = Instance;
            var A = AudioManager.I;
            if (!inst || A == null) return;

            bool muteSfx = A.IsSfxMuted;
            bool muteMusic = A.IsMusicMuted;

            inst.SetToggleWithoutNotify(inst.optionsMuteSfxToggle, muteSfx);
            inst.SetToggleWithoutNotify(inst.optionsMuteMusicToggle, muteMusic);
            inst.SetToggleWithoutNotify(inst.pauseMuteSfxToggle, muteSfx);
            inst.SetToggleWithoutNotify(inst.pauseMuteMusicToggle, muteMusic);

            if (inst.verboseLogs) Debug.Log($"[Options] Sync UI → SFXMuted={muteSfx}, MusicMuted={muteMusic}", inst);
        }

        // ========= Toggle handlers (from either panel) =========
        void OnToggleMuteSfx(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] Mute SFX = {isMuted}", this);

            // Keep writing legacy keys for compatibility (optional)
            PlayerPrefs.SetInt(K_MUTE_SFX, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            // Drive AudioManager (this also fires its OnMuteSfxChanged event)
            AudioManager.I?.SetMuteSFX(isMuted);
        }

        void OnToggleMuteMusic(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] Mute Music = {isMuted}", this);

            // Keep writing legacy keys for compatibility (optional)
            PlayerPrefs.SetInt(K_MUTE_MUSIC, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            // Drive AudioManager (this also fires its OnMuteMusicChanged event)
            AudioManager.I?.SetMuteMusic(isMuted);
        }

        // ========= Event mirroring from AudioManager =========
        void HandleMuteSfxChanged(bool isMuted)
        {
            SetToggleWithoutNotify(optionsMuteSfxToggle, isMuted);
            SetToggleWithoutNotify(pauseMuteSfxToggle, isMuted);
        }

        void HandleMuteMusicChanged(bool isMuted)
        {
            SetToggleWithoutNotify(optionsMuteMusicToggle, isMuted);
            SetToggleWithoutNotify(pauseMuteMusicToggle, isMuted);
        }

        // ========= Exit button (Options) =========
        /// <summary>
        /// Closes the Options panel (routes back via MenuFlowController if present).
        /// Plays the standard UI click SFX if AudioManager is available.
        /// </summary>
        void OnOptionsExitClicked()
        {
            AudioManager.I?.PlayUIButton();

            // Prefer using the existing flow so Pause/Main return logic stays centralized.
            var mf = FindController<MenuFlowController>(true);
            if (mf != null)
            {
                mf.OnOptionsClose();
                return;
            }

            // Fallback: disable our Options panel (parent canvas/panel)
            // This assumes this script lives under the Options panel hierarchy.
            var panel = transform.root?.gameObject;
            if (panel) panel.SetActive(false);
        }

        // ========= Utility =========
        void WireToggle(Toggle t, UnityEngine.Events.UnityAction<bool> cb)
        {
            if (!t) return;
            t.onValueChanged.RemoveListener(cb);
            t.onValueChanged.AddListener(cb);
        }
        void UnwireToggle(Toggle t, UnityEngine.Events.UnityAction<bool> cb)
        {
            if (!t) return;
            t.onValueChanged.RemoveListener(cb);
        }
        void SetToggleWithoutNotify(Toggle t, bool isOn)
        {
            if (!t) return;
            t.SetIsOnWithoutNotify(isOn);
        }

        // Optional external links
        public void OpenTerms() { if (!string.IsNullOrEmpty(termsOfServiceUrl)) Application.OpenURL(termsOfServiceUrl); }
        public void OpenPrivacy() { if (!string.IsNullOrEmpty(privacyPolicyUrl)) Application.OpenURL(privacyPolicyUrl); }

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
