// Assets/_Project/Scripts/Systems/AudioManager.cs
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
        public AudioClip sfxPowerupStart;
        public AudioClip sfxShieldHit;
        public AudioClip sfxClearBurst;
        public AudioClip sfxUIButton;

        [Header("Saved Volumes (0..1)")]
        [Range(0, 1)] public float master = 1f;
        [Range(0, 1)] public float music = 0.8f;
        [Range(0, 1)] public float sfx = 1f;

        const string K_MASTER = "vol_master";
        const string K_MUSIC = "vol_music";
        const string K_SFX = "vol_sfx";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // load prefs
            master = PlayerPrefs.GetFloat(K_MASTER, master);
            music = PlayerPrefs.GetFloat(K_MUSIC, music);
            sfx = PlayerPrefs.GetFloat(K_SFX, sfx);
            ApplyVolumes();
        }

        void OnEnable()
        {
            GameEvents.OnFruitCaught += OnFruitCaught;
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnPowerupEnded += OnPowerupEnded; // reserved if you want end SFX later
            GameEvents.OnGameStart += OnGameStart;    // optional: start BGM
            GameEvents.OnGameOver += OnGameOver;     // optional: stop/transition BGM
        }
        void OnDisable()
        {
            GameEvents.OnFruitCaught -= OnFruitCaught;
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnPowerupEnded -= OnPowerupEnded;
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameOver -= OnGameOver;
        }

        // ------------ Public API ------------
        public void PlaySFX(AudioClip clip, float vol = 1f)
        {
            if (!clip || !sfxSource) return;
            sfxSource.PlayOneShot(clip, vol * master * sfx);
        }

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

        // UI hooks for sliders
        public void SetMaster(float v) { master = Mathf.Clamp01(v); Save(); ApplyVolumes(); }
        public void SetMusic(float v) { music = Mathf.Clamp01(v); Save(); ApplyVolumes(); }
        public void SetSFX(float v) { sfx = Mathf.Clamp01(v); Save(); ApplyVolumes(); }

        void Save()
        {
            PlayerPrefs.SetFloat(K_MASTER, master);
            PlayerPrefs.SetFloat(K_MUSIC, music);
            PlayerPrefs.SetFloat(K_SFX, sfx);
        }

        void ApplyVolumes()
        {
            if (musicSource) musicSource.volume = master * music;
            // sfxSource volume not used for PlayOneShot mix; we apply per call
        }

        // ------------ Event handlers ------------
        void OnFruitCaught(string id, int score, bool isBomb)
        {
            PlaySFX(isBomb ? sfxBomb : sfxCatch);
        }

        void OnPowerupStarted(PowerupDef def)
        {
            if (def == null) return;
            // Play distinct SFX if you want by def.kind, else generic:
            PlaySFX(sfxPowerupStart);
        }

        void OnPowerupEnded(PowerupDef def) { /* optional end sound */ }

        void OnGameStart() { /* optionally start music here */ }
        void OnGameOver() { /* optionally stop/transition music here */ }

        // Convenience for UI buttons
        public void PlayUIButtonClick() => PlaySFX(sfxUIButton, 0.9f);
    }
}
