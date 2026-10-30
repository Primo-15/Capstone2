using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string sceneName;                       // scene to load on Continue
    public List<string> completedQuestIds = new(); // completed quests to restore

    public bool hasPosition;                       // true once a position was saved
    public Vector3 playerPosition;
    public float playerYRotation;
}

public static class SaveSystem
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    // Quest progress to restore (consumed by QuestManager)
    public static SaveData Pending;

    // Player position to restore (consumed by PlayerSpawnLoader)
    public static SaveData PendingSpawn;

    public static bool HasSave() => File.Exists(FilePath);

    public static void Save(SaveData data)
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        Debug.Log("Autosaved to " + FilePath);
    }

    public static SaveData Load()
    {
        if (!HasSave()) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
    }

    public static void Delete()
    {
        if (HasSave()) File.Delete(FilePath);
        Pending = null;
        PendingSpawn = null;
    }
}