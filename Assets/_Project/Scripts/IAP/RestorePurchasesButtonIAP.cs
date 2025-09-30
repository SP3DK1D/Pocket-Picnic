// Assets/_Project/Scripts/Utility/RestorePurchasesButtonIAP.cs
using UnityEngine;
using UnityEngine.UI;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;            // IAP core
using UnityEngine.Purchasing.Extension;  // IStoreExtension, etc.
#endif

namespace CatchTheFruit
{
    [RequireComponent(typeof(Button))]
    public class RestorePurchasesButtonIAP : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Restore);
        }

        void Restore()
        {
#if UNITY_IOS && UNITY_PURCHASING
            // Ensure Codeless IAP has initialized the store (you must have at least one IAPButton in the scene
            // or initialize Codeless on load). Otherwise this will be null.
            var listener = CodelessIAPStoreListener.Instance;
            if (listener == null)
            {
                Debug.LogWarning("[IAP] CodelessIAPStoreListener not initialized. Make sure an IAPButton exists or init Codeless at startup.");
                return;
            }

            // Get the Apple-specific extension via the public helper (works across IAP versions)
            var apple = listener.GetStoreExtensions<IAppleExtensions>();
            if (apple == null)
            {
                Debug.LogWarning("[IAP] IAppleExtensions not available. Is the Apple store active and IAP initialized?");
                return;
            }

            Debug.Log("[IAP] Restoring transactions…");
            apple.RestoreTransactions(success =>
            {
                Debug.Log("[IAP] RestoreTransactions result: " + success);
            });
#else
            Debug.LogWarning("[IAP] Restore is iOS-only and requires Unity IAP. Install/enable In-App Purchasing to use this button.");
#endif
        }
    }
}
