using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StarterAssets;

public class M2OpeningCinematic : MonoBehaviour
{
    // One entry per soldier. Drag your own empty GameObjects into Start Point / End Point.
    [Serializable]
    public class SoldierActor
    {
        [Tooltip("The soldier object. Auto-found by name if left empty.")]
        public Transform soldier;
        [Tooltip("Where the soldier begins. If empty, the soldier's current position is used.")]
        public Transform startPoint;
        [Tooltip("Where the soldier walks to (and stands at the end).")]
        public Transform endPoint;

        [NonSerialized] public Animator anim;
        [NonSerialized] public Vector3 pos;
        [NonSerialized] public Quaternion rot;
        [NonSerialized] public bool controlled;
        [NonSerialized] public bool arrived;
    }

    [Header("Camera Views (Auto-found from CameraViews if empty)")]
    [SerializeField] private Transform view1;
    [SerializeField] private Transform view2;

    [Header("Soldiers (drag your own Start / End points here)")]
    [SerializeField] private SoldierActor soldier1 = new SoldierActor();
    [SerializeField] private SoldierActor soldier2 = new SoldierActor();
    [SerializeField] private Transform inayTarget;

    [Header("Soldier Behaviour")]
    [Tooltip("ON: soldiers turn to face Inay at the end. OFF: they use the End Point's rotation.")]
    [SerializeField] private bool faceInayAtEnd = true;
    [Tooltip("ON: soldiers stay exactly at their End Point after the cinematic (nothing can move them).")]
    [SerializeField] private bool keepSoldiersAtEndAfterCinematic = true;
    [Tooltip("Name of the bool parameter in the soldiers' Animator.")]
    [SerializeField] private string movingParameter = "IsMoving";

    [Header("Timings & Speeds")]
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float view1PanDuration = 4.0f;
    [SerializeField] private float infantryWalkSpeed = 1.4f;
    [SerializeField] private float view2HoldAfterArrival = 2.0f;
    [SerializeField] private bool allowSkip = true;

    private Camera cam;
    private Behaviour cinemachineBrain;
    private ThirdPersonController playerController;
    private StarterAssetsInputs playerInputs;

    private CanvasGroup fadeCanvasGroup;
    private GameObject fadeCanvasObj;
    private bool hasCompleted = false;
    private bool isSkipped = false;
    private Coroutine cinematicCoroutine;

