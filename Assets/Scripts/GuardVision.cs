
using UnityEngine;

public class GuardVision : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Vision Settings")]
    [SerializeField] private float viewDistance = 8f;
    [SerializeField, Range(0f, 180f)]
    private float viewAngle = 90f;

    [Header("Line of Sight")]
    [SerializeField] private Transform eyePoint;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private bool playerDetected;

    public bool PlayerDetected => playerDetected;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(
                playerTag);

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
        {
            Debug.LogWarning(
                "GuardVision: Player not found. Assign the Player Transform " +
                "or check that the Player tag is correct.", this);
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        bool currentlyVisible = CanSeePlayer();

        if (currentlyVisible && !playerDetected)
        {
            playerDetected = true;

            if (showDebugMessages)
                Debug.Log("GUARD DETECTED THE PLAYER!", this);
        }
        else if (!currentlyVisible && playerDetected)
        {
            playerDetected = false;

            if (showDebugMessages)
                Debug.Log("Guard can no longer see the player.", this);
        }
    }

    private bool CanSeePlayer()
    {
        Vector3 eyePosition = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up * 1.6f;

        Vector3 targetPosition = player.position + Vector3.up * 1f;
        Vector3 directionToPlayer = targetPosition - eyePosition;

        if (directionToPlayer.magnitude > viewDistance)
            return false;

        // Compare the direction with the guard's forward-facing direction.
        float angle = Vector3.Angle(
            transform.forward, directionToPlayer);

        if (angle > viewAngle * 0.5f)
            return false;

        // Check whether an obstacle blocks the guard's line of sight.
        if (Physics.Raycast(
            eyePosition,
            directionToPlayer.normalized,
            out RaycastHit hit,
            directionToPlayer.magnitude,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            // An object on an obstacle layer blocks the view.
            return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 leftBoundary = Quaternion.Euler(
            0f, -viewAngle * 0.5f, 0f) * transform.forward;

        Vector3 rightBoundary = Quaternion.Euler(
            0f, viewAngle * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftBoundary * viewDistance);
        Gizmos.DrawRay(transform.position, rightBoundary * viewDistance);
    }
}
