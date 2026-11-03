using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GuardPatrol : MonoBehaviour
{
    [Header("Patrol Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Patrol Settings")]
    [SerializeField] private float waitAtPoint = 1f;
    [SerializeField] private float arrivalDistance = 0.3f;

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

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float loseSightTime = 3f;
    [SerializeField] private float catchDistance = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private NavMeshAgent agent;
    private Transform targetPoint;
    private float waitTimer;
    private bool waiting;

    private float patrolSpeed;
    private bool chasing;
    private bool playerDetected;
    private float lostTimer;

    public bool PlayerDetected => playerDetected;
    public bool IsChasing => chasing;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("GuardPatrol: Assign both patrol points.", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "GuardPatrol: The guard is not on a baked NavMesh.", this);
            enabled = false;
            return;
        }

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(playerTag);

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
        {
            Debug.LogWarning(
                "GuardPatrol: Player not found. Assign the Player Transform " +
                "or check that the Player tag is correct.", this);
        }

        patrolSpeed = agent.speed;
        targetPoint = pointB;
        agent.SetDestination(targetPoint.position);
    }

    private void Update()
    {
        UpdateVision();

        if (playerDetected)
        {
            chasing = true;
            lostTimer = loseSightTime;
        }
        else if (chasing)
        {
            lostTimer -= Time.deltaTime;

            if (lostTimer <= 0f)
                StopChasing();
        }

        if (chasing)
        {
            Chase();
            return; // skip patrol logic while chasing
        }

        Patrol();
    }

    // ---------------- CHASE ----------------

    private void Chase()
    {
        waiting = false;
        agent.isStopped = false;
        agent.speed = chaseSpeed;

        float distance = Vector3.Distance(
            transform.position, player.position);

        if (distance <= catchDistance)
        {
            agent.ResetPath();

            if (showDebugMessages)
                Debug.Log("GUARD CAUGHT THE PLAYER!", this);

            // Put your "caught" logic here (game over, reload, dialogue...)
            return;
        }

        agent.SetDestination(player.position);
    }

    private void StopChasing()
    {
        chasing = false;
        agent.speed = patrolSpeed;

        // Resume patrolling toward the nearer patrol point.
        float distA = Vector3.Distance(transform.position, pointA.position);
        float distB = Vector3.Distance(transform.position, pointB.position);
        targetPoint = distA < distB ? pointA : pointB;

        waiting = false;
        agent.SetDestination(targetPoint.position);

        if (showDebugMessages)
            Debug.Log("Guard lost the player, returning to patrol.", this);
    }

    // ---------------- PATROL ----------------

    private void Patrol()
    {
        if (agent.pathPending)
            return;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                waiting = false;
                targetPoint = targetPoint == pointA ? pointB : pointA;
                agent.SetDestination(targetPoint.position);
            }

            return;
        }

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            return;

        if (agent.remainingDistance <=
            Mathf.Max(arrivalDistance, agent.stoppingDistance))
        {
            waiting = true;
            waitTimer = waitAtPoint;
            agent.ResetPath();
        }
    }

    // ---------------- VISION ----------------

    private void UpdateVision()
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

        // Flatten the direction so hills/stairs don't throw off the cone.
        Vector3 flatDirection =
            new Vector3(directionToPlayer.x, 0f, directionToPlayer.z);

        float angle = Vector3.Angle(transform.forward, flatDirection);

        if (angle > viewAngle * 0.5f)
            return false;

        if (Physics.Raycast(
            eyePosition,
            directionToPlayer.normalized,
            out RaycastHit hit,
            directionToPlayer.magnitude,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 leftBoundary =
            Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightBoundary =
            Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftBoundary * viewDistance);
        Gizmos.DrawRay(transform.position, rightBoundary * viewDistance);
    }
}