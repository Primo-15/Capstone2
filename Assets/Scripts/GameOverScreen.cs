using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private string mainMenuScene = "Main Menu";
    [SerializeField] private string followCameraName = "PlayerFollowCamera";
    [SerializeField] private string cursorScriptPrefix = "CursorAlt";
    [SerializeField] private Behaviour[] disableOnGameOver;

    private bool isGameOver;

    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        gameOverPanel.SetActive(false);
        AutoFindGameplayInput();
    }

    // Finds the follow camera and the cursor-toggle script in whatever scene
    // this is placed in, so no per-scene assigning is needed.
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

        // The script that locks/unlocks the cursor with Alt.
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }
}