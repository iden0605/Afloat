using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that persists across scenes.
/// Handles background music and one-shot SFX.
///
/// ── Scene setup ──
///   1. Add AudioManager to a GameObject in the MainMenu scene.
///   2. Add TWO AudioSource components on the same GameObject.
///   3. Assign the first AudioSource to Music Source, the second to SFX Source.
///   4. On the Music AudioSource: check Loop, set Volume ~0.5, uncheck Play On Awake.
///   5. On the SFX AudioSource: uncheck Loop, uncheck Play On Awake.
///   6. Drag all clips from Assets/Audio into the matching fields below.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    public AudioClip bgmLobby;
    public AudioClip bgmMain;

    [Header("SFX")]
    public AudioClip sfxEnemyHit;
    public AudioClip sfxPlayerHpLoss;  // optional — leave empty until you have the clip
    public AudioClip sfxWaveStart;     // optional — leave empty until you have the clip

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Switch music automatically when a scene loads.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
            PlayMusic(bgmLobby);
        else if (scene.name == "Main")
            PlayMusic(bgmMain);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || (musicSource.clip == clip && musicSource.isPlaying)) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PauseMusic()  => musicSource.Pause();
    public void ResumeMusic() => musicSource.UnPause();

    public void SetMusicVolume(float v) => musicSource.volume = Mathf.Clamp01(v);
    public void SetSFXVolume(float v)   => sfxSource.volume   = Mathf.Clamp01(v);
}
