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
    [SerializeField] private string followCameraName = "PlayerFollowCamera";
    [SerializeField] private string cursorScriptPrefix = "CursorAlt";
    [SerializeField] private Behaviour[] disableOnGameOver;

    private bool isGameOver;
    private TMP_Text tryAgainLabel;
    private Color tryAgainLabelColor = Color.black;

    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        gameOverPanel.SetActive(false);

        // Remember the label's normal color so it can be restored.
        if (tryAgainButton != null)
        {
            tryAgainLabel = tryAgainButton.GetComponentInChildren<TMP_Text>(true);
            if (tryAgainLabel != null)
                tryAgainLabelColor = tryAgainLabel.color;
        }

        AutoFindGameplayInput();
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
        isGameOver = true;

        // Every game over costs one heart (and refills stamina).
        PlayerStats.LoseHeart();

        if (triesLeftText != null)
        {
            triesLeftText.text = PlayerStats.HasHearts
                ? "Tries left: " + PlayerStats.Hearts
                : "No tries left";
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.interactable = PlayerStats.HasHearts;

            // Grey out the label too when Try Again is unavailable.
            if (tryAgainLabel != null)
            {
                tryAgainLabel.color = PlayerStats.HasHearts
                    ? tryAgainLabelColor
                    : disabledLabelColor;
            }
        }

        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;

        foreach (var b in disableOnGameOver)
            if (b != null) b.enabled = false;

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
        if (!PlayerStats.HasHearts)
            return; // no hearts left, Main Menu only

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        PlayerStats.ResetAll();   // fresh hearts and stamina for the next run
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }
}