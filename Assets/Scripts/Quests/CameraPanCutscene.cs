using System;
using System.Collections;
using UnityEngine;

// Pans the main camera from one target (e.g. Mang Rodolfo) to another
// (e.g. Rosa), holds there, then returns to normal gameplay.
// Put this on an empty GameObject and assign it to the NPC's
// "Cutscene After First Talk" slot.
public class CameraPanCutscene : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Where the pan starts (e.g. Mang Rodolfo).")]
    [SerializeField] private Transform startTarget;

    [Tooltip("Where the pan ends (e.g. Rosa).")]
    [SerializeField] private Transform endTarget;

    [Tooltip("Leave empty to use the camera tagged MainCamera.")]
    [SerializeField] private Camera cam;

    [Header("Exact Camera Positions (optional)")]
    [Tooltip("If BOTH are set, the camera flies from the first to the second. The Targets and Camera Framing settings are then ignored.")]
    [SerializeField] private Transform startCameraPoint;
    [SerializeField] private Transform endCameraPoint;

    [Header("Timing (seconds)")]
    [SerializeField] private float blendInTime = 1f;
    [SerializeField] private float panTime = 4f;
    [Tooltip("How long the camera stays on the end target.")]
    [SerializeField] private float holdTime = 3f;
    [SerializeField] private float blendOutTime = 1f;

    [Header("Camera Framing")]
    [Tooltip("How far the camera is from the path. Use a negative value to view from the other side.")]
    [SerializeField] private float sideDistance = 6f;
    [SerializeField] private float height = 2.5f;
    [Tooltip("Height of the point the camera looks at (about chest height).")]
    [SerializeField] private float lookHeight = 1.4f;

    private Behaviour brain;
    private Action onDone;
    private bool running;
    private Vector3 savedPos;
    private Quaternion savedRot;

    public bool IsPlaying => running;

    public void Play(Action onFinished)
    {
        if (running)
            return;

        if (cam == null)
            cam = Camera.main;

        bool usePoints = startCameraPoint != null && endCameraPoint != null;

        if (cam == null ||
            (!usePoints && (startTarget == null || endTarget == null)))
        {
            Debug.LogWarning(
                "CameraPanCutscene: assign the Start/End Camera Points " +
                "(or the Start/End Targets) and make sure a MainCamera exists."
            );
            onFinished?.Invoke();
            return;
        }

        onDone = onFinished;
        brain = FindBrain(cam);
        running = true;

        StartCoroutine(Run());
    }

    // Finds the Cinemachine Brain (if any) without needing the package.
    private static Behaviour FindBrain(Camera camera)
    {
        foreach (Behaviour b in camera.GetComponents<Behaviour>())
        {
            if (b != null && b.GetType().Name == "CinemachineBrain")
                return b;
        }

        return null;
    }

    private IEnumerator Run()
    {
        savedPos = cam.transform.position;
        savedRot = cam.transform.rotation;

        // The brain would fight us for control of the camera.
        if (brain != null)
            brain.enabled = false;

        Vector3 pos;
        Quaternion rot;

        // 1. Blend from the gameplay view to the start target.
        GetPose(0f, out Vector3 startPos, out Quaternion startRot);
        float e = 0f;
        float dur = Mathf.Max(0.01f, blendInTime);

        while (e < dur)
        {
            e += Time.deltaTime;
            float k = Smooth(e / dur);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(savedPos, startPos, k),
                Quaternion.Slerp(savedRot, startRot, k)
            );
            yield return null;
        }

        // 2. Pan from the start target to the end target.
        e = 0f;
        dur = Mathf.Max(0.01f, panTime);

        while (e < dur)
        {
            e += Time.deltaTime;
            GetPose(Smooth(e / dur), out pos, out rot);
            cam.transform.SetPositionAndRotation(pos, rot);
            yield return null;
        }

        // 3. Stay on the end target.
        e = 0f;

        while (e < holdTime)
        {
            e += Time.deltaTime;
            GetPose(1f, out pos, out rot);
            cam.transform.SetPositionAndRotation(pos, rot);
            yield return null;
        }

        // 4. Blend back to the gameplay view.
        GetPose(1f, out Vector3 endPos, out Quaternion endRot);
        e = 0f;
        dur = Mathf.Max(0.01f, blendOutTime);

        while (e < dur)
        {
            e += Time.deltaTime;
            float k = Smooth(e / dur);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(endPos, savedPos, k),
                Quaternion.Slerp(endRot, savedRot, k)
            );
            yield return null;
        }

        Finish();
    }

    // Camera pose for a point t (0..1) along the path. The camera sits
    // beside the path and looks at the moving focus point.
    private void GetPose(float t, out Vector3 pos, out Quaternion rot)
    {
        // Exact camera points: fly from one to the other.
        if (startCameraPoint != null && endCameraPoint != null)
        {
            pos = Vector3.Lerp(
                startCameraPoint.position, endCameraPoint.position, t);
            rot = Quaternion.Slerp(
                startCameraPoint.rotation, endCameraPoint.rotation, t);
            return;
        }

        Vector3 a = startTarget.position;
        Vector3 b = endTarget.position;

        Vector3 focus = Vector3.Lerp(a, b, t) + Vector3.up * lookHeight;

        Vector3 dir = b - a;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = Vector3.forward;

        dir.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, dir);

        pos = focus + side * sideDistance
                    + Vector3.up * (height - lookHeight);

        rot = Quaternion.LookRotation(focus - pos, Vector3.up);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void Finish()
    {
        if (!running)
            return;

        running = false;

        if (cam != null)
            cam.transform.SetPositionAndRotation(savedPos, savedRot);

        if (brain != null)
            brain.enabled = true;

        Action done = onDone;
        onDone = null;
        done?.Invoke();
    }

    // Safety: never leave the camera or the player stuck if this
    // object is disabled mid-cutscene.
    private void OnDisable()
    {
        if (running)
        {
            StopAllCoroutines();
            Finish();
        }
    }
}