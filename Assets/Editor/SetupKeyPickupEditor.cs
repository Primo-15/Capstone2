using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class SetupKeyPickupEditor
{
    static SetupKeyPickupEditor()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Setup();
    }

    [MenuItem("Tools/Setup Key Pickup in M5")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "M5") return;

        GameObject keyObj = GameObject.Find("key");
        if (keyObj == null)
        {
            var npcs = GameObject.Find("NPCs");
            if (npcs != null)
            {
                var t = npcs.transform.Find("key");
                if (t != null) keyObj = t.gameObject;
            }
        }

        if (keyObj == null)
        {
            Debug.LogWarning("[SetupKeyPickup] 'key' GameObject not found in current scene.");
            return;
        }

        // Remove QuestNPCInteraction if present
        var old = keyObj.GetComponent<QuestNPCInteraction>();
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        var pickup = keyObj.GetComponent<KeyPickup>();
        if (pickup == null)
        {
            pickup = Undo.AddComponent<KeyPickup>(keyObj);
        }

        EditorUtility.SetDirty(keyObj);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[SetupKeyPickup] Successfully configured KeyPickup on 'key' in M5!");
    }
}

