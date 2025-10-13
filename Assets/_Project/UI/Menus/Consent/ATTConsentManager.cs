// Assets/_Project/Scripts/Consent/ATTConsentManager.cs
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace CatchTheFruit
{
    /// <summary>
    /// iOS App Tracking Transparency (ATT) helper with **no hard dependency** on the
    /// "Unity iOS Support" package. Uses reflection if available, otherwise no-ops.
    ///
    /// - Requests ATT once on first launch (iOS 14+), then caches that it asked.
    /// - Stores an app-level preference "AllowTracking" in PlayerPrefs for your SDK gating.
    /// - Optional Toggle to let the user flip AllowTracking later (independent of system ATT).
    /// - Editor simulation flag to make UI testable without an iOS device.
    ///
    /// Notes:
    /// • System ATT decision (Allow/Deny) is controlled by iOS. This script only:
    ///   (1) asks once on first run, and (2) manages your own app preference.
    /// • If you rely on ad SDKs, gate their initialization behind AllowTracking (and/or the
    ///   system status if you integrate that elsewhere).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ATTConsentManager : MonoBehaviour
    {
        // -------------------- Inspector --------------------
        [Header("Optional UI")]
        [Tooltip("If assigned, this Toggle mirrors the app-level AllowTracking preference.")]
        [SerializeField] private Toggle allowTrackingToggle;

        [Tooltip("If your toggle art is visually inverted, flip mapping UI<->logical here.")]
        [SerializeField] private bool invertUiMapping = false;

#if UNITY_EDITOR
        [Header("Editor Simulation")]
        [Tooltip("Simulate 'asked once' and default AllowTracking in Editor.")]
        [SerializeField] private bool simulateInEditor = true;
        [Tooltip("Editor default for AllowTracking when simulation is on.")]
        [SerializeField] private bool editorDefaultAllowTracking = true;
#endif

        [Header("Logs")]
        [SerializeField] private bool verboseLogs = true;

        // -------------------- PlayerPrefs keys --------------------
        private const string KAskedOnce = "att_asked_once";
        private const string KAllow = "att_allow_tracking";

        /// <summary>
        /// Your **app-level** preference. Use this to gate SDK init or ad personalization.
        /// Independent of the OS decision (you can still store a preference even if ATT denied).
        /// Default ON so you don’t accidentally disable analytics/ads on first boot.
        /// </summary>
        public static bool AllowTracking
        {
            get => PlayerPrefs.GetInt(KAllow, 1) == 1;
            set { PlayerPrefs.SetInt(KAllow, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // -------------------- Unity lifecycle --------------------
        private void Awake()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Only request on a real iOS device, once.
            if (GetInt(KAskedOnce, 0) == 0)
                StartCoroutine(CoRequestATTOnce());
#endif
        }

        private void OnEnable()
        {
            // Keep the toggle in sync with our app pref (respect inversion).
            if (allowTrackingToggle)
            {
                bool uiOn = invertUiMapping ? !AllowTracking : AllowTracking;
                allowTrackingToggle.SetIsOnWithoutNotify(uiOn);
                allowTrackingToggle.onValueChanged.AddListener(OnAllowTrackingChanged);
            }

#if UNITY_EDITOR
            if (simulateInEditor)
            {
                // Seed first-run prefs to something sane for testing UI flows.
                if (!HasKey(KAskedOnce)) SetInt(KAskedOnce, 1);
                if (!HasKey(KAllow)) SetInt(KAllow, editorDefaultAllowTracking ? 1 : 0);

                if (allowTrackingToggle)
                {
                    bool uiOn = invertUiMapping ? !AllowTracking : AllowTracking;
                    allowTrackingToggle.SetIsOnWithoutNotify(uiOn);
                }

                if (verboseLogs) Debug.Log("[ATT] Editor simulation active (no real iOS prompt).");
            }
#endif
        }

        private void OnDisable()
        {
            if (allowTrackingToggle)
                allowTrackingToggle.onValueChanged.RemoveListener(OnAllowTrackingChanged);
        }

        // -------------------- UI hooks --------------------
        /// <summary>Wire this to the Toggle's OnValueChanged(bool).</summary>
        public void OnAllowTrackingChanged(bool uiOn)
        {
            bool logical = invertUiMapping ? !uiOn : uiOn;
            AllowTracking = logical;

            if (verboseLogs)
                Debug.Log($"[ATT] Toggle UI: {(uiOn ? "ON" : "OFF")} -> AllowTracking: {(logical ? "TRUE" : "FALSE")}");
        }

        /// <summary>Optional button to open iOS Settings so the user can change system ATT.</summary>
        public void OpenIOSSettings()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Application.OpenURL("app-settings:");
#endif
        }

        // -------------------- iOS ATT (reflection, no hard dep) --------------------
#if UNITY_IOS && !UNITY_EDITOR
        // ATT status enum mirrors Unity.Advertisement.IosSupport.ATTrackingStatusBinding values.
        private enum ATTStatus { NotDetermined = 0, Restricted = 1, Denied = 2, Authorized = 3, Unknown = -1 }

        // Type resolved by reflection so the app runs even if the package is not present.
        private static System.Type AttType =>
            System.Type.GetType("Unity.Advertisement.IosSupport.ATTrackingStatusBinding, Unity.Advertisement.IosSupport");

        private static ATTStatus GetStatus()
        {
            try
            {
                var t = AttType; if (t == null) return ATTStatus.Unknown;
                var m = t.GetMethod("GetAuthorizationTrackingStatus", BindingFlags.Public | BindingFlags.Static);
                if (m == null) return ATTStatus.Unknown;
                return (ATTStatus)(int)m.Invoke(null, null);
            }
            catch { return ATTStatus.Unknown; }
        }

        private static void RequestATT()
        {
            try
            {
                var t = AttType; if (t == null) return;
                var m = t.GetMethod("RequestAuthorizationTracking", BindingFlags.Public | BindingFlags.Static);
                m?.Invoke(null, null);
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Requests ATT once. We also set your app-level AllowTracking default based on the
        /// system’s result on first run (Authorized => true, otherwise leave existing value).
        /// </summary>
        private IEnumerator CoRequestATTOnce()
        {
            var status = GetStatus();

            // Only ask if the system hasn't decided yet (or we can't tell).
            if (status == ATTStatus.NotDetermined || status == ATTStatus.Unknown)
            {
                if (verboseLogs) Debug.Log("[ATT] Requesting ATT authorization…");
                RequestATT();

                // Poll for the result with a short timeout to avoid hanging.
                float t = 0f, timeout = 6f;
                while (t < timeout)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    t += 0.25f;
                    status = GetStatus();
                    if (status != ATTStatus.NotDetermined) break;
                }
            }

            SetInt(KAskedOnce, 1);

            // If the app has no opinion yet, pick the system result on first-run.
            if (!HasKey(KAllow) && status == ATTStatus.Authorized)
                AllowTracking = true;

            if (verboseLogs)
                Debug.Log($"[ATT] First-run complete. System status: {status} | AllowTracking={AllowTracking}");

            // Re-sync UI and exit.
            if (allowTrackingToggle)
            {
                bool uiOn = invertUiMapping ? !AllowTracking : AllowTracking;
                allowTrackingToggle.SetIsOnWithoutNotify(uiOn);
            }
        }
#endif

        // -------------------- tiny PlayerPrefs helpers (no GC) --------------------
        static bool HasKey(string k) => PlayerPrefs.HasKey(k);
        static int GetInt(string k, int d) => PlayerPrefs.GetInt(k, d);
        static void SetInt(string k, int v) { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); }
    }
}
