using UnityEngine;

public class AudioHub : MonoBehaviour
{
    public static AudioHub I;

    [Header("Background Music")]
    public AudioClip backgroundLoop;

    [Header("SFX Clips")]
    public AudioClip pickup, bomb, button;
    public AudioClip freezeOn, freezeOff;
    public AudioClip shieldOn, shieldHit;
    public AudioClip clear;
    public AudioClip magnet;   // NEW

    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;

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

        if (backgroundLoop) StartMusic();
    }

    private AudioSource MakeSrc(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = loop;
        a.spatialBlend = 0f; // 2D
        a.volume = loop ? musicVolume : sfxVolume;
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
    public void PlayPickup() => Play(pickup);
    public void PlayBomb() => Play(bomb);
    public void PlayButton() => Play(button);
    public void PlayFreezeOn() => Play(freezeOn);
    public void PlayFreezeOff() => Play(freezeOff);
    public void PlayShieldOn() => Play(shieldOn);
    public void PlayShieldHit() => Play(shieldHit);
    public void PlayClear() => Play(clear);
    public void PlayMagnet() => Play(magnet);   // NEW

    public void StartMusic(AudioClip loop = null)
    {
        if (loop) backgroundLoop = loop;
        if (!backgroundLoop) return;
        music.clip = backgroundLoop;
        music.volume = musicVolume;
        music.Play();
    }

    public void StopMusic() => music.Stop();

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);   // ✅ fixed
        if (music) music.volume = musicVolume;
    }

    private void Play(AudioClip clip, float volMul = 1f, float pitch = 1f)
    {
        if (!clip) return;
        float old = sfx.pitch;
        sfx.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        sfx.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volMul));
        sfx.pitch = old;
    }
}