    // Add this component to an empty GameObject in the M2 scene.

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
                StopAllCoroutines(); // also stops the nested walk/fade coroutines
                StartCoroutine(SkipCinematicSequence());
            }
        }
    }

    // Runs after the Animator and every other script, so the soldiers always end up exactly where we put them.
    private void LateUpdate()
    {
        ApplyActor(soldier1);
        ApplyActor(soldier2);
    }

    private void ApplyActor(SoldierActor a)
    {
        if (a == null || !a.controlled || a.soldier == null) return;
        a.soldier.SetPositionAndRotation(a.pos, a.rot);
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------
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

        // Fallbacks if CameraViews is not in the scene
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

        // 3. Soldiers
        if (soldier1.soldier == null) soldier1.soldier = FindTransformByName("Infantry1");
        if (soldier2.soldier == null) soldier2.soldier = FindTransformByName("Infantry1 (1)");

        if (soldier1.soldier != null) soldier1.anim = soldier1.soldier.GetComponentInChildren<Animator>();
        if (soldier2.soldier != null) soldier2.anim = soldier2.soldier.GetComponentInChildren<Animator>();

        if (soldier1.soldier != null && soldier1.endPoint == null)
            Debug.LogWarning("M2OpeningCinematic: Soldier 1 has no End Point assigned, so he will not walk.", this);
        if (soldier2.soldier != null && soldier2.endPoint == null)
            Debug.LogWarning("M2OpeningCinematic: Soldier 2 has no End Point assigned, so he will not walk.", this);

        // 4. Inay
        if (inayTarget == null)
            inayTarget = FindTransformByName("Inay");

        // 5. Player
        playerController = FindFirstObjectByType<ThirdPersonController>();
        if (playerController != null)
        {
            playerInputs = playerController.GetComponent<StarterAssetsInputs>();
        }
    }

    private Transform FindTransformByName(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
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

    // ------------------------------------------------------------------
    // Main sequence
    // ------------------------------------------------------------------
    private IEnumerator PlayCinematicSequence()
    {
        // 1. Prepare player and soldiers
        LockPlayer(true);
        PrepareActor(soldier1);
        PrepareActor(soldier2);

        if (cinemachineBrain != null)
            cinemachineBrain.enabled = false;

        // Snap camera to View 1 initially
        if (cam != null && view1 != null)
        {
            cam.transform.SetPositionAndRotation(view1.position, view1.rotation);
        }

        // Small initial pause to let the scene settle
        yield return new WaitForSeconds(0.2f);

        // ==========================================
        // VIEW 1: Village scenic camera view
        // ==========================================
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

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

        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        // ==========================================
        // VIEW 2: Soldiers walk to Mother Inay
        // ==========================================
        if (cam != null && view2 != null)
        {
            cam.transform.SetPositionAndRotation(view2.position, view2.rotation);
        }

        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        yield return StartCoroutine(WalkSoldiersAndPushCamera());

        // Hold shot on the confrontation with Inay
        yield return new WaitForSeconds(view2HoldAfterArrival);

        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        // ==========================================
        // RESTORE GAMEPLAY
        // ==========================================
        EndCinematic();

        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        if (fadeCanvasObj != null)
        {
            Destroy(fadeCanvasObj);
        }
    }

    // ------------------------------------------------------------------
    // Soldier movement
    // ------------------------------------------------------------------
    private bool HasWalk(SoldierActor a)
    {
        return a != null && a.soldier != null && a.endPoint != null;
    }

    private void PrepareActor(SoldierActor a)
    {
        if (a == null || a.soldier == null) return;

        // Stop anything that could steer the soldier on its own
        foreach (var agent in a.soldier.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            agent.enabled = false;

        foreach (var mb in a.soldier.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null && mb.GetType().Name == "GuardPatrol")
            {
                mb.StopAllCoroutines();
                mb.enabled = false;
            }
        }

        if (a.anim != null)
            a.anim.applyRootMotion = false;

        // Start pose
        a.pos = a.soldier.position;
        a.rot = a.soldier.rotation;
        if (a.startPoint != null)
        {
            a.pos = a.startPoint.position;
            a.rot = a.startPoint.rotation;
        }

        a.soldier.SetPositionAndRotation(a.pos, a.rot);
        a.arrived = !HasWalk(a);
        a.controlled = true; // from now on LateUpdate keeps this soldier exactly where we say
    }

    private IEnumerator WalkSoldiersAndPushCamera()
    {
        SetMoving(soldier1, HasWalk(soldier1));
        SetMoving(soldier2, HasWalk(soldier2));

        Vector3 camStartPos = cam != null ? cam.transform.position : Vector3.zero;
        Quaternion camStartRot = cam != null ? cam.transform.rotation : Quaternion.identity;
        Vector3 camEndPos = camStartPos;
        if (view2 != null)
        {
            camStartPos = view2.position;
            camStartRot = view2.rotation;
            camEndPos = camStartPos + (view2.forward * 0.9f + view2.up * 0.15f);
        }

        float timeWalking = 0f;

        while (!soldier1.arrived || !soldier2.arrived)
        {
            timeWalking += Time.deltaTime;

            // Safety: never get stuck if a soldier can't reach the point
            if (timeWalking > 60f)
            {
                SnapSoldiersToFinal();
                break;
            }

            // Subtle cinematic push
            if (cam != null)
            {
                float camT = Mathf.Clamp01(timeWalking / 6f);
                float smoothCamT = Mathf.SmoothStep(0f, 1f, camT);
                cam.transform.position = Vector3.Lerp(camStartPos, camEndPos, smoothCamT);
                cam.transform.rotation = camStartRot;
            }

            StepActor(soldier1);
            StepActor(soldier2);

            yield return null;
        }

        // Both arrived: let them settle into their final facing
        float settleTimer = 0f;
        while (settleTimer < 0.6f)
        {
            settleTimer += Time.deltaTime;
            FaceAtEnd(soldier1);
            FaceAtEnd(soldier2);
            yield return null;
        }
    }

    private void StepActor(SoldierActor a)
    {
        if (!HasWalk(a)) return;

        if (a.arrived)
        {
            FaceAtEnd(a);
            return;
        }

        Vector3 target = a.endPoint.position;
        Vector3 toTarget = target - a.pos;

        if (toTarget.magnitude > 0.05f)
        {
            Vector3 flat = toTarget;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
                a.rot = Quaternion.Slerp(a.rot, lookRot, Time.deltaTime * 6f);
            }

            a.pos = Vector3.MoveTowards(a.pos, target, infantryWalkSpeed * Time.deltaTime);
        }
        else
        {
            a.pos = target;
            a.arrived = true;
            SetMoving(a, false);
        }
    }

    private Quaternion GetFinalRotation(SoldierActor a, out bool valid)
    {
        valid = true;

        if (faceInayAtEnd && inayTarget != null)
        {
            Vector3 d = inayTarget.position - a.pos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f)
                return Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        if (a.endPoint != null)
            return a.endPoint.rotation;

        valid = false;
        return a.rot;
    }

    private void FaceAtEnd(SoldierActor a)
    {
        if (!HasWalk(a)) return;

        bool valid;
        Quaternion target = GetFinalRotation(a, out valid);
        if (!valid) return;

        a.rot = Quaternion.Slerp(a.rot, target, Time.deltaTime * 7f);
    }

    private void SetMoving(SoldierActor a, bool moving)
    {
        if (a == null || a.anim == null || string.IsNullOrEmpty(movingParameter)) return;
        if (a.anim.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in a.anim.parameters)
        {
            if (p.name == movingParameter && p.type == AnimatorControllerParameterType.Bool)
            {
                a.anim.SetBool(movingParameter, moving);
                return;
            }
        }

        Debug.LogWarning("M2OpeningCinematic: Animator has no Bool parameter named '" + movingParameter + "'. Check the 'Moving Parameter' field.", this);
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

    // ------------------------------------------------------------------
    // Skip / End
    // ------------------------------------------------------------------
    private IEnumerator SkipCinematicSequence()
    {
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeRoutine(fadeCanvasGroup.alpha, 1f, 0.3f));
        }

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
        SnapActorToFinal(soldier1);
        SnapActorToFinal(soldier2);
    }

    private void SnapActorToFinal(SoldierActor a)
    {
        if (!HasWalk(a)) return;

        a.pos = a.endPoint.position;

        bool valid;
        Quaternion finalRot = GetFinalRotation(a, out valid);
        if (valid) a.rot = finalRot;

        a.arrived = true;
        SetMoving(a, false);
    }

    private void EndCinematic()
    {
        hasCompleted = true;

        // Either keep the soldiers pinned at their end pose, or hand them back to the scene
        soldier1.controlled = keepSoldiersAtEndAfterCinematic && soldier1.soldier != null;
        soldier2.controlled = keepSoldiersAtEndAfterCinematic && soldier2.soldier != null;

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

    // ------------------------------------------------------------------
    // Scene-view helper: shows Start / End points and the path between them
    // ------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        DrawActorGizmo(soldier1, Color.cyan);
        DrawActorGizmo(soldier2, Color.yellow);
    }

    private void DrawActorGizmo(SoldierActor a, Color color)
    {
        if (a == null) return;

        Gizmos.color = color;

        if (a.startPoint != null)
            Gizmos.DrawWireSphere(a.startPoint.position, 0.25f);

        if (a.endPoint != null)
            Gizmos.DrawSphere(a.endPoint.position, 0.25f);

        if (a.startPoint != null && a.endPoint != null)
            Gizmos.DrawLine(a.startPoint.position, a.endPoint.position);
    }
}