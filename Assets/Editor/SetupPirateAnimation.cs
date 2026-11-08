using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class SetupPirateAnimation
{
    static SetupPirateAnimation()
    {
        EditorApplication.delayCall += ApplyQuietly;
    }

    private static void ApplyQuietly()
    {
        Apply(false);
    }

    [MenuItem("Tools/Setup Pirate Idle Animation")]
    public static void ApplyMenuItem()
    {
        Apply(true);
    }

    public static void Apply(bool verbose)
    {
        GameObject pirate = GameObject.Find("Pirate");
        if (pirate == null)
        {
            if (verbose) Debug.LogWarning("[SetupPirate] Pirate GameObject not found in current scene.");
            return;
        }

        Animator animator = pirate.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(pirate);
        }

        string pirateFbxPath = "Assets/Polytope Studio/Lowpoly_Characters/AdultMan1/source/Pirate.fbx";
        Avatar pirateAvatar = null;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(pirateFbxPath))
        {
            if (obj is Avatar av)
            {
                pirateAvatar = av;
                break;
            }
        }

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Polytope Studio/Lowpoly_Characters/AdultMan1/PirateIdle.controller"
        );

        bool changed = false;

        if (pirateAvatar != null && animator.avatar != pirateAvatar)
        {
            Undo.RecordObject(animator, "Set Pirate Avatar");
            animator.avatar = pirateAvatar;
            changed = true;
            if (verbose) Debug.Log($"[SetupPirate] Set Avatar to: {pirateAvatar.name}");
        }

        if (controller != null && animator.runtimeAnimatorController != controller)
        {
            Undo.RecordObject(animator, "Set Pirate Controller");
            animator.runtimeAnimatorController = controller;
            changed = true;
            if (verbose) Debug.Log($"[SetupPirate] Set Controller to: {controller.name}");
        }

        if (animator.applyRootMotion)
        {
            Undo.RecordObject(animator, "Disable Root Motion on Pirate");
            animator.applyRootMotion = false;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(pirate);
            EditorSceneManager.MarkSceneDirty(pirate.scene);
            if (verbose) Debug.Log("[SetupPirate] Successfully updated Pirate in scene!");
        }
        else if (verbose)
        {
            Debug.Log("[SetupPirate] Pirate is already configured correctly.");
        }
    }
}

