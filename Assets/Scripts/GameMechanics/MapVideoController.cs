using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapVideoController : MonoBehaviour
{
    public static MapVideoController Instance { get; private set; }
    public static bool IsVideoPlaying { get; private set; }

    public static event Action OnIntroVideoFinished;
    public static event Action OnOutroVideoFinished;

    [Header("Video Clips")]
    [SerializeField] private VideoClip introVideo;
    [SerializeField] private VideoClip outroVideo;

    [Header("Options")]
    [SerializeField] private bool playIntroOnStart = true;
    [SerializeField] private bool muteBackgroundMusicDuringVideo = true;

    [Header("UI & Video Components")]
    [SerializeField] private Canvas videoCanvas;
    [SerializeField] private RawImage videoRawImage;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource videoAudioSource;

    private RenderTexture renderTexture;
    private Action onCurrentVideoFinished;
    private bool hasIntroPlayed;
    private bool hasOutroPlayed;

    private readonly List<AudioSource> pausedAudioSources = new List<AudioSource>();

    public bool HasIntroVideo => introVideo != null;
    public bool HasOutroVideo => outroVideo != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        AutoAssignClipsIfEmpty();
        EnsureComponents();

        // If playing intro on start, freeze immediately in Awake so other systems
        // (music, HUD, dialogue) initialize with IsVideoPlaying == true.
        if (playIntroOnStart && introVideo != null && !hasIntroPlayed)
        {
            FreezeGameplay();
        }
    }

    private void Start()
    {
        if (playIntroOnStart && introVideo != null && !hasIntroPlayed)
        {
            PlayIntroVideo();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsVideoPlaying = false;
        }

        CleanupRenderTexture();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignClipsIfEmpty();
    }
