using UnityEngine;

namespace CatchTheFruit
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;   // looped BGM
        [SerializeField] private AudioSource sfxSource;     // one-shots

        [Header("SFX Clips")]
        public AudioClip sfxCatch;
        public AudioClip sfxBomb;

        [Space(6)]
        public AudioClip sfxFreezeStart;
        public AudioClip sfxScoreStart;
        public AudioClip sfxMagnetStart;
        public AudioClip sfxShieldOn;
        public AudioClip sfxShieldBreak;
        public AudioClip sfxClearBurst;

        [Space(6)]
        public AudioClip sfxUIButton;

        [Header("Saved Volumes (0..1)")]
        [Range(0, 1)] public float master = 1f;
        [Range(0, 1)] public float music = 0.8f;
        [Range(0, 1)] public float sfx = 1f;

        const string K_MASTER = "vol_master";
        const string K_MUSIC  = "vol_music";
        const string K_SFX    = "vol_sfx";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            master = PlayerPrefs.GetFloat(K_MASTER, master);
            music  = PlayerPrefs.GetFloat(K_MUSIC,  music);
            sfx    = PlayerPrefs.GetFloat(K_SFX,    sfx);
            ApplyVolumes();
        }

        void OnEnable()
        {
            GameEvents.OnFruitCaught     += OnFruitCaught;
            GameEvents.OnPowerupStarted  += OnPowerupStarted;
            GameEvents.OnGameStart       += OnGameStart;
            GameEvents.OnGameOver        += OnGameOver;
        }
        void OnDisable()
        {
            GameEvents.OnFruitCaught     -= OnFruitCaught;
            GameEvents.OnPowerupStarted  -= OnPowerupStarted;
            GameEvents.OnGameStart       -= OnGameStart;
            GameEvents.OnGameOver        -= OnGameOver;
        }

        // ------- Public helpers -------
        public void PlaySFX(AudioClip clip, float vol = 1f)
        {
            if (!clip || !sfxSource) return;
            sfxSource.PlayOneShot(clip, vol * master * sfx);
        }
        public void PlayUIButtonClick() => PlaySFX(sfxUIButton, 0.9f);

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (!musicSource) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = master * music;
            musicSource.Play();
        }
        public void StopMusic() { if (musicSource) musicSource.Stop(); }

        public void SetMaster(float v) { master = Mathf.Clamp01(v); Save(); ApplyVolumes(); }
        public void SetMusic (float v) { music  = Mathf.Clamp01(v); Save(); ApplyVolumes(); }
        public void SetSFX   (float v) { sfx    = Mathf.Clamp01(v); Save(); ApplyVolumes(); }

        void Save()
        {
            PlayerPrefs.SetFloat(K_MASTER, master);
            PlayerPrefs.SetFloat(K_MUSIC,  music);
            PlayerPrefs.SetFloat(K_SFX,    sfx);
        }
        void ApplyVolumes()
        {
            if (musicSource) musicSource.volume = master * music;
            // sfxSource uses PlayOneShot mixing, volume applied per call
        }

        // ------- Event handlers -------
        void OnFruitCaught(string id, int score, bool isBomb)
        {
            PlaySFX(isBomb ? sfxBomb : sfxCatch);
        }

        // Called by PowerupManager at Start events
        public void PlayPowerupStart(PowerupDef.PowerupKind kind)
        {
            switch (kind)
            {
                case PowerupDef.PowerupKind.TimeScale:       PlaySFX(sfxFreezeStart); break;
                case PowerupDef.PowerupKind.ScoreMultiplier: PlaySFX(sfxScoreStart);  break;
                case PowerupDef.PowerupKind.Magnet:          PlaySFX(sfxMagnetStart); break;
                case PowerupDef.PowerupKind.Shield:          PlaySFX(sfxShieldOn);    break;
                case PowerupDef.PowerupKind.ClearScreen:     PlaySFX(sfxClearBurst);  break;
            }
        }

        // Called by PowerupManager when shield is consumed by a hit
        public void PlayShieldBreak() => PlaySFX(sfxShieldBreak);

        void OnPowerupStarted(PowerupDef def) { /* handled via PlayPowerupStart(kind) */ }
        void OnGameStart() { /* optional music start */ }
        void OnGameOver()  { /* optional music stop */ }
    }
}
