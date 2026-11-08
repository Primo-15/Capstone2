using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets;

public class M2OpeningCinematic : MonoBehaviour
{
    [Header("Camera Views (Auto-found from CameraViews if empty)")]
    [SerializeField] private Transform view1;
    [SerializeField] private Transform view2;

    [Header("Targets (Auto-found if empty)")]
    [SerializeField] private Transform infantry1;
    [SerializeField] private Transform infantry2;
    [SerializeField] private Transform inayTarget;
    [SerializeField] private Transform soldiersRoot;

    [Header("Target Local Positions (under Soldiers root)")]
    [SerializeField] private Vector3 inf1TargetLocal = new Vector3(-4.07f, 0f, -0.48f);
    [SerializeField] private Vector3 inf2TargetLocal = new Vector3(-1.99f, 0f, 1.34f);

    [Header("Timings & Speeds")]
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float view1PanDuration = 4.0f;
    [SerializeField] private float infantryWalkSpeed = 1.4f;
    [SerializeField] private float view2HoldAfterArrival = 2.0f;
    [SerializeField] private bool allowSkip = true;

    [Header("Debug")]
    [SerializeField] private bool hasCompleted = false;

    private Camera cam;
    private Behaviour cinemachineBrain;
    private ThirdPersonController playerController;
    private StarterAssetsInputs playerInputs;

    private CanvasGroup fadeCanvasGroup;
    private GameObject fadeCanvasObj;
    private bool isSkipped = false;
    private Coroutine cinematicCoroutine;

    // Final-pose hold (keeps soldiers in place once they arrive, even if another script tries to move them)
    private bool hold1, hold2;
    private Vector3 holdPos1, holdPos2;
    private Quaternion holdRot1, holdRot2;

    // NOTE: The old [RuntimeInitializeOnLoadMethod] auto-bootstrapper was removed.
    // Add this component to an empty GameObject in the M2 scene instead.

    private void Awake()
    {
        FindSceneReferences();
        CreateFadeCanvas();
    }

    private void Start()
    {
        cinematicCoroutine = StartCoroutine(PlayCinematicSequence());
    }

