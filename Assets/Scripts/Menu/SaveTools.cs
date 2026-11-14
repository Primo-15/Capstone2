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

    [MenuItem("Tools/Save/Reset Main Menu Confirm Preference")]
    private static void ResetMainMenuConfirmPreference()
    {
        MainMenuConfirm.SetNeverShowAgainEnabled(false);
        Debug.Log("Main Menu confirmation 'Never show this again' preference reset.");
    }

    [MenuItem("Tools/Save/Reset Player Settings")]
    private static void ResetPlayerSettings()
    {
        PlayerPrefs.DeleteKey(MenuSettings.BgMusicPrefKey);
        PlayerPrefs.DeleteKey(MenuSettings.SfxPrefKey);
        PlayerPrefs.DeleteKey(MenuSettings.MouseSensitivityPrefKey);
        PlayerPrefs.Save();
        MenuSettings.LoadSavedSettings();
        MenuSettings.ApplyAllSettings();
        Debug.Log("Player Settings reset to defaults.");
    }

    [MenuItem("Tools/Save/Unlock Map Selection (Mark Game Completed)")]
    private static void UnlockMapSelection()
    {
        SaveSystem.SetGameCompleted(true);
        Debug.Log("Map Selection unlocked (Game Completed = true).");
    }

    [MenuItem("Tools/Save/Lock Map Selection (Reset Game Completed)")]
    private static void LockMapSelection()
    {
        SaveSystem.SetGameCompleted(false);
        Debug.Log("Map Selection locked (Game Completed = false).");
    }
}
#endif