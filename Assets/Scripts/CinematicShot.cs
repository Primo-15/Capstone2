using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// Put this on each Cinemachine Camera used in the dialogue cinematic.
/// The camera slowly slides and turns while it is the active shot.
[RequireComponent(typeof(CinemachineCamera))]
public class CinematicShot : MonoBehaviour
{
    [Tooltip("Play order across ALL scenes. Example: M4 shots = 0, 1 and M5 shots = 2, 3")]
    public int order;

    [Tooltip("Seconds this shot stays on screen")]
    public float duration = 6f;

    [Header("Pan (relative to the camera's own direction)")]
    [Tooltip("X = slide right, Y = slide up, Z = push forward")]
    public Vector3 slide = new Vector3(3f, 0f, 0f);
    [Tooltip("Degrees to turn left/right during the shot")]
    public float yawDegrees = 10f;

    [HideInInspector] public CinemachineCamera cam;

    Vector3 startPos;
    Quaternion startRot;
    bool hasStart;

    void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        cam.Priority = 0; // stay inactive until the manager picks this shot
    }

    public IEnumerator Play()
    {
        // Recorded lazily so any scene offset applied after loading is included
        if (!hasStart)
        {
            startPos = transform.position;
            startRot = transform.rotation;
            hasStart = true;
        }

        Vector3 endPos = startPos + startRot * slide;
        Quaternion endRot = startRot * Quaternion.Euler(0f, yawDegrees, 0f);
        transform.SetPositionAndRotation(startPos, startRot);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, endPos, k),
                Quaternion.Slerp(startRot, endRot, k));
            yield return null;
        }
    }

    public void ResetPose()
    {
        if (hasStart) transform.SetPositionAndRotation(startPos, startRot);
    }
}