using UnityEngine;
using System;
using System.Collections;

namespace CatchTheFruit
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public static AudioManager I => Instance;

        [Header("Auto-create sources if not assigned")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Default Music (optional)")]
        [SerializeField] private AudioClip defaultMusic;

        [Header("SFX Clips")]
        public AudioClip sfxUIButton;
        public AudioClip sfxCatch;
        public AudioClip sfxBomb;
        public AudioClip sfxFreezeStart;
        public AudioClip sfxScoreStart;
        public AudioClip sfxMagnetStart;
        public AudioClip sfxShieldOn;
        public AudioClip sfxShieldBreak;
        public AudioClip sfxClearBurst;

        [Header("Volumes (0..1)")]
        [Range(0, 1)] public float master = 1f;
        [Range(0, 1)] public float music = 0.05f;
        [Range(0, 1)] public float sfx = 0.45f;

        [Header("Mute")]
        public bool muteMusic = false;
        public bool muteSfx = false;

        [Header("Fade Settings")]
        public float fadeDuration = 0.5f;

        [Header("Prefs Keys")]
        [SerializeField] private string kMaster = "vol_master";
        [SerializeField] private string kMusic = "vol_music";
        [SerializeField] private string kSfx = "vol_sfx";
        [SerializeField] private string kMuteM = "mute_music";
        [SerializeField] private string kMuteS = "mute_sfx";

        public event Action<bool> OnMuteMusicChanged;
        public event Action<bool> OnMuteSfxChanged;
        public event Action<float, float, float> OnVolumesChanged;

        // ✅ Added for OptionManager compatibility
        public bool IsMusicMuted => muteMusic;
        public bool IsSfxMuted => muteSfx;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioListener();
            EnsureSources();

            // Load saved prefs
            master = PlayerPrefs.GetFloat(kMaster, master);
            music = PlayerPrefs.GetFloat(kMusic, music);
            sfx = PlayerPrefs.GetFloat(kSfx, sfx);
            muteMusic = PlayerPrefs.GetInt(kMuteM, muteMusic ? 1 : 0) != 0;
            muteSfx = PlayerPrefs.GetInt(kMuteS, muteSfx ? 1 : 0) != 0;

            ApplyState();

            if (defaultMusic && !musicSource.isPlaying)
                PlayMusic(defaultMusic, true);
        }

        void OnEnable()
        {
            GameEvents.OnFruitCaught += OnFruitCaught;
            GameEvents.OnPowerupStarted += OnPowerupStarted;
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGameOver += OnGameOver;
        }
        void OnDisable()
        {
            GameEvents.OnFruitCaught -= OnFruitCaught;
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameOver -= OnGameOver;
            if (Instance == this) Instance = null;
        }

        public void PlaySFX(AudioClip clip, float volMul = 1f, float pitch = 1f)
        {
            if (!clip || !sfxSource) return;
            if (muteSfx || master <= 0f || sfx <= 0f) return;

            float final = Mathf.Clamp01(master * sfx * volMul);
            float oldPitch = sfxSource.pitch;
            sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            sfxSource.PlayOneShot(clip, final);
            sfxSource.pitch = oldPitch;
        }

        public void PlayUIButton() => PlaySFX(sfxUIButton, 0.9f);
        public void PlayUIButtonClick() => PlayUIButton();
        public void PlayShieldBreak() => PlaySFX(sfxShieldBreak);

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (!clip || !musicSource) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            ApplyState();
            if (!muteMusic) musicSource.Play();
        }

        public void StopMusic() { if (musicSource) musicSource.Stop(); }

        IEnumerator FadeMusic(float target)
        {
            if (!musicSource) yield break;
            float start = musicSource.volume;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }
            musicSource.volume = target;
        }

        void OnGameStart() { StartCoroutine(FadeMusic(master * music)); }
        void OnGameOver() { StartCoroutine(FadeMusic(0f)); }

        public void SetMaster(float v) { master = Mathf.Clamp01(v); Save(); ApplyState(); }
        public void SetMusicVolume(float v) { music = Mathf.Clamp01(v); Save(); ApplyState(); }
        public void SetSfxVolume(float v) { sfx = Mathf.Clamp01(v); Save(); ApplyState(); }

        public void SetMuteMusic(bool m)
        {
            muteMusic = m;
            PlayerPrefs.SetInt(kMuteM, m ? 1 : 0);
            Save(); ApplyState();
            OnMuteMusicChanged?.Invoke(muteMusic);
        }

        public void SetMuteSFX(bool m)
        {
            muteSfx = m;
            PlayerPrefs.SetInt(kMuteS, m ? 1 : 0);
            Save(); ApplyState();
            OnMuteSfxChanged?.Invoke(muteSfx);
        }

        void ApplyState()
        {
            if (musicSource)
            {
                musicSource.mute = muteMusic || master <= 0f || music <= 0f;
                musicSource.volume = (muteMusic ? 0f : 1f) * master * music;
            }
            if (sfxSource)
                sfxSource.mute = muteSfx || master <= 0f || sfx <= 0f;
        }

        void Save()
        {
            PlayerPrefs.SetFloat(kMaster, master);
            PlayerPrefs.SetFloat(kMusic, music);
            PlayerPrefs.SetFloat(kSfx, sfx);
            PlayerPrefs.Save();
        }

        void EnsureAudioListener()
        {
            if (!FindObjectOfType<AudioListener>())
            {
                var cam = Camera.main ? Camera.main.gameObject : new GameObject("AudioListener_Auto");
                if (!cam.TryGetComponent<AudioListener>(out _)) cam.AddComponent<AudioListener>();
            }
        }

        void EnsureSources()
        {
            if (!sfxSource)
            {
                var go = new GameObject("SFX_AudioSource");
                go.transform.SetParent(transform, false);
                sfxSource = go.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
            }

            if (!musicSource)
            {
                var go = new GameObject("Music_AudioSource");
                go.transform.SetParent(transform, false);
                musicSource = go.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f;
            }
        }

        void OnFruitCaught(string id, int score, bool isBomb)
        {
            if (isBomb) PlaySFX(sfxBomb);
            else PlaySFX(sfxCatch);
        }

        void OnPowerupStarted(PowerupDef def)
        {
            if (!def) return;
            PlayPowerupStart(def);
        }

        public void PlayPowerupStart(PowerupDef def)
        {
            if (!def) return;
            PlayPowerupStart(def.kind);
        }

        public void PlayPowerupStart(PowerupDef.PowerupKind kind)
        {
            switch (kind)
            {
                case PowerupDef.PowerupKind.TimeScale: PlaySFX(sfxFreezeStart); break;
                case PowerupDef.PowerupKind.ScoreMultiplier: PlaySFX(sfxScoreStart); break;
                case PowerupDef.PowerupKind.Magnet: PlaySFX(sfxMagnetStart); break;
                case PowerupDef.PowerupKind.Shield: PlaySFX(sfxShieldOn); break;
                case PowerupDef.PowerupKind.ClearScreen: PlaySFX(sfxClearBurst); break;
            }
        }
    }
}
