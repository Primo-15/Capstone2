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
    private readonly List<Behaviour> pausedPlayerBehaviours = new List<Behaviour>();

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
        // Don't allow pausing during game over, a quick time event, or while M3Tooltip is open.
        if (GameOverScreen.Instance != null && GameOverScreen.Instance.IsGameOver)
            return;

        if (QuickTimeEvent.Instance != null && QuickTimeEvent.Instance.IsActive)
            return;

        if (PlayerStatsHUD.IsTooltipOpen)
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
        FreezePlayer();
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        SetGameplayInput(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;
        UnfreezePlayer();
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        SetGameplayInput(true);
    }

    public void GoToMainMenu()
    {
        if (SceneManager.GetActiveScene().name == "M3" && PlayerStats.HasHearts)
        {
            // Keep current heart count in M3 so going to Main Menu -> Continue doesn't refill hearts.
            SaveSystem.SaveM3Hearts(PlayerStats.Hearts);
            PlayerStats.RefillStamina();
        }
        else
        {
            PlayerStats.ResetAll();   // fresh hearts and stamina for the next run
        }

        Time.timeScale = 1f; // important, otherwise the main menu loads frozen
        SetGameplayInput(true);
        UnfreezePlayer();
        SceneManager.LoadScene(mainMenuScene);
    }

    // Fully freezes the player (movement, look, input, hotbar actions) while paused.
    private void FreezePlayer()
    {
        pausedPlayerBehaviours.Clear();

        StarterAssets.StarterAssetsInputs[] inputs =
            FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None);
        foreach (var i in inputs)
        {
            i.move = Vector2.zero;
            i.look = Vector2.zero;
            i.jump = false;
            i.sprint = false;
        }

        DisableAll<StarterAssets.ThirdPersonController>();
#if ENABLE_INPUT_SYSTEM
        DisableAll<PlayerInput>();
#endif
        DisableAll<CursorAltToggle>();
        DisableAll<M5HotbarController>();
        DisableAll<DialogueMovementLock>();
    }

    private void DisableAll<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (b != null && b.enabled)
            {
                b.enabled = false;
                pausedPlayerBehaviours.Add(b);
            }
        }
    }

    private void UnfreezePlayer()
    {
        foreach (Behaviour b in pausedPlayerBehaviours)
            if (b != null) b.enabled = true;

        pausedPlayerBehaviours.Clear();
    }
}