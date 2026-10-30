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

        // Continue is only clickable if a save exists
        continueButton.interactable = SaveSystem.HasSave();
    }

    public void StartGame()
    {
        SaveSystem.Delete();   // wipe old save file and any pending save

        // Wipe quest progress that survived in DontDestroyOnLoad
        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetProgress();

        SceneManager.LoadScene("M1");
    }

    public void ContinueGame()
    {
        SaveData data = SaveSystem.Load();
        if (data == null) return;

        SaveSystem.Pending = data;
        SaveSystem.PendingSpawn = data;   // player position to restore after the scene loads

        // If the QuestManager survived from earlier in this run,
        // restore into it now. Otherwise the new one created in the
        // next scene restores itself in Awake.
        if (QuestManager.Instance != null)
            QuestManager.Instance.ApplyPendingSave();

        SceneManager.LoadScene(data.sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit pressed");
        Application.Quit();
    }
}