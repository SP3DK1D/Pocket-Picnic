using UnityEngine;
using Unity.Services.LevelPlay;

namespace CatchTheFruit
{
    /// <summary>
    /// Loads/Shows LevelPlay Rewarded/Interstitial/Banner after LevelPlay init succeeds.
    /// Put this on a persistent GameObject in your first scene.
    /// </summary>
    public class LevelPlayAdsManager : MonoBehaviour
    {
        public static LevelPlayAdsManager I { get; private set; }

        [Header("Ad Unit IDs (exact names from LevelPlay dashboard)")]
        [SerializeField] private string rewardedUnitId = "REWARDED_AD_UNIT_ID";
        [SerializeField] private string interstitialUnitId = "INTERSTITIAL_AD_UNIT_ID";
        [SerializeField] private string bannerUnitId = "BANNER_AD_UNIT_ID";
        [SerializeField] private LevelPlayBannerPosition bannerPosition = LevelPlayBannerPosition.BottomCenter;

        private LevelPlayRewardedAd _rewarded;
        private LevelPlayInterstitialAd _interstitial;
        private LevelPlayBannerAd _banner;

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);

            // Match the new event signature: Action<LevelPlayConfiguration>
            LevelPlay.OnInitSuccess += OnLevelPlayReady;
            LevelPlay.OnInitFailed += err => Debug.LogWarning($"[LevelPlay] Init failed: {err}");
        }

        void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnLevelPlayReady;
            LevelPlay.OnInitFailed -= err => Debug.LogWarning($"[LevelPlay] Init failed: {err}");

            _banner?.DestroyAd();
            _interstitial?.DestroyAd();
            _rewarded?.Dispose();
        }

        // Called when LevelPlay.Init completes; cfg includes mediation info if you need it
        private void OnLevelPlayReady(LevelPlayConfiguration cfg)
        {
            // ---- Rewarded ----
            if (!string.IsNullOrEmpty(rewardedUnitId))
            {
                _rewarded = new LevelPlayRewardedAd(rewardedUnitId);
                _rewarded.OnAdLoaded += info => Debug.Log("[LP] Rewarded loaded");
                _rewarded.OnAdLoadFailed += err => Debug.LogWarning($"[LP] Rewarded load fail: {err}");
                _rewarded.OnAdDisplayed += info => Debug.Log("[LP] Rewarded shown");
                _rewarded.OnAdDisplayFailed += err => Debug.LogWarning($"[LP] Rewarded show fail: {err}");
                _rewarded.OnAdRewarded += info => {
                    Debug.Log("[LP] Reward earned");
                    // TODO: grant the reward in your game here
                };
                _rewarded.OnAdClosed += info => { /* optional */ };
                _rewarded.LoadAd();
            }

            // ---- Interstitial ----
            if (!string.IsNullOrEmpty(interstitialUnitId))
            {
                _interstitial = new LevelPlayInterstitialAd(interstitialUnitId);
                _interstitial.OnAdLoaded += info => Debug.Log("[LP] Interstitial loaded");
                _interstitial.OnAdLoadFailed += err => Debug.LogWarning($"[LP] Interstitial load fail: {err}");
                _interstitial.OnAdDisplayed += info => Debug.Log("[LP] Interstitial shown");
                _interstitial.OnAdDisplayFailed += err => Debug.LogWarning($"[LP] Interstitial show fail: {err}");
                _interstitial.OnAdClosed += info => { /* optional */ };
                _interstitial.LoadAd();
            }

            // ---- Banner ----
            if (!string.IsNullOrEmpty(bannerUnitId))
            {
                _banner = new LevelPlayBannerAd(bannerUnitId, LevelPlayAdSize.BANNER, bannerPosition);
                _banner.OnAdLoaded += info => { Debug.Log("[LP] Banner loaded"); _banner.ShowAd(); };
                _banner.OnAdLoadFailed += err => Debug.LogWarning($"[LP] Banner load fail: {err}");
                _banner.OnAdClicked += info => { /* optional */ };
                _banner.LoadAd();
            }
        }

        // ======= Public helpers =======
        public bool ShowRewarded()
        {
            if (_rewarded != null && _rewarded.IsAdReady())
            {
                _rewarded.ShowAd();
                return true;
            }
            _rewarded?.LoadAd();
            return false;
        }

        public bool ShowInterstitial()
        {
            if (_interstitial != null && _interstitial.IsAdReady())
            {
                _interstitial.ShowAd();
                _interstitial.LoadAd(); // warm next
                return true;
            }
            _interstitial?.LoadAd();
            return false;
        }

        public void ShowBanner()
        {
            if (_banner == null && !string.IsNullOrEmpty(bannerUnitId))
            {
                _banner = new LevelPlayBannerAd(bannerUnitId, LevelPlayAdSize.BANNER, bannerPosition);
                _banner.OnAdLoaded += info => { Debug.Log("[LP] Banner loaded"); _banner.ShowAd(); };
                _banner.OnAdLoadFailed += err => Debug.LogWarning($"[LP] Banner load fail: {err}");
                _banner.LoadAd();
            }
            else
            {
                // If already created/loaded previously
                _banner?.ShowAd();
            }
        }

        public void HideBanner() => _banner?.DestroyAd();
    }
}
