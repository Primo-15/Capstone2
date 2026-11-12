using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Button continueButton;

    private void Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (continueButton != null)
            continueButton.interactable = SaveSystem.HasSave();
    }

    public void StartGame()
    {
        // Delete the previous checkpoint.
        SaveSystem.Delete();
        PlayerStats.ResetAll();

        // Clear any progress from the previous session.
        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetProgress();

        SceneManager.LoadScene("M1");
    }

    public void ContinueGame()
    {
        SaveData data = SaveSystem.Load();

        if (data == null)
        {
            Debug.LogWarning("No save file found.");
            return;
        }

        // Always start the saved map from its beginning.
        data.completedQuestIds.Clear();
        data.hasPosition = false;

        // Preserve remaining hearts when continuing M3 so the player cannot refill hearts via Main Menu.
        if (data.sceneName == "M3")
        {
            int savedHearts = data.m3Hearts > 0 ? data.m3Hearts : PlayerStats.MaxHearts;
            PlayerStats.SetHearts(savedHearts);
        }
        else
        {
            PlayerStats.ResetAll();
        }

        SaveSystem.Pending = data;
        SaveSystem.PendingSpawn = data;

        // Restore empty quest progress.
        if (QuestManager.Instance != null)
            QuestManager.Instance.ApplyPendingSave();

        Debug.Log(
            $"Continuing from the beginning of {data.sceneName}"
        );

        SceneManager.LoadScene(data.sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit pressed");
        Application.Quit();
    }
}
