using UnityEngine;

public class AudioHub : MonoBehaviour
{
    public static AudioHub I;

    [Header("Background Music")]
    public AudioClip backgroundLoop;

    [Header("SFX Clips")]
    public AudioClip pickup;
    public AudioClip bomb;
    public AudioClip button;
    public AudioClip freezeOn;
    public AudioClip freezeOff;
    public AudioClip shieldOn;
    public AudioClip shieldHit;
    public AudioClip clear;

    [Header("Volumes (0..1)")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;

    private AudioSource sfxSource;
    private AudioSource musicSource;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        EnsureListener();
        sfxSource = MakeSource("SFX", loop: false);
        musicSource = MakeSource("Music", loop: true);

        if (backgroundLoop) StartMusic();
    }

    private void EnsureListener()
    {
        if (!FindObjectOfType<AudioListener>())
        {
            var cam = Camera.main ? Camera.main.gameObject : new GameObject("AudioListener_Auto");
            if (!cam.TryGetComponent<AudioListener>(out _)) cam.AddComponent<AudioListener>();
        }
    }

    private AudioSource MakeSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.loop = loop;
        a.playOnAwake = false;
        a.spatialBlend = 0f; // 2D
        a.volume = loop ? musicVolume : sfxVolume;
        return a;
    }

    // ---------- Public API ----------
    public void PlayPickup() => PlaySFX(pickup);
    public void PlayBomb() => PlaySFX(bomb);
    public void PlayButton() => PlaySFX(button);
    public void PlayFreezeOn() => PlaySFX(freezeOn);
    public void PlayFreezeOff() => PlaySFX(freezeOff);
    public void PlayShieldOn() => PlaySFX(shieldOn);
    public void PlayShieldHit() => PlaySFX(shieldHit);
    public void PlayClear() => PlaySFX(clear);

    public void StartMusic(AudioClip loop = null)
    {
        if (loop) backgroundLoop = loop;
        if (!backgroundLoop) return;
        musicSource.clip = backgroundLoop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }
    public void StopMusic() => musicSource.Stop();

    public void SetSfxVolume(float v) { sfxVolume = Mathf.Clamp01(v); }
    public void SetMusicVolume(float v) { musicVolume = Mathf.Clamp01(v); musicSource.volume = musicVolume; }

    // ---------- Internals ----------
    private void PlaySFX(AudioClip clip, float volumeMul = 1f, float pitch = 1f)
    {
        if (!clip) return;
        float oldPitch = sfxSource.pitch;
        sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeMul));
        sfxSource.pitch = oldPitch;
    }
}
