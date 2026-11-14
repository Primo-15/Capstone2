using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Lives in the M4 scene. Call Begin() when dialogue reaches the trigger line
/// and End() when the interaction finishes.
/// It loads M5 additively, then loops through every CinematicShot (sorted by
/// "order") across both scenes until End() is called.
public class DialogueCinematic : MonoBehaviour
{
    [Header("Second scene")]
    public string secondSceneName = "M5";

    [Tooltip("M5 is moved by this amount while loaded so it doesn't overlap M4. " +
             "Use (0,0,0) if the two scenes are already far apart.")]
    public Vector3 secondSceneOffset = new Vector3(5000f, 0f, 0f);

    [Tooltip("Root objects in M5 to switch off (player, main camera, UI, quest starters...). " +
             "Names must match M5's Hierarchy. Do NOT list the object holding your M5 shots.")]
    public string[] secondSceneRootsToDisable =
    {
        "PlayerArmature", "PlayerFollowCamera", "MainCamera", "EventSystem",
        "Canvas", "Canvas (1)", "QuestAutoStart", "PauseSystem", "SceneManager"
    };

    [Header("Camera")]
    public CinemachineBrain brain;
    public int shotPriority = 30;
    public float blendTime = 1.5f;

    bool running;
    bool prepared;
    Coroutine routine;
    AsyncOperation secondLoad;
    CinemachineBlendDefinition originalBlend;

    public void Begin()
    {
        if (running) return;
        if (brain == null && Camera.main != null)
            brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            Debug.LogWarning("DialogueCinematic: no CinemachineBrain found on the main camera.");
            return;
        }

        running = true;
        prepared = false;
        originalBlend = brain.DefaultBlend;
        routine = StartCoroutine(Run());
    }

    public void End()
    {
        if (!running) return;
        running = false;
        if (routine != null) StopCoroutine(routine);
        StartCoroutine(EndRoutine());
    }

    IEnumerator Run()
    {
        var cut = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        var smooth = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);

        // Load M5 in the background; prepare it immediately the moment it finishes loading
        secondLoad = SceneManager.LoadSceneAsync(secondSceneName, LoadSceneMode.Additive);
        if (secondLoad != null)
        {
            secondLoad.completed += OnSecondLoadCompleted;
            if (secondLoad.isDone)
                EnsureSecondScenePrepared();
        }

        CinematicShot previous = null;
        int index = 0;

        while (true)
        {
            if (!prepared && secondLoad != null && secondLoad.isDone)
                EnsureSecondScenePrepared();

            List<CinematicShot> shots = CollectShots();
            if (!prepared) shots.RemoveAll(s => s.gameObject.scene.name == secondSceneName);
            if (shots.Count == 0) { yield return null; continue; }

            CinematicShot shot = shots[index % shots.Count];

            // Cut when jumping between scenes (they are far apart), blend otherwise
            bool crossScene = previous != null && previous.gameObject.scene != shot.gameObject.scene;
            brain.DefaultBlend = crossScene ? cut : smooth;

            if (previous != null) previous.cam.Priority = 0;
            shot.cam.Priority = shotPriority;

            yield return shot.Play();

            previous = shot;
            index++;
        }
    }

    void OnSecondLoadCompleted(AsyncOperation op)
    {
        EnsureSecondScenePrepared();
    }

    void EnsureSecondScenePrepared()
    {
        if (prepared) return;
        Scene scene = SceneManager.GetSceneByName(secondSceneName);
        if (!scene.isLoaded) return;

        prepared = true;
        PrepareSecondScene();
    }

    void PrepareSecondScene()
    {
        Scene scene = SceneManager.GetSceneByName(secondSceneName);
        if (!scene.isLoaded) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (Array.IndexOf(secondSceneRootsToDisable, root.name) >= 0)
            {
                root.SetActive(false);
                continue;
            }

            root.transform.position += secondSceneOffset;

            // Safety net: only the M4 camera/listener/event system/UI should be live
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var l in root.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
            foreach (var b in root.GetComponentsInChildren<CinemachineBrain>(true)) b.enabled = false;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);
            foreach (var tracker in root.GetComponentsInChildren<QuestTrackerUI>(true))
                tracker.gameObject.SetActive(false);
            foreach (var e in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                e.gameObject.SetActive(false);
        }
    }

    IEnumerator EndRoutine()
    {
        // Snap back to the player camera (no long blend from a far-away shot)
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        List<CinematicShot> shots = CollectShots();
        foreach (var s in shots) s.cam.Priority = 0;
        yield return null;

        brain.DefaultBlend = originalBlend;
        foreach (var s in shots)
            if (s.gameObject.scene == gameObject.scene) s.ResetPose();

        // Unload M5 once it has finished loading
        if (secondLoad != null)
        {
            while (!secondLoad.isDone) yield return null;
            Scene scene = SceneManager.GetSceneByName(secondSceneName);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            secondLoad = null;
        }
    }

    static List<CinematicShot> CollectShots()
    {
        var list = new List<CinematicShot>(
            FindObjectsByType<CinematicShot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        list.Sort((a, b) => a.order.CompareTo(b.order));
        return list;
    }
}