using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class SetupNPCAnimationsDirect
{
    static SetupNPCAnimationsDirect()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Run();
    }

    [MenuItem("Tools/Setup NPC Animations (Direct)")]
    public static void Run()
    {
        SetupWomanLeader();
        SetupWomanViceLeader();
        SetupFemaleNpc();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupNPCAnimations] Successfully configured animations for WomanLeader, WomanViceLeader, and FemaleNpc!");
    }

    [MenuItem("Tools/Setup Female NPC Animation")]
    public static void SetupFemaleNpc()
    {
        // 1. Ensure Female_01.fbx is configured as Humanoid
        string fbxPath = "Assets/Prefabs/Female_Starter/FBX/Characters/Female_01.fbx";
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            bool needReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                needReimport = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needReimport = true;
            }
            if (needReimport)
            {
                importer.SaveAndReimport();
            }
        }

        // 2. Find Avatar
        Avatar femaleAvatar = null;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (obj is Avatar av)
            {
                femaleAvatar = av;
                break;
            }
        }

        // Fallback to PT_Female avatar if needed
        if (femaleAvatar == null)
        {
            string ptFbx = "Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_NPC/Meshes/Peasants_Citizens/PT_Female_Modular_Free_Pack.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ptFbx))
            {
                if (obj is Avatar av)
                {
                    femaleAvatar = av;
                    break;
                }
            }
        }

        // 3. Controller
        string controllerPath = "Assets/Animations/PT_Female_Modular_Free_Pack.controller";
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

        // 4. Configure Prefab
        string prefabPath = "Assets/Prefabs/FemaleNpc.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            Animator anim = prefab.GetComponent<Animator>();
            if (anim == null) anim = prefab.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            if (femaleAvatar != null) anim.avatar = femaleAvatar;
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupNPCAnimations] FemaleNpc prefab updated with Animator!");
        }

        // 5. Configure scene instance in active scene
        GameObject npc = GameObject.Find("FemaleNpc");
        if (npc == null)
        {
            var parent = GameObject.Find("NPCs");
            if (parent != null)
            {
                var t = parent.transform.Find("FemaleNpc");
                if (t != null) npc = t.gameObject;
            }
        }

        if (npc != null)
        {
            Undo.RecordObject(npc, "Setup FemaleNpc Animator");
            Animator anim = npc.GetComponent<Animator>();
            if (anim == null) anim = Undo.AddComponent<Animator>(npc);

            anim.runtimeAnimatorController = controller;
            if (femaleAvatar != null) anim.avatar = femaleAvatar;
            anim.applyRootMotion = false;

            EditorUtility.SetDirty(anim);
            EditorUtility.SetDirty(npc);
            EditorSceneManager.MarkSceneDirty(npc.scene);
            Debug.Log("[SetupNPCAnimations] Successfully configured FemaleNpc in scene!");
        }
    }

    private static void SetupWomanLeader()
    {
        GameObject npc = GameObject.Find("WomanLeader");
        if (npc == null)
        {
            var parent = GameObject.Find("NPCs");
            if (parent != null)
            {
                var t = parent.transform.Find("WomanLeader");
                if (t != null) npc = t.gameObject;
            }
        }

        if (npc == null)
        {
            Debug.LogError("[SetupNPCAnimations] WomanLeader GameObject not found in scene!");
            return;
        }

        float baseY = npc.transform.localPosition.y;

        // 1. Create Idle Animation
        string idlePath = "Assets/Animations/WomanLeader_Idle.anim";
        AnimationClip idleClip = CreateIdleClip(baseY, 3.0f, 0.022f);
        AssetDatabase.CreateAsset(idleClip, idlePath);

        // 2. Create Talk Animation
        string talkPath = "Assets/Animations/WomanLeader_Talk.anim";
        AnimationClip talkClip = CreateTalkClip(baseY, 2.5f);
        AssetDatabase.CreateAsset(talkClip, talkPath);

        // 3. Create Animator Controller
        string controllerPath = "Assets/Animations/WomanLeader.controller";
        AnimatorController controller = CreateController(controllerPath, idleClip, talkClip);

        // 4. Attach Animator to NPC
        Animator animator = npc.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(npc);

        animator.runtimeAnimatorController = controller;
        animator.avatar = null; // Direct transform animation does not use avatar
        animator.applyRootMotion = false;

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(npc);
    }

    private static void SetupWomanViceLeader()
    {
        GameObject npc = GameObject.Find("WomanViceLeader");
        if (npc == null)
        {
            var parent = GameObject.Find("NPCs");
            if (parent != null)
            {
                var t = parent.transform.Find("WomanViceLeader");
                if (t != null) npc = t.gameObject;
            }
        }

        if (npc == null)
        {
            Debug.LogError("[SetupNPCAnimations] WomanViceLeader GameObject not found in scene!");
            return;
        }

        // Fix rotation: WomanViceLeader was placed with X=-90 (lying down).
        // Change to upright: localRotation Euler(0, 0, 0)
        Undo.RecordObject(npc.transform, "Stand WomanViceLeader Upright");
        npc.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        float baseY = npc.transform.localPosition.y;

        // 1. Create Idle Animation
        string idlePath = "Assets/Animations/WomanViceLeader_Idle.anim";
        AnimationClip idleClip = CreateIdleClip(baseY, 2.8f, 0.025f);
        AssetDatabase.CreateAsset(idleClip, idlePath);

        // 2. Create Talk Animation
        string talkPath = "Assets/Animations/WomanViceLeader_Talk.anim";
        AnimationClip talkClip = CreateTalkClip(baseY, 2.5f);
        AssetDatabase.CreateAsset(talkClip, talkPath);

        // 3. Create Animator Controller
        string controllerPath = "Assets/Animations/WomanViceLeader.controller";
        AnimatorController controller = CreateController(controllerPath, idleClip, talkClip);

        // 4. Attach Animator to NPC
        Animator animator = npc.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(npc);

        animator.runtimeAnimatorController = controller;
        animator.avatar = null;
        animator.applyRootMotion = false;

        EditorUtility.SetDirty(npc.transform);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(npc);
    }

    private static AnimationClip CreateIdleClip(float baseY, float duration, float bobHeight)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Idle";

        // Vertical breathing bob on Y
        Keyframe[] yKeys = new Keyframe[]
        {
            new Keyframe(0f, baseY, 0f, 0.04f),
            new Keyframe(duration * 0.35f, baseY + bobHeight, 0f, 0f),
            new Keyframe(duration * 0.7f, baseY + bobHeight * 0.4f, 0f, 0f),
            new Keyframe(duration, baseY, -0.04f, 0f)
        };
        AnimationCurve yCurve = new AnimationCurve(yKeys);
        for (int i = 0; i < yKeys.Length; i++)
        {
            AnimationUtility.SetKeyBroken(yCurve, i, false);
        }
        clip.SetCurve("", typeof(Transform), "localPosition.y", yCurve);

        // Subtle breathing pitch on X (nodding gently with breath)
        Keyframe[] pitchKeys = new Keyframe[]
        {
            new Keyframe(0f, 0f, 0f, 1.5f),
            new Keyframe(duration * 0.35f, 1.2f, 0f, 0f),
            new Keyframe(duration * 0.7f, 0.5f, 0f, 0f),
            new Keyframe(duration, 0f, -1.5f, 0f)
        };
        AnimationCurve pitchCurve = new AnimationCurve(pitchKeys);
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", pitchCurve);

        // Subtle weight-shifting roll on Z
        Keyframe[] rollKeys = new Keyframe[]
        {
            new Keyframe(0f, 0f, 0f, 1.2f),
            new Keyframe(duration * 0.25f, 0.8f, 0f, 0f),
            new Keyframe(duration * 0.5f, 0f, -1.2f, -1.2f),
            new Keyframe(duration * 0.75f, -0.8f, 0f, 0f),
            new Keyframe(duration, 0f, 1.2f, 0f)
        };
        AnimationCurve rollCurve = new AnimationCurve(rollKeys);
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", rollCurve);

        // Mark as looping
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        return clip;
    }

    private static AnimationClip CreateTalkClip(float baseY, float duration)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Talk";

        // Conversational bobbing on Y
        Keyframe[] yKeys = new Keyframe[]
        {
            new Keyframe(0f, baseY, 0f, 0.05f),
            new Keyframe(0.3f, baseY + 0.035f, 0f, 0f),
            new Keyframe(0.6f, baseY + 0.01f, 0f, 0f),
            new Keyframe(1.0f, baseY + 0.03f, 0f, 0f),
            new Keyframe(1.4f, baseY + 0.005f, 0f, 0f),
            new Keyframe(1.8f, baseY + 0.025f, 0f, 0f),
            new Keyframe(duration, baseY, -0.04f, 0f)
        };
        AnimationCurve yCurve = new AnimationCurve(yKeys);
        clip.SetCurve("", typeof(Transform), "localPosition.y", yCurve);

        // Conversational nodding on X (head/body nods to accentuate words)
        Keyframe[] pitchKeys = new Keyframe[]
        {
            new Keyframe(0f, 0f, 0f, 8f),
            new Keyframe(0.25f, 3.5f, 0f, 0f),    // Initial nod
            new Keyframe(0.55f, -0.5f, 0f, 0f),   // Rebound
            new Keyframe(0.95f, 3.0f, 0f, 0f),    // Second conversational nod
            new Keyframe(1.35f, 0.2f, 0f, 0f),
            new Keyframe(1.75f, 2.0f, 0f, 0f),    // Emphasis nod
            new Keyframe(duration, 0f, -4f, 0f)   // Settles back cleanly to 0
        };
        AnimationCurve pitchCurve = new AnimationCurve(pitchKeys);
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", pitchCurve);

        // Conversational gestures/tilt on Z
        Keyframe[] rollKeys = new Keyframe[]
        {
            new Keyframe(0f, 0f, 0f, -3f),
            new Keyframe(0.4f, -1.8f, 0f, 0f),
            new Keyframe(1.1f, 1.5f, 0f, 0f),
            new Keyframe(1.7f, -1.0f, 0f, 0f),
            new Keyframe(duration, 0f, 1.5f, 0f)
        };
        AnimationCurve rollCurve = new AnimationCurve(rollKeys);
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", rollCurve);

        // Not looping (plays once on Talk trigger and returns to Idle)
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        return clip;
    }

    private static AnimatorController CreateController(string path, AnimationClip idleClip, AnimationClip talkClip)
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Add "Talk" trigger parameter
        controller.AddParameter("Talk", AnimatorControllerParameterType.Trigger);

        var rootStateMachine = controller.layers[0].stateMachine;

        // Add states
        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;

        var talkState = rootStateMachine.AddState("Talk");
        talkState.motion = talkClip;

        rootStateMachine.defaultState = idleState;

        // Transition: Idle -> Talk (triggered by "Talk")
        var toTalk = idleState.AddTransition(talkState);
        toTalk.AddCondition(AnimatorConditionMode.If, 0, "Talk");
        toTalk.hasExitTime = false;
        toTalk.duration = 0.15f;

        // Transition: Talk -> Idle (returns automatically when speech finishes)
        var toIdle = talkState.AddTransition(idleState);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 0.9f;
        toIdle.duration = 0.25f;

        AssetDatabase.SaveAssets();
        return controller;
    }
}
