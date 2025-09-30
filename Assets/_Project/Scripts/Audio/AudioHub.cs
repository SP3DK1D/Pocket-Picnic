using UnityEngine;

public class AudioHub : MonoBehaviour
{
    public static AudioHub I;

    [Header("Background Music")]
    public AudioClip backgroundLoop;

    [Header("SFX Clips")]
    public AudioClip pickup, bomb, button;
    public AudioClip freezeOn;
    public AudioClip shieldOn, shieldBreak;
    public AudioClip clear;
    public AudioClip magnet;
    public AudioClip scoreMultiplier;

    [Range(0f, 1f)] public float sfxVolume = 0.45f;
    [Range(0f, 1f)] public float musicVolume = 0.05f;

    private AudioSource sfx;
    private AudioSource music;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        EnsureListener();

        sfx = MakeSrc("SFX", loop: false);
        music = MakeSrc("Music", loop: true);

        // make sure sources reflect current slider values on boot
        ApplyVolumesNow();

        if (backgroundLoop) StartMusic();
    }

    void OnEnable()
    {
        // safety: re-apply if values were changed in inspector while disabled
        ApplyVolumesNow();
    }

    private AudioSource MakeSrc(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = loop;
        a.spatialBlend = 0f; // 2D
        return a;
    }

    private void EnsureListener()
    {
        if (!FindObjectOfType<AudioListener>())
        {
            var cam = Camera.main ? Camera.main.gameObject : new GameObject("AudioListener_Auto");
            if (!cam.TryGetComponent<AudioListener>(out _)) cam.AddComponent<AudioListener>();
        }
    }

    // ---- Public helpers ----
    public void PlayPickup()          => Play(pickup);
    public void PlayBomb()            => Play(bomb);
    public void PlayButton()          => Play(button);
    public void PlayFreezeOn()        => Play(freezeOn);
    public void PlayShieldOn()        => Play(shieldOn);
    public void PlayShieldBreak()     => Play(shieldBreak);
    public void PlayClear()           => Play(clear);
    public void PlayMagnet()          => Play(magnet);
    public void PlayScoreMultiplier() => Play(scoreMultiplier);

    public void StartMusic(AudioClip loop = null)
    {
        if (loop) backgroundLoop = loop;
        if (!backgroundLoop) return;
        music.clip = backgroundLoop;
        music.volume = musicVolume; // ensure current slider applied
        music.Play();
    }

    public void StopMusic() => music.Stop();

    // === Volume controls (hook these to your UI sliders) ===
    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (sfx) sfx.volume = sfxVolume; // affects any non-OneShot plays; OneShot uses sfxVolume below
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (music) music.volume = musicVolume; // live adjust currently playing music
    }

    /// <summary>Immediately applies current slider values to sources.</summary>
    public void ApplyVolumesNow()
    {
        if (sfx)   sfx.volume   = Mathf.Clamp01(sfxVolume);
        if (music) music.volume = Mathf.Clamp01(musicVolume);
    }

#if UNITY_EDITOR
    // Reflect inspector slider changes in-editor (even when not playing)
    void OnValidate()
    {
        // During edit-time, sources may be null until Awake; guard it.
        if (sfx)   sfx.volume   = Mathf.Clamp01(sfxVolume);
        if (music) music.volume = Mathf.Clamp01(musicVolume);
    }
#endif

    private void Play(AudioClip clip, float volMul = 1f, float pitch = 1f)
    {
        if (!clip || !sfx) return;
        float old = sfx.pitch;
        sfx.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        // Use sfxVolume every shot so the slider takes effect instantly
        sfx.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volMul));
        sfx.pitch = old;
    }
}
