using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Lightweight per-scene music controller (used in M1, M3, and M5).
/// - Plays and loops the background music when the scene starts (or after the intro video finishes).
/// - Switches to QTE music (and plays QTE hit sounds) for Quick Time Events.
/// - Pauses/resumes together with the PauseSystem.
/// It lives in the scene, so everything stops automatically when the scene is left.
public class SceneMusic : MonoBehaviour
{
    public static SceneMusic Current { get; private set; }

    [Header("Background Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [Range(0f, 1f)] [SerializeField] private float backgroundVolume = 0.6f;

    [Header("Quick Time Event (optional)")]
    [SerializeField] private AudioClip qteMusic;
    [SerializeField] private AudioClip qteHitPass;
    [SerializeField] private AudioClip qteHitFail;
    [Range(0f, 1f)] [SerializeField] private float qteMusicVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    private AudioSource bgSource;
    private AudioSource qteSource;
    private AudioSource sfxSource;

    private bool qteMusicActive;
    private bool paused;
    private bool stoppedForOutro;

    public float BaseBackgroundVolume => backgroundVolume;
    public float BaseQteMusicVolume => qteMusicVolume;
    public float BaseSfxVolume => sfxVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureM5MusicInActiveScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureM5MusicInActiveScene(scene);
    }

    private static void EnsureM5MusicInActiveScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "M5")
            return;

        if (Current != null)
            return;

        SceneMusic existing = FindFirstObjectByType<SceneMusic>();
        if (existing != null)
            return;

        GameObject go = new GameObject("SceneMusic");
        go.AddComponent<SceneMusic>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (backgroundMusic == null && gameObject.scene.IsValid() && gameObject.scene.name == "M5")
        {
            backgroundMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Prefabs/BgMusic/M5BgMusic.mp3");
        }
    }
#endif

    private void Awake()
    {
        // Prevent duplicated music if a second controller ever exists.
        if (Current != null && Current != this)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;

#if UNITY_EDITOR
        if (backgroundMusic == null && SceneManager.GetActiveScene().name == "M5")
        {
            backgroundMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Prefabs/BgMusic/M5BgMusic.mp3");
        }
#endif

        bgSource = CreateSource(backgroundMusic, backgroundVolume, true);
        qteSource = CreateSource(qteMusic, qteMusicVolume, true);
        sfxSource = CreateSource(null, sfxVolume, false);

        ApplySettingsVolumes();
    }

    private AudioSource CreateSource(AudioClip clip, float volume, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void Start()
    {
        ApplySettingsVolumes();

        if (backgroundMusic == null)
        {
            Debug.LogWarning("SceneMusic: no background music assigned.", this);
            return;
        }

        if (MapVideoController.IsVideoPlaying)
        {
            MapVideoController.OnIntroVideoFinished += HandleIntroFinishedForMusic;
            return;
        }

        if (!stoppedForOutro && !bgSource.isPlaying)
            bgSource.Play();
    }

    private void HandleIntroFinishedForMusic()
    {
        MapVideoController.OnIntroVideoFinished -= HandleIntroFinishedForMusic;
        if (this != null && !stoppedForOutro && bgSource != null && !bgSource.isPlaying && !paused && backgroundMusic != null)
        {
            ApplySettingsVolumes();
            bgSource.Play();
        }
    }

    /// <summary>
    /// Permanently stops background and QTE music when the final quest completes before the outro video starts.
    /// </summary>
    public void StopForOutro()
    {
        stoppedForOutro = true;
        MapVideoController.OnIntroVideoFinished -= HandleIntroFinishedForMusic;

        if (bgSource != null)
        {
            bgSource.Stop();
            bgSource.clip = null;
        }

        if (qteSource != null)
        {
            qteSource.Stop();
            qteSource.clip = null;
        }
    }

    public static void StopCurrentForOutro()
    {
        if (Current != null)
            Current.StopForOutro();
    }

    private void OnDestroy()
    {
        MapVideoController.OnIntroVideoFinished -= HandleIntroFinishedForMusic;

        if (Current == this)
            Current = null;
    }

    public void ApplySettingsVolumes()
    {
        MenuSettings.EnsureLoaded();

        if (bgSource != null)
        {
            bgSource.volume = MenuSettings.ComputeBgMusicVolume(backgroundVolume);
        }

        // QTEBgMusic, QteHitPass, and QTEHitFail are controlled by the Sound FX setting.
        if (qteSource != null)
        {
            qteSource.volume = MenuSettings.ComputeSfxVolume(qteMusicVolume);
        }

        if (sfxSource != null)
        {
            sfxSource.volume = MenuSettings.ComputeSfxVolume(sfxVolume);
        }
    }

    // ---------- Quick Time Event ----------

    public void BeginQTE()
    {
        if (stoppedForOutro || qteMusicActive) return;   // never stack QTE music
        qteMusicActive = true;

        ApplySettingsVolumes();

        if (bgSource.isPlaying)
            bgSource.Pause();          // keeps the playback position

        if (qteMusic != null && !qteSource.isPlaying)
            qteSource.Play();
    }

    // resumeBackground = true after a success; false when the game over screen takes over.
    public void EndQTE(bool resumeBackground)
    {
        qteMusicActive = false;
        qteSource.Stop();

        if (stoppedForOutro)
            return;

        ApplySettingsVolumes();

        if (resumeBackground && backgroundMusic != null && !paused)
            bgSource.UnPause();
    }

    public void PlayHitPass()
    {
        PlayEffect(qteHitPass);
    }

    public void PlayHitFail()
    {
        PlayEffect(qteHitFail);
    }

    private void PlayEffect(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        float effectiveVolume = MenuSettings.ComputeSfxVolume(sfxVolume);
        sfxSource.volume = 1f;
        sfxSource.PlayOneShot(clip, effectiveVolume);
    }

    // ---------- PauseSystem ----------

    public void SetPaused(bool value)
    {
        if (stoppedForOutro || paused == value) return;
        paused = value;

        if (paused)
        {
            if (!qteMusicActive) bgSource.Pause();
            else qteSource.Pause();
        }
        else
        {
            ApplySettingsVolumes();
            if (!qteMusicActive) bgSource.UnPause();
            else qteSource.UnPause();
        }
    }

    public static void NotifyPaused(bool value)
    {
        if (Current != null)
            Current.SetPaused(value);
    }
}
