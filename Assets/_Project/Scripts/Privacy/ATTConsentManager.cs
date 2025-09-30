using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheFruit
{
    /// <summary>
    /// - iOS: Requests ATT once on first launch (iOS 14+).
    /// - Options: Toggle for Allow Tracking (+ optional ON/OFF sprite swap).
    /// - Debug: logs state on every change.
    /// - Fix: 'invertUiMapping' lets you flip UI <-> value if your art is reversed.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ATTConsentManager : MonoBehaviour
    {
        [Header("Options UI (assign if you use a Toggle)")]
        [SerializeField] private Toggle allowTrackingToggle;

        [Tooltip("If your UI looks ON when you actually want AllowTracking=FALSE (or vice versa), enable this.")]
        [SerializeField] private bool invertUiMapping = false;

        [Header("Optional Sprite Swap (same toggle)")]
        [SerializeField] private Image toggleSpriteTarget;   // e.g., the button face image
        [SerializeField] private Sprite spriteOn;            // shown when UI is ON
        [SerializeField] private Sprite spriteOff;           // shown when UI is OFF

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

#if UNITY_EDITOR
        [Header("Editor Test")]
        [SerializeField] private bool simulateInEditor = true;
#endif

        private const string KAskedOnce = "att_asked_once";
        private const string KAllow = "att_allow_tracking";

        /// <summary>Your app-level preference. Use this to gate SDK init.</summary>
        public static bool AllowTracking
        {
            get => PlayerPrefs.GetInt(KAllow, 1) == 1;   // default ON
            set { PlayerPrefs.SetInt(KAllow, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (PlayerPrefs.GetInt(KAskedOnce, 0) == 0)
                StartCoroutine(CoRequestATTOnce());
#endif
        }

        private void OnEnable()
        {
            // Sync UI from saved value (respect invert)
            if (allowTrackingToggle)
            {
                bool uiOn = invertUiMapping ? !AllowTracking : AllowTracking;
                allowTrackingToggle.SetIsOnWithoutNotify(uiOn);
                allowTrackingToggle.onValueChanged.AddListener(OnAllowTrackingChanged);
            }
            UpdateToggleSprite(allowTrackingToggle ? allowTrackingToggle.isOn : (invertUiMapping ? !AllowTracking : AllowTracking));
        }

        private void OnDisable()
        {
            if (allowTrackingToggle)
                allowTrackingToggle.onValueChanged.RemoveListener(OnAllowTrackingChanged);
        }

#if UNITY_EDITOR
        private void Start()
        {
            if (!simulateInEditor) return;

            if (!PlayerPrefs.HasKey(KAskedOnce)) PlayerPrefs.SetInt(KAskedOnce, 1);
            if (!PlayerPrefs.HasKey(KAllow)) PlayerPrefs.SetInt(KAllow, 1);
            PlayerPrefs.Save();

            if (allowTrackingToggle)
            {
                bool uiOn = invertUiMapping ? !AllowTracking : AllowTracking;
                allowTrackingToggle.SetIsOnWithoutNotify(uiOn);
            }
            UpdateToggleSprite(allowTrackingToggle ? allowTrackingToggle.isOn : (invertUiMapping ? !AllowTracking : AllowTracking));

            if (verboseLogs) Debug.Log("[ATT] Editor simulation active. No real iOS prompt will appear.");
        }
#endif

        // ================= UI hooks =================

        /// <summary>Wire this to the Toggle's OnValueChanged(bool).</summary>
        public void OnAllowTrackingChanged(bool uiOn)
        {
            // Map UI state to logical value (invert if needed)
            bool logical = invertUiMapping ? !uiOn : uiOn;
            AllowTracking = logical;

            UpdateToggleSprite(uiOn);

            if (verboseLogs)
                Debug.Log($"[ATT] Toggle UI: {(uiOn ? "ON" : "OFF")}  ->  AllowTracking (logical): {(logical ? "TRUE" : "FALSE")}");
        }

        /// <summary>Optional: wire to a Button to open iOS Settings.</summary>
        public void OpenIOSSettings()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Application.OpenURL("app-settings:");
#endif
        }

        // ================= Visual helpers =================
        private void UpdateToggleSprite(bool uiOn)
        {
            if (!toggleSpriteTarget) return;
            if (uiOn && spriteOn) toggleSpriteTarget.sprite = spriteOn;
            else if (!uiOn && spriteOff) toggleSpriteTarget.sprite = spriteOff;
        }

        // ================= iOS ATT (reflection, no hard dep) =================
#if UNITY_IOS && !UNITY_EDITOR
        private enum ATTStatus { NotDetermined = 0, Restricted = 1, Denied = 2, Authorized = 3, Unknown = -1 }
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

        private IEnumerator CoRequestATTOnce()
        {
            var status = GetStatus();

            if (status == ATTStatus.NotDetermined || status == ATTStatus.Unknown)
            {
                if (verboseLogs) Debug.Log("[ATT] Requesting ATT...");
                RequestATT();

                // Poll until user responds or we time out
                float t = 0f, timeout = 6f;
                while (t < timeout)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    t += 0.25f;
                    status = GetStatus();
                    if (status != ATTStatus.NotDetermined) break;
                }
            }

            PlayerPrefs.SetInt(KAskedOnce, 1);

            if (!PlayerPrefs.HasKey(KAllow))
                AllowTracking = (status == ATTStatus.Authorized);

            PlayerPrefs.Save();

            // Re-sync UI and sprite after first-run
            if (allowTrackingToggle)
            {
                bool uiOn = invertUiMapping ? !AllowTracking : AllowTracking;
                allowTrackingToggle.SetIsOnWithoutNotify(uiOn);
                UpdateToggleSprite(uiOn);
            }

            if (verboseLogs)
                Debug.Log($"[ATT] First-run complete. System status: {status} | AllowTracking: {AllowTracking}");
        }
#endif
    }
}
