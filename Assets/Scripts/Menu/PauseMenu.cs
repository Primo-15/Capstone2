using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string mainMenuScene = "Main Menu";
    [SerializeField] private Behaviour[] disableWhilePaused;

    private bool isPaused;

    private void Start()
    {
        pausePanel.SetActive(false);
    }

    private void Update()
    {
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
        Time.timeScale = 1f; // important, otherwise the main menu loads frozen
        SetGameplayInput(true);
        SceneManager.LoadScene(mainMenuScene);
    }
}