#endif

    public void AutoAssignClipsIfEmpty()
    {
#if UNITY_EDITOR
        string sceneName = gameObject.scene.name;
        if (string.IsNullOrEmpty(sceneName))
            sceneName = SceneManager.GetActiveScene().name;

        if (introVideo == null && !string.IsNullOrEmpty(sceneName))
        {
            string introPath = $"Assets/Prefabs/IntroVids/{sceneName}Intro.mp4";
            introVideo = AssetDatabase.LoadAssetAtPath<VideoClip>(introPath);
        }

        if (outroVideo == null && sceneName == "M5")
        {
            outroVideo = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Prefabs/IntroVids/M5Outro.mp4");
        }
#endif
    }

    public void EnsureComponents()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null)
                videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;

        if (videoAudioSource == null)
        {
            videoAudioSource = GetComponent<AudioSource>();
            if (videoAudioSource == null)
                videoAudioSource = gameObject.AddComponent<AudioSource>();
        }

        videoAudioSource.playOnAwake = false;
        videoAudioSource.spatialBlend = 0f;
        videoAudioSource.loop = false;

        // Build UI canvas hierarchy if not present
        if (videoCanvas == null)
        {
            Transform existingCanvas = transform.Find("VideoCanvas");
            if (existingCanvas != null)
            {
                videoCanvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (videoCanvas == null)
        {
            GameObject canvasObj = new GameObject("VideoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);

            videoCanvas = canvasObj.GetComponent<Canvas>();
            videoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            videoCanvas.overrideSorting = true;
            videoCanvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen solid black background to hide everything behind video
            GameObject bgObj = new GameObject("BlackBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = true; // Blocks all clicks from reaching underlying UI

            // Video RawImage with AspectRatioFitter
            GameObject rawImgObj = new GameObject("VideoDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            rawImgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform rawRt = rawImgObj.GetComponent<RectTransform>();
            rawRt.anchorMin = Vector2.zero;
            rawRt.anchorMax = Vector2.one;
            rawRt.offsetMin = Vector2.zero;
            rawRt.offsetMax = Vector2.zero;

            videoRawImage = rawImgObj.GetComponent<RawImage>();
            videoRawImage.color = Color.black; // Starts black to avoid flashing before first frame
            videoRawImage.raycastTarget = false;

            aspectRatioFitter = rawImgObj.GetComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectRatioFitter.aspectRatio = 16f / 9f;
        }

        if (videoCanvas != null)
        {
            videoCanvas.gameObject.SetActive(false);
        }
    }

    public void PlayIntroVideo(Action onFinished = null)
    {
        if (introVideo == null || hasIntroPlayed)
        {
            onFinished?.Invoke();
            return;
        }

        hasIntroPlayed = true;
        Debug.Log($"[MapVideoController] Starting Intro Video: {introVideo.name}");

        PlayVideo(introVideo, () =>
        {
            Debug.Log($"[MapVideoController] Intro Video Finished: {introVideo.name}");
            OnIntroVideoFinished?.Invoke();
            onFinished?.Invoke();
        });
    }

    public void PlayOutroVideo(Action onFinished = null)
    {
        if (outroVideo == null || hasOutroPlayed)
        {
            onFinished?.Invoke();
            return;
        }

        hasOutroPlayed = true;
        SceneMusic.StopCurrentForOutro();
        Debug.Log($"[MapVideoController] Starting Outro Video: {outroVideo.name}");

        PlayVideo(outroVideo, () =>
        {
            Debug.Log($"[MapVideoController] Outro Video Finished: {outroVideo.name}");
            OnOutroVideoFinished?.Invoke();
            onFinished?.Invoke();
        });
    }

    private void PlayVideo(VideoClip clip, Action onFinished)
    {
        if (clip == null)
        {
            onFinished?.Invoke();
            return;
        }

        EnsureComponents();
        FreezeGameplay();

        onCurrentVideoFinished = onFinished;

        if (videoCanvas != null)
        {
            videoCanvas.gameObject.SetActive(true);
        }

        if (videoRawImage != null)
        {
            videoRawImage.color = Color.black;
        }

        // Setup RenderTexture matching video resolution
        CleanupRenderTexture();
        int width = (int)clip.width;
        int height = (int)clip.height;
        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;

        renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        renderTexture.Create();

        videoPlayer.targetTexture = renderTexture;
        if (videoRawImage != null)
        {
            videoRawImage.texture = renderTexture;
        }

        if (aspectRatioFitter != null)
        {
            aspectRatioFitter.aspectRatio = (float)width / Mathf.Max(1, height);
        }

        // Audio track setup
        MenuSettings.EnsureLoaded();
        if (clip.audioTrackCount > 0)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
            videoAudioSource.volume = MenuSettings.ComputeSfxVolume(1f);
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }

        videoPlayer.clip = clip;
        videoPlayer.isLooping = false;

        // Unsubscribe any previous events
        videoPlayer.prepareCompleted -= OnPrepareCompleted;
        videoPlayer.loopPointReached -= OnLoopPointReached;
        videoPlayer.errorReceived -= OnErrorReceived;

        // Subscribe new events
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.loopPointReached += OnLoopPointReached;
        videoPlayer.errorReceived += OnErrorReceived;

        videoPlayer.Prepare();
    }

    private void OnPrepareCompleted(VideoPlayer source)
    {
        if (videoRawImage != null)
        {
            videoRawImage.color = Color.white;
        }

        source.Play();
    }

    private void OnLoopPointReached(VideoPlayer source)
    {
        FinishCurrentVideo();
    }

    private void OnErrorReceived(VideoPlayer source, string message)
    {
        Debug.LogError($"[MapVideoController] VideoPlayer error: {message}");
        FinishCurrentVideo();
    }

    private void FinishCurrentVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.loopPointReached -= OnLoopPointReached;
            videoPlayer.errorReceived -= OnErrorReceived;
            videoPlayer.Stop();
        }

        if (videoAudioSource != null && videoAudioSource.isPlaying)
        {
            videoAudioSource.Stop();
        }

        if (videoCanvas != null)
        {
            videoCanvas.gameObject.SetActive(false);
        }

        CleanupRenderTexture();
        UnfreezeGameplay();

        Action callback = onCurrentVideoFinished;
        onCurrentVideoFinished = null;
        callback?.Invoke();
    }

    private void CleanupRenderTexture()
    {
        if (renderTexture != null)
        {
            if (videoPlayer != null && videoPlayer.targetTexture == renderTexture)
            {
                videoPlayer.targetTexture = null;
            }

            if (videoRawImage != null && videoRawImage.texture == renderTexture)
            {
                videoRawImage.texture = null;
            }

            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    private void Update()
    {
        if (IsVideoPlaying)
        {
            KeepInputsLocked();
        }
    }

    private void KeepInputsLocked()
    {
        // Cancel inputs every frame during video
        StarterAssets.StarterAssetsInputs[] inputs =
            FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None);
        foreach (var i in inputs)
        {
            if (i == null) continue;
            i.move = Vector2.zero;
            i.look = Vector2.zero;
            i.jump = false;
            i.sprint = false;
        }

        StarterAssets.ThirdPersonController[] controllers =
            FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c == null) continue;
            c.LockCameraPosition = true;
        }

        // Hide mouse cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void FreezeGameplay()
    {
        IsVideoPlaying = true;

        KeepInputsLocked();

        // 1. Stop player movement / footstep animation and lock camera position on ThirdPersonController
        // (do not disable the controller during intro videos so character gravity/grounding and script lookups remain intact)
        foreach (var c in FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsSortMode.None))
        {
            if (c != null)
            {
                c.StopMovementImmediately();
                c.LockCameraPosition = true;
            }
        }

        // Silence any active one-shot footstep audio sources spawned by AudioSource.PlayClipAtPoint
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (src != null && src.gameObject.name == "One shot audio")
            {
                src.Stop();
                Destroy(src.gameObject);
            }
        }

        // 2. Hide M5 Hotbar if present
        foreach (var hb in FindObjectsByType<M5HotbarController>(FindObjectsSortMode.None))
        {
            if (hb != null)
            {
                hb.SetHotbarUIHidden(true);
            }
        }

        // 3. Mute / pause existing scene background audio so video audio plays cleanly
        if (muteBackgroundMusicDuringVideo)
        {
            MuteOrPauseBackgroundAudio();
        }
    }

    private void UnfreezeGameplay()
    {
        IsVideoPlaying = false;

        // If a scene transition is actively completing the stage (e.g. after M5Outro),
        // keep player movement, inputs, and hotbar disabled until the next scene loads.
        if (QuestSceneTransition.Instance != null && QuestSceneTransition.Instance.IsTransitioning)
        {
            KeepInputsLocked();
            return;
        }

        // 1. Ensure ThirdPersonController is enabled and camera unlocked
        foreach (var c in FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c != null)
            {
                c.enabled = true;
                c.LockCameraPosition = false;
            }
        }

