using UnityEngine;
using System;

namespace CatchTheFruit
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public static AudioManager I => Instance;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource; // BGM (2D)
        [SerializeField] private AudioSource sfxSource;   // One-shots (2D)

        [Header("General SFX")]
        public AudioClip sfxCatch;
        public AudioClip sfxBomb;

        [Header("Powerup SFX (Start)")]
        public AudioClip sfxFreezeStart;
        public AudioClip sfxScoreStart;
        public AudioClip sfxMagnetStart;
        public AudioClip sfxShieldOn;
        public AudioClip sfxClearBurst;

        [Header("Special SFX")]
        public AudioClip sfxShieldBreak;
        public AudioClip sfxUIButton;

        [Header("Volumes (0..1)")]
        [Range(0, 1)] public float master = 1f;
        [Range(0, 1)] public float music = 0.8f;
        [Range(0, 1)] public float sfx = 1f;

        [Header("Mute state")]
        public bool muteMusic = false;
        public bool muteSfx = false;

        [Header("Defaults when unmuting via toggles")]
        [Range(0, 1)] public float defaultMusicVol = 0.05f; // matches your OptionManager
        [Range(0, 1)] public float defaultSfxVol = 0.45f;

        [Header("Safety")]
        [Tooltip("If no SFX AudioSource is set, use PlayClipAtPoint at Camera.main.")]
        public bool fallbackToAtPoint = true;

        [Header("Debug")]
        public bool debugLogs = false;

        // UI sync events
        public event Action<bool> OnMuteMusicChanged;
        public event Action<bool> OnMuteSfxChanged;
        public event Action<float, float, float> OnVolumesChanged; // (master,music,sfx)

        // PlayerPrefs keys
        const string K_MASTER = "vol_master";
        const string K_MUSIC = "vol_music";
        const string K_SFX = "vol_sfx";
        const string K_MUTE_M = "mute_music";
        const string K_MUTE_S = "mute_sfx";

        // OptionManager legacy keys (your current Options script writes these)
        const string K_OPT_MUTE_SFX = "opt_mute_sfx";
        const string K_OPT_MUTE_MUSIC = "opt_mute_music";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load volumes + mutes
            master = PlayerPrefs.GetFloat(K_MASTER, master);
            music = PlayerPrefs.GetFloat(K_MUSIC, music);
            sfx = PlayerPrefs.GetFloat(K_SFX, sfx);
            muteMusic = PlayerPrefs.GetInt(K_MUTE_M, muteMusic ? 1 : 0) != 0;
            muteSfx = PlayerPrefs.GetInt(K_MUTE_S, muteSfx ? 1 : 0) != 0;

            // Respect Options toggles if they exist
            muteMusic |= PlayerPrefs.GetInt(K_OPT_MUTE_MUSIC, 0) == 1;
            muteSfx |= PlayerPrefs.GetInt(K_OPT_MUTE_SFX, 0) == 1;

            // If muted, clamp channel volumes to 0 so any “read volume” logic sees mute
            if (muteMusic) music = 0f;
            if (muteSfx) sfx = 0f;

            ApplyMuteState();  // sets AudioSource.mute and effective volumes
        }

        void OnEnable()
        {
            GameEvents.OnFruitCaught += OnFruitCaught;
            GameEvents.OnPowerupStarted += OnPowerupStarted;
        }
        void OnDisable()
        {
            GameEvents.OnFruitCaught -= OnFruitCaught;
            GameEvents.OnPowerupStarted -= OnPowerupStarted;
            if (Instance == this) Instance = null;
        }

        // ---------- Event handlers ----------
        void OnFruitCaught(string id, int score, bool isBomb) => PlaySFX(isBomb ? sfxBomb : sfxCatch);
        void OnPowerupStarted(PowerupDef def) { if (def) PlayPowerupStart(def.kind); }

        // ---------- Public API ----------
        public void PlaySFX(AudioClip clip, float vol = 1f)
        {
            if (!clip || muteSfx) return;

            float final = Mathf.Clamp01(vol) * master * sfx;

            if (sfxSource)
            {
                sfxSource.PlayOneShot(clip, final);
            }
            else if (fallbackToAtPoint)
            {
                var pos = Camera.main ? Camera.main.transform.position : Vector3.zero;
                AudioSource.PlayClipAtPoint(clip, pos, final);
                if (debugLogs) Debug.LogWarning("[AudioManager] SFX source missing — using PlayClipAtPoint fallback.");
            }
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (!clip || !musicSource) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            ApplyMuteState();
            if (!muteMusic) musicSource.Play();
        }
        public void StopMusic() { if (musicSource) musicSource.Stop(); }

        public void PlayUIButton() => PlaySFX(sfxUIButton, 0.9f);
        public void PlayUIButtonClick() => PlayUIButton(); // shim for legacy callers
        public void PlayShieldBreak() { if (sfxShieldBreak) PlaySFX(sfxShieldBreak); }

        public void PlayPowerupStart(PowerupDef def) => PlayPowerupStart(def?.kind ?? PowerupDef.PowerupKind.ScoreMultiplier);
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

        // -------- UI setters (work with OptionManager & Pause) --------
        public void SetMaster(float v)
        {
            master = Mathf.Clamp01(v);
            Save(); ApplyMuteState();
            OnVolumesChanged?.Invoke(master, music, sfx);
        }

        // OptionManager passes 0 (mute) or default (>0) — interpret accordingly
        public void SetMusic(float v)
        {
            music = Mathf.Clamp01(v);
            bool newMute = music <= 0.0001f;
            if (muteMusic != newMute) ToggleMusicMuteInternal(newMute, raiseEvents: true);
            Save(); ApplyMuteState();
            OnVolumesChanged?.Invoke(master, music, sfx);
        }

        public void SetSFX(float v)
        {
            sfx = Mathf.Clamp01(v);
            bool newMute = sfx <= 0.0001f;
            if (muteSfx != newMute) ToggleSfxMuteInternal(newMute, raiseEvents: true);
            Save(); ApplyMuteState();
            OnVolumesChanged?.Invoke(master, music, sfx);
        }

        // Direct mute APIs (e.g., Pause toggles)
        public void SetMuteMusic(bool m)
        {
            ToggleMusicMuteInternal(m, raiseEvents: true);
            if (!muteMusic && music <= 0.0001f) music = defaultMusicVol; // restore sensible default on unmute
            if (muteMusic) music = 0f;
            Save(); ApplyMuteState();
            OnVolumesChanged?.Invoke(master, music, sfx);
        }

        public void SetMuteSFX(bool m)
        {
            ToggleSfxMuteInternal(m, raiseEvents: true);
            if (!muteSfx && sfx <= 0.0001f) sfx = defaultSfxVol;
            if (muteSfx) sfx = 0f;
            Save(); ApplyMuteState();
            OnVolumesChanged?.Invoke(master, music, sfx);
        }

        // Read-only props for UI init
        public bool IsMusicMuted => muteMusic;
        public bool IsSfxMuted => muteSfx;
        public float MasterVolume => master;
        public float MusicVolume => music;
        public float SfxVolume => sfx;

        // ---------- internals ----------
        void ToggleMusicMuteInternal(bool m, bool raiseEvents)
        {
            muteMusic = m;
            PlayerPrefs.SetInt(K_MUTE_M, muteMusic ? 1 : 0);
            PlayerPrefs.SetInt(K_OPT_MUTE_MUSIC, muteMusic ? 1 : 0);
            if (raiseEvents) OnMuteMusicChanged?.Invoke(muteMusic);
            if (debugLogs) Debug.Log($"[AudioManager] Music mute={muteMusic}");
        }

        void ToggleSfxMuteInternal(bool m, bool raiseEvents)
        {
            muteSfx = m;
            PlayerPrefs.SetInt(K_MUTE_S, muteSfx ? 1 : 0);
            PlayerPrefs.SetInt(K_OPT_MUTE_SFX, muteSfx ? 1 : 0);
            if (raiseEvents) OnMuteSfxChanged?.Invoke(muteSfx);
            if (debugLogs) Debug.Log($"[AudioManager] SFX mute={muteSfx}");
        }

        void ApplyMuteState()
        {
            // 1) Enforce AudioSource.mute flags
            if (musicSource) musicSource.mute = muteMusic || master <= 0f || music <= 0f;
            if (sfxSource) sfxSource.mute = muteSfx || master <= 0f || sfx <= 0f;

            // 2) Apply effective volumes (master * channel)
            if (musicSource) musicSource.volume = (muteMusic ? 0f : 1f) * master * music;

            // 3) Nothing to set on SFX volume here (we apply mix per PlaySFX call),
            //    but keeping sfxSource.mute in sync guarantees silence if it is used.
        }

        void Save()
        {
            PlayerPrefs.SetFloat(K_MASTER, master);
            PlayerPrefs.SetFloat(K_MUSIC, music);
            PlayerPrefs.SetFloat(K_SFX, sfx);
            PlayerPrefs.SetInt(K_MUTE_M, muteMusic ? 1 : 0);
            PlayerPrefs.SetInt(K_MUTE_S, muteSfx ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
