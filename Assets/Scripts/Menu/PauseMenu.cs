using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string mainMenuScene = "Main Menu";
    [SerializeField] private string followCameraName = "PlayerFollowCamera";
    [SerializeField] private Behaviour[] disableWhilePaused;

    private bool isPaused;

    private void Start()
    {
        pausePanel.SetActive(false);
        AutoFindGameplayInput();
    }

    // Finds the follow camera in whatever scene this prefab is dropped into.
    // Anything you assigned by hand in the Inspector is kept.
    private void AutoFindGameplayInput()
    {
        var list = new List<Behaviour>();
        if (disableWhilePaused != null)
            list.AddRange(disableWhilePaused);

        GameObject cam = GameObject.Find(followCameraName);

        if (cam == null)
        {
            Debug.LogWarning("PauseMenu: could not find '" + followCameraName +
                "' in this scene, so the camera won't freeze while paused.", this);
        }
        else
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

        disableWhilePaused = list.ToArray();
    }

    private void Update()
    {
        // Don't allow pausing during game over or a quick time event.
        if (GameOverScreen.Instance != null && GameOverScreen.Instance.IsGameOver)
            return;

        if (QuickTimeEvent.Instance != null && QuickTimeEvent.Instance.IsActive)
            return;

        if (EscapePressed())
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    private bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void SetGameplayInput(bool enabled)
    {
        foreach (var b in disableWhilePaused)
        {
            if (b != null) b.enabled = enabled;
        }
    }

    public void Pause()
    {
        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        SetGameplayInput(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        SetGameplayInput(true);
        // Re-lock the cursor here only if your player uses a locked cursor
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
    }

    public void GoToMainMenu()
    {
        PlayerStats.ResetAll();   // fresh hearts and stamina for the next run

        Time.timeScale = 1f; // important, otherwise the main menu loads frozen
        SetGameplayInput(true);
        SceneManager.LoadScene(mainMenuScene);
    }
}