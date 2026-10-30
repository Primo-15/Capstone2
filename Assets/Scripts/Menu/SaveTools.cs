#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SaveTools
{
    [MenuItem("Tools/Save/Delete Save File")]
    private static void DeleteSaveFile()
    {
        string path = Path.Combine(Application.persistentDataPath, "save.json");

        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Save file deleted: " + path);
        }
        else
        {
            Debug.Log("No save file found at: " + path);
        }
    }
}
#endif