#if ENABLE_INPUT_SYSTEM
        // 2. Ensure PlayerInput is enabled
        foreach (var pi in FindObjectsByType<PlayerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pi != null)
            {
                pi.enabled = true;
            }
        }
#endif

        foreach (var cat in FindObjectsByType<CursorAltToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cat != null)
            {
                cat.enabled = true;
            }
        }

        // 3. Ensure PlayerFollowCamera Cinemachine camera is active
        GameObject followCam = GameObject.Find("PlayerFollowCamera");
        if (followCam != null)
        {
            foreach (var b in followCam.GetComponents<Behaviour>())
            {
                if (b != null && (b.GetType().Name.Contains("Cinemachine") || b.GetType().Name.Contains("Camera")))
                {
                    b.enabled = true;
                }
            }
        }

        // 4. Ensure CinemachineBrain on MainCamera is active
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            foreach (var b in mainCam.GetComponents<Behaviour>())
            {
                if (b != null && b.GetType().Name == "CinemachineBrain")
                {
                    b.enabled = true;
                }
            }
        }

        // 5. Reset inputs cleanly
        StarterAssets.StarterAssetsInputs[] inputs =
            FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None);
        foreach (var i in inputs)
        {
            if (i == null) continue;
            i.move = Vector2.zero;
            i.look = Vector2.zero;
            i.jump = false;
            i.sprint = false;
        }

        // 6. Restore M5 Hotbar if present
        foreach (var hb in FindObjectsByType<M5HotbarController>(FindObjectsSortMode.None))
        {
            if (hb != null)
            {
                hb.SetHotbarUIHidden(false);
            }
        }

        // 7. Restore normal gameplay cursor state
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // 8. Restore background audio
        if (muteBackgroundMusicDuringVideo)
        {
            RestoreBackgroundAudio();
        }
    }

    private void MuteOrPauseBackgroundAudio()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var s in sources)
        {
            if (s == null || s == videoAudioSource) continue;
            if (pausedAudioSources.Contains(s)) continue;
            if (s.isPlaying)
            {
                s.Pause();
                pausedAudioSources.Add(s);
            }
        }
    }

    private void RestoreBackgroundAudio()
    {
        foreach (var s in pausedAudioSources)
        {
            if (s != null)
            {
                s.UnPause();
            }
        }
        pausedAudioSources.Clear();
    }
}
