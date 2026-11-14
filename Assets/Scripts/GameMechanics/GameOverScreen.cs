using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text triesLeftText;
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Color disabledLabelColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private string mainMenuScene = "Main Menu";
    [SerializeField] private string previousStageScene = "M2";
    [SerializeField] private string returnToLastStageLabel = "Return to the last stage";
    [SerializeField] private string followCameraName = "PlayerFollowCamera";
    [SerializeField] private string cursorScriptPrefix = "CursorAlt";
    [SerializeField] private Behaviour[] disableOnGameOver;

    [Header("Tries")]
    [Tooltip("If ticked, hearts are ignored: no tries counter, and Try Again can be clicked forever.")]
    [SerializeField] private bool infiniteTries = false;

    private bool isGameOver;
    private TMP_Text tryAgainLabel;
    private Color tryAgainLabelColor = new Color(0.196f, 0.196f, 0.196f, 1f);
    private string defaultTryAgainText = "Try again";

    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        EnsureUIReferences();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        AutoFindGameplayInput();
    }

    private void EnsureUIReferences()
    {
        if (gameOverPanel == null)
            return;

        if (tryAgainButton == null)
        {
            Transform btnTransform = gameOverPanel.transform.Find("TryAgainButton");
            if (btnTransform != null)
            {
                tryAgainButton = btnTransform.GetComponent<Button>();
            }
        }

        if (tryAgainButton != null && tryAgainLabel == null)
        {
            tryAgainLabel = tryAgainButton.GetComponentInChildren<TMP_Text>(true);
            if (tryAgainLabel != null)
            {
                tryAgainLabelColor = tryAgainLabel.color;
                if (!string.IsNullOrEmpty(tryAgainLabel.text))
                    defaultTryAgainText = tryAgainLabel.text;
            }
        }
    }

    private void AutoFindGameplayInput()
    {
        var list = new List<Behaviour>();
        if (disableOnGameOver != null)
            list.AddRange(disableOnGameOver);

        GameObject cam = GameObject.Find(followCameraName);
        if (cam != null)
        {
            foreach (Behaviour b in cam.GetComponents<Behaviour>())
            {
                if (b != null &&
                    b.GetType().Name == "CinemachineCamera" &&
                    !list.Contains(b))
                {
                    list.Add(b);
                }
            }
        }

        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(
            FindObjectsSortMode.None))
        {
            if (mb != null &&
                mb.GetType().Name.StartsWith(cursorScriptPrefix) &&
                !list.Contains(mb))
            {
                list.Add(mb);
            }
        }

        disableOnGameOver = list.ToArray();
    }

    public void Show()
    {
        if (isGameOver) return;
        if (MapVideoController.IsVideoPlaying) return;
        if (QuestSceneTransition.Instance != null && QuestSceneTransition.Instance.IsTransitioning) return;
        isGameOver = true;

        EnsureUIReferences();

        // Every game over costs one heart (and refills stamina),
        // unless tries are infinite.
        if (!infiniteTries)
            PlayerStats.LoseHeart();

        if (triesLeftText != null)
        {
            // Hide the counter entirely when tries are infinite.
            triesLeftText.gameObject.SetActive(!infiniteTries);

            if (!infiniteTries)
            {
                triesLeftText.text = PlayerStats.HasHearts
                    ? "Tries left: " + PlayerStats.Hearts
                    : "No tries left";
            }
        }

        bool isM3 = SceneManager.GetActiveScene().name == "M3";
        bool lostAllHeartsInM3 = isM3 && !infiniteTries && !PlayerStats.HasHearts;
        bool canRetry = infiniteTries || PlayerStats.HasHearts;

        if (lostAllHeartsInM3)
        {
            // Ensure checkpoint is immediately moved back to M2 when all 3 hearts are lost in M3.
            SaveSystem.SaveMapCheckpoint(previousStageScene);
        }

        if (tryAgainButton != null)
        {
            if (lostAllHeartsInM3)
            {
                // Replace "Try again" with "Return to the last stage" and keep button clickable.
                tryAgainButton.interactable = true;

                RectTransform btnRect = tryAgainButton.GetComponent<RectTransform>();
                if (btnRect != null && btnRect.sizeDelta.x < 260f)
                {
                    btnRect.sizeDelta = new Vector2(260f, btnRect.sizeDelta.y);
                }

                if (tryAgainLabel != null)
                {
                    tryAgainLabel.text = returnToLastStageLabel;
                    tryAgainLabel.color = tryAgainLabelColor;
                }
            }
            else
            {
                tryAgainButton.interactable = canRetry;

                if (tryAgainLabel != null)
                {
                    tryAgainLabel.text = defaultTryAgainText;
                    tryAgainLabel.color = canRetry
                        ? tryAgainLabelColor
                        : disabledLabelColor;
                }
            }
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        Time.timeScale = 0f;

        if (disableOnGameOver != null)
        {
            foreach (var b in disableOnGameOver)
                if (b != null) b.enabled = false;
        }

        ShowCursor();
    }

    // LateUpdate runs after other scripts, so nothing can re-lock the cursor.
    private void LateUpdate()
    {
        if (isGameOver)
            ShowCursor();
    }

    private void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void TryAgain()
    {
        if (!infiniteTries && !PlayerStats.HasHearts)
        {
            if (SceneManager.GetActiveScene().name == "M3")
            {
                // Return to the last stage (M2) with fresh hearts for the next attempt.
                SaveSystem.SaveMapCheckpoint(previousStageScene);
                PlayerStats.ResetAll();
                Time.timeScale = 1f;
                SceneManager.LoadScene(previousStageScene);
            }
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        if (SceneManager.GetActiveScene().name == "M3")
        {
            if (PlayerStats.HasHearts)
            {
                // Keep the remaining hearts in M3 so Main Menu -> Continue does not refill hearts.
                SaveSystem.SaveM3Hearts(PlayerStats.Hearts);
                PlayerStats.RefillStamina();
            }
            else
            {
                // Lost all 3 hearts in M3: Continue from Main Menu must go back to M2.
                SaveSystem.SaveMapCheckpoint(previousStageScene);
                PlayerStats.ResetAll();
            }
        }
        else
        {
            PlayerStats.ResetAll();   // fresh hearts and stamina for the next run
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }
}
