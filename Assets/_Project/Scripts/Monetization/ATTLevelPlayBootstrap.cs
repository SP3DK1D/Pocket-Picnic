using UnityEngine;
using Unity.Services.LevelPlay;                 // LevelPlay SDK

#if UNITY_IOS
using Unity.Advertisement.IosSupport;          // ATT dialog (iOS support pkg)
#endif

namespace CatchTheFruit
{
    /// <summary>
    /// Shows ATT once on first launch (iOS), stores consent defaults, then initializes LevelPlay.
    /// Attach to a boot object in your first scene. Stays alive across scenes.
    /// </summary>
    public class ATTLevelPlayBootstrap : MonoBehaviour
    {
        [Header("LevelPlay")]
        [Tooltip("Copy from LevelPlay dashboard → App → App Key")]
        [SerializeField] private string levelPlayAppKey = "YOUR_LEVELPLAY_APP_KEY";

        [Header("Privacy Defaults (first run only)")]
        [Tooltip("If you don't collect explicit GDPR consent yet, choose a default.")]
        [SerializeField] private bool defaultPersonalizedAds = false;

        const string K_FirstRun = "pp_first_run_done";
        const string K_Consent = "pp_consent_personalized";   // 1/0

        void Awake() => DontDestroyOnLoad(gameObject);

        void Start()
        {
            // First run: ask ATT (iOS) then init; later runs: init immediately
            if (PlayerPrefs.GetInt(K_FirstRun, 0) == 0)
            {
                PlayerPrefs.SetInt(K_Consent, defaultPersonalizedAds ? 1 : 0);
                PlayerPrefs.Save();

#if UNITY_IOS
                // ATT: parameterless request in the new support package
                ATTrackingStatusBinding.RequestAuthorizationTracking();
                // Give iOS a tiny moment; then initialize
                Invoke(nameof(InitFromSaved), 0.5f);
#else
                InitFromSaved();
#endif
                PlayerPrefs.SetInt(K_FirstRun, 1);
                PlayerPrefs.Save();
            }
            else
            {
                InitFromSaved();
            }
        }

        void InitFromSaved()
        {
            bool consent = PlayerPrefs.GetInt(K_Consent, 0) == 1;

            // New LevelPlay consent call (apply BEFORE Init)
            LevelPlay.SetConsent(consent);

            Debug.Log($"[LevelPlay] Init (consent={consent})");
            LevelPlay.Init(levelPlayAppKey);
        }

        // Expose a simple hook for your Options menu
        public void SetGdprConsent(bool on)
        {
            PlayerPrefs.SetInt(K_Consent, on ? 1 : 0);
            PlayerPrefs.Save();
            LevelPlay.SetConsent(on); // can be updated at runtime
        }
    }
}
