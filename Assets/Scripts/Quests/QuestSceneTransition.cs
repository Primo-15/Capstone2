
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestSceneTransition : MonoBehaviour
{
    public static QuestSceneTransition Instance { get; private set; }

    [Header("Transition Message")]
    [SerializeField] private CanvasGroup transitionOverlay;
    [SerializeField] private TMP_Text transitionMessage;
    [SerializeField] private string clearedMessage = "MAP 1 CLEARED";
    [SerializeField] private float transitionDelay = 3f;

    [Header("Disable Gameplay")]
    [Tooltip("Add the player movement and input scripts here.")]
    [SerializeField]
    private MonoBehaviour[] gameplayComponentsToDisable;

    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transitionOverlay != null)
            transitionOverlay.alpha = 0f;
    }

    public bool LoadNextScene(string sceneName)
    {
        if (isTransitioning)
        {
            Debug.LogWarning(
                "A scene transition is already in progress."
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                "Cannot load a scene: the scene name is empty."
            );
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Cannot load scene '{sceneName}'. " +
                "Check its name and active Build Profile."
            );
            return false;
        }

        isTransitioning = true;

        StopAllGameplayForTransition();

        if (gameplayComponentsToDisable != null)
        {
            foreach (MonoBehaviour component in
                gameplayComponentsToDisable)
            {
                if (component != null)
                    component.enabled = false;
            }
        }

        bool hasOutro = MapVideoController.Instance != null && MapVideoController.Instance.HasOutroVideo;

        if (transitionMessage != null)
            transitionMessage.text = clearedMessage;

        if (transitionOverlay != null)
        {
            // If an outro video is about to play, keep transitionOverlay invisible until after the outro video
            transitionOverlay.alpha = hasOutro ? 0f : 1f;
            transitionOverlay.interactable = false;
            transitionOverlay.blocksRaycasts = true;
        }

        StartCoroutine(LoadSceneAfterDelay(sceneName, hasOutro));

        return true;
    }

    private void StopAllGameplayForTransition()
    {
        // 1. Stop all patrolling/chasing guards immediately so they cannot move or trigger Game Over during outro
        GuardPatrol.StopAllGuards();

        // 2. Stop player movement, reset Animator speed/motion floats to idle, and lock camera
        StarterAssets.ThirdPersonController[] controllers =
            FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c != null)
            {
                c.StopMovementImmediately();
                c.LockCameraPosition = true;
                c.enabled = false;
            }
        }

        // 3. Zero out all player inputs
        StarterAssets.StarterAssetsInputs[] inputs =
            FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var i in inputs)
        {
            if (i != null)
            {
                i.move = Vector2.zero;
                i.look = Vector2.zero;
                i.jump = false;
                i.sprint = false;
            }
        }

        // 4. Hide M5 Inventory Hotbar if present
        M5HotbarController[] hotbars = FindObjectsByType<M5HotbarController>(FindObjectsSortMode.None);
        foreach (var hb in hotbars)
        {
            if (hb != null)
            {
                hb.SetHotbarUIHidden(true);
            }
        }

        // 5. Stop scene background music immediately before the outro video / transition
        SceneMusic.StopCurrentForOutro();

        // 6. Silence any active one-shot footstep audio sources spawned by AudioSource.PlayClipAtPoint
        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var src in audioSources)
        {
            if (src != null && src.gameObject.name == "One shot audio")
            {
                src.Stop();
                Destroy(src.gameObject);
            }
        }
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, bool hasOutro)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        // If completing a scene with an outro video (e.g. M5Outro in M5), play it first!
        if (hasOutro && MapVideoController.Instance != null)
        {
            bool outroFinished = false;
            MapVideoController.Instance.PlayOutroVideo(() =>
            {
                outroFinished = true;
            });

            while (!outroFinished)
            {
                yield return null;
            }

            // Restore transition overlay message after outro video completes
            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 1f;
                if (transitionMessage != null)
                    transitionMessage.text = clearedMessage;
            }
        }

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, transitionDelay)
        );

        // If completing M5 (the final chapter), mark the game as permanently completed!
        if (currentScene == "M5")
        {
            SaveSystem.SetGameCompleted(true);
        }

        // Chapter Replay Mode: Return directly to Main Menu without advancing or saving checkpoints
        if (SaveSystem.IsChapterReplayMode)
        {
            Debug.Log($"[ReplayMode] Chapter {currentScene} finished. Returning directly to Main Menu.");
            SaveSystem.IsChapterReplayMode = false;
            SceneManager.LoadScene("Main Menu");
            yield break;
        }

        // Normal Story Mode:
        if (sceneName != "Main Menu")
        {
            // Save the next map, NOT the completed quest.
            SaveSystem.SaveMapCheckpoint(sceneName);
        }
        else
        {
            // Story completed: clear in-progress checkpoint
            SaveSystem.Delete();
        }

        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}