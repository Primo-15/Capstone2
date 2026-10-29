
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
    [SerializeField] private MonoBehaviour[] gameplayComponentsToDisable;

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
            Debug.LogWarning("A scene transition is already in progress.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Cannot load a scene: the scene name is empty.");
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

        // Stop player movement and gameplay input.
        if (gameplayComponentsToDisable != null)
        {
            foreach (MonoBehaviour component in gameplayComponentsToDisable)
            {
                if (component != null)
                    component.enabled = false;
            }
        }

        // Display the map-cleared message.
        if (transitionMessage != null)
            transitionMessage.text = clearedMessage;

        if (transitionOverlay != null)
        {
            transitionOverlay.alpha = 1f;
            transitionOverlay.interactable = false;
            transitionOverlay.blocksRaycasts = true;
        }

        StartCoroutine(LoadSceneAfterDelay(sceneName));

        return true;
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName)
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, transitionDelay)
        );

        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