    private void Update()
    {
        if (allowSkip && !hasCompleted && !isSkipped)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
            {
                isSkipped = true;
                if (cinematicCoroutine != null)
                {
                    StopCoroutine(cinematicCoroutine);
                }
                StartCoroutine(SkipCinematicSequence());
            }
        }
    }

    private void LateUpdate()
    {
        HoldSoldier(infantry1, hold1, holdPos1, ref holdRot1);
        HoldSoldier(infantry2, hold2, holdPos2, ref holdRot2);
    }

    private void HoldSoldier(Transform s, bool hold, Vector3 pos, ref Quaternion rot)
    {
        if (!hold || s == null) return;

        // Debug: tells us if something else moved the soldier after he arrived
        if ((s.position - pos).sqrMagnitude > 0.0025f)
            Debug.LogWarning($"{s.name} was moved off its final spot to {s.position} (frame {Time.frameCount})", s);

        if (inayTarget != null)
        {
            Vector3 d = inayTarget.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f)
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation(d.normalized, Vector3.up), Time.deltaTime * 7f);
        }

        s.SetPositionAndRotation(pos, rot);
    }

    private void FindSceneReferences()
    {
        // 1. Camera
        if (cam == null)
            cam = Camera.main;

        if (cam != null)
        {
            foreach (Behaviour b in cam.GetComponents<Behaviour>())
            {
                if (b != null && b.GetType().Name == "CinemachineBrain")
                {
                    cinemachineBrain = b;
                    break;
                }
            }
        }

        // 2. CameraViews
        if (view1 == null || view2 == null)
        {
            GameObject cv = GameObject.Find("CameraViews");
            if (cv == null)
            {
                // Try finding any root with CameraViews
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name.IndexOf("CameraView", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cv = root;
                        break;
                    }
                }
            }

            if (cv != null)
            {
                for (int i = 0; i < cv.transform.childCount; i++)
                {
                    Transform child = cv.transform.GetChild(i);
                    string lower = child.name.ToLower().Replace(" ", "").Replace("_", "");
                    if (view1 == null && (lower.Contains("view1") || (i == 0 && cv.transform.childCount >= 2)))
                    {
                        view1 = child;
                    }
                    else if (view2 == null && (lower.Contains("view2") || (i == 1 && cv.transform.childCount >= 2)))
                    {
                        view2 = child;
                    }
                }

                if (view1 == null && cv.transform.childCount > 0)
                    view1 = cv.transform.GetChild(0);
                if (view2 == null && cv.transform.childCount > 1)
                    view2 = cv.transform.GetChild(1);
            }
        }

        // Fallbacks if CameraViews is not in scene
        if (view1 == null)
        {
            GameObject fallback1 = new GameObject("Fallback_View1");
            fallback1.transform.position = new Vector3(32f, 10f, 25f);
            fallback1.transform.rotation = Quaternion.Euler(18f, -145f, 0f);
            view1 = fallback1.transform;
        }

        if (view2 == null)
        {
            GameObject fallback2 = new GameObject("Fallback_View2");
            fallback2.transform.position = new Vector3(18f, 2.8f, 14f);
            fallback2.transform.rotation = Quaternion.Euler(12f, -80f, 0f);
            view2 = fallback2.transform;
        }

        // 3. Soldiers root & Infantries
        if (soldiersRoot == null)
        {
            GameObject sObj = GameObject.Find("Soldiers");
            if (sObj != null) soldiersRoot = sObj.transform;
        }

        if (infantry1 == null)
        {
            if (soldiersRoot != null)
                infantry1 = soldiersRoot.Find("Infantry1");
            if (infantry1 == null)
            {
                GameObject i1 = GameObject.Find("Infantry1");
                if (i1 != null) infantry1 = i1.transform;
            }
        }

        if (infantry2 == null)
        {
            if (soldiersRoot != null)
                infantry2 = soldiersRoot.Find("Infantry1 (1)");
            if (infantry2 == null)
            {
                GameObject i2 = GameObject.Find("Infantry1 (1)");
                if (i2 != null) infantry2 = i2.transform;
            }
        }

        // 4. Inay
        if (inayTarget == null)
        {
            GameObject inayObj = GameObject.Find("Inay");
            if (inayObj != null)
                inayTarget = inayObj.transform;
        }

        // 5. Player
        playerController = FindFirstObjectByType<ThirdPersonController>();
        if (playerController != null)
        {
            playerInputs = playerController.GetComponent<StarterAssetsInputs>();
        }
    }

    private void CreateFadeCanvas()
    {
        fadeCanvasObj = new GameObject("M2Cinematic_FadeCanvas");
        Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        fadeCanvasObj.AddComponent<CanvasScaler>();
        fadeCanvasObj.AddComponent<GraphicRaycaster>();

        fadeCanvasGroup = fadeCanvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 1f; // Start fully black
        fadeCanvasGroup.blocksRaycasts = true;

        GameObject blackImgObj = new GameObject("BlackImage");
        blackImgObj.transform.SetParent(fadeCanvasObj.transform, false);

        Image img = blackImgObj.AddComponent<Image>();
        img.color = Color.black;

        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private IEnumerator PlayCinematicSequence()
    {
        // 1. Prepare player and infantries
        LockPlayer(true);
        PrepareInfantries();

        if (cinemachineBrain != null)
            cinemachineBrain.enabled = false;

        // Snap camera to View 1 initially
        if (cam != null && view1 != null)
        {
            cam.transform.SetPositionAndRotation(view1.position, view1.rotation);
        }

        // Small initial pause to let scene settle
        yield return new WaitForSeconds(0.2f);

        // ==========================================
        // VIEW 1: Village Scenic Camera View
        // ==========================================
        // Fade in View 1
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        // Pan camera slowly across View 1
        Vector3 v1StartPos = view1.position;
        Quaternion v1StartRot = view1.rotation;
        Vector3 v1EndPos = v1StartPos + view1.right * 2.5f + view1.forward * 0.5f;
        Quaternion v1EndRot = v1StartRot * Quaternion.Euler(0f, 3f, 0f);

        float elapsed = 0f;
        float panTime = Mathf.Max(0.5f, view1PanDuration);

        while (elapsed < panTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / panTime);
            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(v1StartPos, v1EndPos, t);
                cam.transform.rotation = Quaternion.Slerp(v1StartRot, v1EndRot, t);
            }
            yield return null;
        }

        // Fade out View 1
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        // ==========================================
        // VIEW 2: Infantries walking to Mother Inay
        // ==========================================
        // Snap camera to View 2 while screen is black
        if (cam != null && view2 != null)
        {
            cam.transform.SetPositionAndRotation(view2.position, view2.rotation);
        }

        // Fade in View 2
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        // Start soldier walk sequence in View 2
        yield return StartCoroutine(AnimateSoldiersAndCameraView2());

        // Hold shot on confrontation with Inay
        yield return new WaitForSeconds(view2HoldAfterArrival);

        // Fade out View 2
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        // ==========================================
        // RESTORE GAMEPLAY
        // ==========================================
        EndCinematic();

        // Fade in to gameplay
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        // Clean up fade canvas
        if (fadeCanvasObj != null)
        {
            Destroy(fadeCanvasObj);
        }
    }

    private IEnumerator AnimateSoldiersAndCameraView2()
    {
        // Calculate destination world coordinates
        Vector3 targetWorld1 = infantry1 != null ? GetTargetWorldPosition(infantry1, inf1TargetLocal) : Vector3.zero;
        Vector3 targetWorld2 = infantry2 != null ? GetTargetWorldPosition(infantry2, inf2TargetLocal) : Vector3.zero;

        Animator anim1 = infantry1 != null ? infantry1.GetComponentInChildren<Animator>() : null;
        Animator anim2 = infantry2 != null ? infantry2.GetComponentInChildren<Animator>() : null;

        if (anim1 != null)
        {
            anim1.applyRootMotion = false;
            anim1.SetBool("IsMoving", true);
        }
        if (anim2 != null)
        {
            anim2.applyRootMotion = false;
            anim2.SetBool("IsMoving", true);
        }

        Vector3 camStartPos = view2 != null ? view2.position : cam.transform.position;
        Quaternion camStartRot = view2 != null ? view2.rotation : cam.transform.rotation;
        Vector3 camEndPos = camStartPos + (view2 != null ? (view2.forward * 0.9f + view2.up * 0.15f) : Vector3.zero);

        bool inf1Arrived = (infantry1 == null);
        bool inf2Arrived = (infantry2 == null);
        float timeWalking = 0f;

        while (!inf1Arrived || !inf2Arrived)
        {
            timeWalking += Time.deltaTime;

            // Camera subtle cinematic push
            if (cam != null)
            {
                float camT = Mathf.Clamp01(timeWalking / 6f);
                float smoothCamT = Mathf.SmoothStep(0f, 1f, camT);
                cam.transform.position = Vector3.Lerp(camStartPos, camEndPos, smoothCamT);
                cam.transform.rotation = camStartRot;
            }

            // Move Infantry 1
            if (!inf1Arrived && infantry1 != null)
            {
                Vector3 toTarget = targetWorld1 - infantry1.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist > 0.08f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    infantry1.rotation = Quaternion.Slerp(infantry1.rotation, targetRot, Time.deltaTime * 6f);
                    infantry1.position = Vector3.MoveTowards(infantry1.position, targetWorld1, infantryWalkSpeed * Time.deltaTime);
                }
                else
                {
                    infantry1.position = targetWorld1;
                    inf1Arrived = true;
                    holdPos1 = targetWorld1; holdRot1 = infantry1.rotation; hold1 = true;
                    if (anim1 != null) anim1.SetBool("IsMoving", false);
                }
            }

            // Move Infantry 2
            if (!inf2Arrived && infantry2 != null)
            {
                Vector3 toTarget = targetWorld2 - infantry2.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist > 0.08f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    infantry2.rotation = Quaternion.Slerp(infantry2.rotation, targetRot, Time.deltaTime * 6f);
                    infantry2.position = Vector3.MoveTowards(infantry2.position, targetWorld2, infantryWalkSpeed * Time.deltaTime);
                }
                else
                {
                    infantry2.position = targetWorld2;
                    inf2Arrived = true;
                    holdPos2 = targetWorld2; holdRot2 = infantry2.rotation; hold2 = true;
                    if (anim2 != null) anim2.SetBool("IsMoving", false);
                }
            }

            // Continuously face Inay once arrived
            FaceTarget(infantry1, inf1Arrived, inayTarget);
            FaceTarget(infantry2, inf2Arrived, inayTarget);

            yield return null;
        }

        // Both arrived, finalize facing Inay
        float settleTimer = 0f;
        while (settleTimer < 0.6f)
        {
            settleTimer += Time.deltaTime;
            FaceTarget(infantry1, true, inayTarget);
            FaceTarget(infantry2, true, inayTarget);
            yield return null;
        }
    }

    private void FaceTarget(Transform soldier, bool hasArrived, Transform target)
    {
        if (!hasArrived || soldier == null || target == null)
            return;

        Vector3 dir = target.position - soldier.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            soldier.rotation = Quaternion.Slerp(soldier.rotation, targetRot, Time.deltaTime * 7f);
        }
    }

    private Vector3 GetTargetWorldPosition(Transform soldier, Vector3 targetLocalXZ)
    {
        Transform parent = soldier.parent != null ? soldier.parent : soldiersRoot;
        float currentLocalY = soldier.localPosition.y;
        Vector3 localPos = new Vector3(targetLocalXZ.x, currentLocalY, targetLocalXZ.z);

        if (parent != null)
        {
            return parent.TransformPoint(localPos);
        }

        return new Vector3(targetLocalXZ.x, soldier.position.y, targetLocalXZ.z);
    }

    private void PrepareInfantries()
    {
        // Disable GuardPatrol and NavMeshAgent without modifying GuardPatrol.cs
        DisablePatrolAndAgent(infantry1);
        DisablePatrolAndAgent(infantry2);
    }

    private void DisablePatrolAndAgent(Transform soldier)
    {
        if (soldier == null) return;

        foreach (MonoBehaviour mb in soldier.GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb.GetType().Name == "GuardPatrol")
            {
                mb.enabled = false;
            }
        }

        var agent = soldier.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    private void LockPlayer(bool locked)
    {
        if (playerController != null)
        {
            playerController.enabled = !locked;
        }

        if (playerInputs != null)
        {
            playerInputs.move = Vector2.zero;
            playerInputs.look = Vector2.zero;
            playerInputs.jump = false;
            playerInputs.sprint = false;
        }
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null)
            yield break;

        fadeCanvasGroup.blocksRaycasts = (to > 0.01f || from > 0.01f);
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, duration);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / dur));
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = (to > 0.01f);
    }

    private IEnumerator SkipCinematicSequence()
    {
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeRoutine(fadeCanvasGroup.alpha, 1f, 0.3f));
        }

        // Snap infantries to target positions
        SnapSoldiersToFinal();

        EndCinematic();

        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeRoutine(1f, 0f, 0.5f));
            if (fadeCanvasObj != null)
            {
                Destroy(fadeCanvasObj);
            }
        }
    }

    private void SnapSoldiersToFinal()
    {
        if (infantry1 != null)
        {
            infantry1.position = GetTargetWorldPosition(infantry1, inf1TargetLocal);
            Animator a1 = infantry1.GetComponentInChildren<Animator>();
            if (a1 != null) a1.SetBool("IsMoving", false);
            if (inayTarget != null)
            {
                Vector3 d = inayTarget.position - infantry1.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.001f) infantry1.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
        }

        if (infantry2 != null)
        {
            infantry2.position = GetTargetWorldPosition(infantry2, inf2TargetLocal);
            Animator a2 = infantry2.GetComponentInChildren<Animator>();
            if (a2 != null) a2.SetBool("IsMoving", false);
            if (inayTarget != null)
            {
                Vector3 d = inayTarget.position - infantry2.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.001f) infantry2.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
        }
    }

    private void EndCinematic()
    {
        hasCompleted = true;
        hold1 = false;
        hold2 = false;

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;
        }

        LockPlayer(false);
    }

    private void OnDisable()
    {
        if (!hasCompleted)
        {
            EndCinematic();
        }
    }
}