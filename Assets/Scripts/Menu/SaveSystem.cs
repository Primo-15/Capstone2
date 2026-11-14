using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string sceneName;
    public List<string> completedQuestIds = new List<string>();

    public bool hasPosition;
    public Vector3 playerPosition;
    public float playerYRotation;

    // Persists remaining hearts in M3 so Main Menu -> Continue does not refill lost hearts.
    public int m3Hearts = PlayerStats.MaxHearts;
}

public static class SaveSystem
{
    public const string GameCompletedPrefKey = "GameCompleted";

    private static string FilePath =>
        Path.Combine(
            Application.persistentDataPath,
            "save.json"
        );

    // Chapter Replay Mode flag (true only when replaying a chapter from Chapter Selection)
    public static bool IsChapterReplayMode { get; set; } = false;

    // Quest progress to restore.
    public static SaveData Pending;

    // Player position to restore.
    public static SaveData PendingSpawn;

    // Tracks a legitimate map transition.
    public static bool PendingMapTransition;

    public static string PendingMapScene;

    public static bool HasSave()
    {
        return File.Exists(FilePath);
    }

    public static bool IsGameCompleted()
    {
        return PlayerPrefs.GetInt(GameCompletedPrefKey, 0) == 1;
    }

    public static void SetGameCompleted(bool completed = true)
    {
        if (completed)
        {
            PlayerPrefs.SetInt(GameCompletedPrefKey, 1);
        }
        else
        {
            PlayerPrefs.DeleteKey(GameCompletedPrefKey);
        }

        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Game completion status set to: {completed}");
    }

    public static void DeleteAllGameplaySaveData()
    {
        Delete();
        IsChapterReplayMode = false;

        PlayerPrefs.DeleteKey(GameCompletedPrefKey);
        MainMenuConfirm.SetNeverShowAgainEnabled(false);
        PlayerPrefs.Save();

        PlayerStats.ResetAll();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetProgress();
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearInventory();
        }

        Debug.Log("[SaveSystem] All gameplay save data deleted (Settings preferences preserved).");
    }

    public static void Save(SaveData data)
    {
        File.WriteAllText(
            FilePath,
            JsonUtility.ToJson(data, true)
        );

        Debug.Log("Saved to " + FilePath);
    }

    // Save ONLY when advancing to another map (or returning to M2 after losing all hearts in M3).
    public static void SaveMapCheckpoint(string nextScene)
    {
        if (string.IsNullOrWhiteSpace(nextScene))
        {
            Debug.LogError("Cannot save an empty map name.");
            return;
        }

        SaveData data = new SaveData();

        data.sceneName = nextScene;

        // Never carry previous map quest progress.
        data.completedQuestIds =
            new List<string>();

        // Always use the new map's default spawn.
        data.hasPosition = false;
        data.m3Hearts = PlayerStats.MaxHearts;

        Save(data);

        PendingMapScene = nextScene;
        PendingMapTransition = true;

        // Fresh checkpoint into M3 starts with full hearts.
        if (nextScene == "M3")
        {
            PlayerStats.ResetAll();
        }

        Debug.Log(
            $"Map checkpoint saved: {nextScene}"
        );
    }

    // Saves the remaining heart count while in M3 so Continue keeps the exact heart count.
    public static void SaveM3Hearts(int hearts)
    {
        SaveData data = Load() ?? new SaveData();
        data.sceneName = "M3";
        data.completedQuestIds = new List<string>();
        data.hasPosition = false;
        data.m3Hearts = Mathf.Clamp(hearts, 1, PlayerStats.MaxHearts);
        Save(data);
    }

    public static void ClearPendingMapTransition()
    {
        PendingMapTransition = false;
        PendingMapScene = null;
    }

    public static SaveData Load()
    {
        if (!HasSave())
            return null;

        return JsonUtility.FromJson<SaveData>(
            File.ReadAllText(FilePath)
        );
    }

    public static void Delete()
    {
        if (HasSave())
            File.Delete(FilePath);

        Pending = null;
        PendingSpawn = null;

        ClearPendingMapTransition();
    }
}
