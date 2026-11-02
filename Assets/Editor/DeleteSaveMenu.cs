#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Put this file in a folder named "Editor" inside Assets.
// Adds Tools > Delete Save File to the Unity menu bar.
public static class DeleteSaveMenu
{
    [MenuItem("Tools/Delete Save File")]
    private static void DeleteSave()
    {
        SaveSystem.Delete();
        Debug.Log("Save file deleted.");
    }
}
#endif