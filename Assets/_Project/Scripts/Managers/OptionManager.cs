using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// Centralizes SFX/Music mute toggles for both Options and Pause panels.
    /// Mirrors UI from AudioManager and keeps both panels in sync.
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

        [Header("Links (optional)")]
        [SerializeField] private string termsOfServiceUrl = "https://example.com/terms";
        [SerializeField] private string privacyPolicyUrl = "https://example.com/privacy";

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // Legacy keys kept for compatibility with any old code
        const string K_MUTE_SFX = "opt_mute_sfx";
        const string K_MUTE_MUSIC = "opt_mute_music";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Keep this object enabled even when panels close so we keep listening to events.
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
        }

        // ===== Public: called by MenuFlowController when opening panels =====
        public static void SyncUIFromAudio()
        {
            var inst = Instance;
            var A = AudioManager.I;
            if (!inst || A == null) return;

            bool muteSfx = A.IsSfxMuted;
            bool muteMusic = A.IsMusicMuted;

            // Update all toggles WITHOUT triggering callbacks
            inst.SetToggleWithoutNotify(inst.optionsMuteSfxToggle, muteSfx);
            inst.SetToggleWithoutNotify(inst.optionsMuteMusicToggle, muteMusic);
            inst.SetToggleWithoutNotify(inst.pauseMuteSfxToggle, muteSfx);
            inst.SetToggleWithoutNotify(inst.pauseMuteMusicToggle, muteMusic);

            if (inst.verboseLogs) Debug.Log($"[Options] Sync UI → SFX={muteSfx}, Music={muteMusic}", inst);
        }

        // ===== Toggle handlers (from either panel) =====
        void OnToggleMuteSfx(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] mute SFX = {isMuted}", this);
            PlayerPrefs.SetInt(K_MUTE_SFX, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            AudioManager.I?.SetMuteSFX(isMuted);
            // Event will call HandleMuteSfxChanged → mirror other panel automatically.
        }

        void OnToggleMuteMusic(bool isMuted)
        {
            if (verboseLogs) Debug.Log($"[Options] mute Music = {isMuted}", this);
            PlayerPrefs.SetInt(K_MUTE_MUSIC, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            AudioManager.I?.SetMuteMusic(isMuted);
            // Event will call HandleMuteMusicChanged → mirror other panel automatically.
        }

        // ===== Event mirroring from AudioManager =====
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

        // ===== Utility =====
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
            // If your toggle uses a custom "On/Off" image, ensure the animator or
            // sprite swap is driven by Toggle.isOn so setting it updates the look.
        }

        // ===== Links (optional) =====
        public void OpenTerms() { if (!string.IsNullOrEmpty(termsOfServiceUrl)) Application.OpenURL(termsOfServiceUrl); }
        public void OpenPrivacy() { if (!string.IsNullOrEmpty(privacyPolicyUrl)) Application.OpenURL(privacyPolicyUrl); }

        // Allow MenuFlowController to close panels as before
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
