using System.Collections;
using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Open Motion")]
    [Tooltip("Optional. Defaults to this object.")]
    [SerializeField] private Transform gateToMove;

    [Tooltip("Local Position of the gate when it looks open. Copy it from the Transform in Edit mode.")]
    [SerializeField] private Vector3 openLocalPosition;

    [Tooltip("Local Rotation (Euler) of the gate when it looks open. Copy it from the Transform in Edit mode.")]
    [SerializeField] private Vector3 openLocalEuler;

    [SerializeField] private float openDuration = 1.5f;

    private Rigidbody body;
    private bool isOpen;

    private void Awake()
    {
        if (gateToMove == null)
            gateToMove = transform;

        // Lock the gate so the player can't push it or bump it.
        body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    // Call this when the gate receives the key.
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Vector3 startPos = gateToMove.localPosition;
        Quaternion startRot = gateToMove.localRotation;
        Vector3 endPos = openLocalPosition;
        Quaternion endRot = Quaternion.Euler(openLocalEuler);

        // How far the gate turns around the vertical axis.
        float angle = Mathf.DeltaAngle(startRot.eulerAngles.y, openLocalEuler.y);
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float det = 2f - 2f * cos;

        // Find the hinge point that turns the closed pose into the open pose.
        // Swinging around it gives a natural arc instead of a straight slide.
        bool hasHinge = Mathf.Abs(det) > 0.0001f;
        Vector3 hinge = Vector3.zero;

        if (hasHinge)
        {
            Quaternion full = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 rhs = endPos - full * startPos;

            hinge = new Vector3(
                ((1f - cos) * rhs.x + sin * rhs.z) / det,
                startPos.y,
                (-sin * rhs.x + (1f - cos) * rhs.z) / det
            );
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            if (hasHinge)
            {
                Quaternion step = Quaternion.AngleAxis(angle * k, Vector3.up);

                Vector3 pos = hinge + step * (startPos - hinge);
                pos.y = Mathf.Lerp(startPos.y, endPos.y, k);

                gateToMove.localPosition = pos;
                gateToMove.localRotation = step * startRot;
            }
            else
            {
                // No turning, so just slide.
                gateToMove.localPosition = Vector3.Lerp(startPos, endPos, k);
            }

            yield return null;
        }

        gateToMove.localPosition = endPos;
        gateToMove.localRotation = endRot;
    }
}