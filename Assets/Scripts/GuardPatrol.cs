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
    [SerializeField] private float stunDuration = 3f;

    [Header("Distraction / Investigation")]
    [SerializeField] private float distractionRadius = 4.5f;
    [SerializeField] private float investigateDuration = 2f;
    [SerializeField] private float investigateStoppingDistance = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private NavMeshAgent agent;
    private Animator guardAnimator;
    private bool hasIsMovingParam;
    private Transform targetPoint;
    private float waitTimer;
    private bool waiting;

    private float patrolSpeed;
    private bool chasing;
    private bool playerDetected;
    private bool caught;
    private bool stunned;
    private float stunTimer;
    private float lostTimer;

    private bool investigating;
    private bool investigatingArrived;
    private Vector3 investigateTarget;
    private Vector3 investigateLookPoint;
    private float investigateTimer;
    private float activeInvestigateDuration;

    public bool PlayerDetected => playerDetected;
    public bool IsChasing => chasing;
    public bool IsInvestigating => investigating;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        guardAnimator = GetComponentInChildren<Animator>();
        CacheAnimatorParameters();
    }

    private void CacheAnimatorParameters()
    {
        hasIsMovingParam = false;
        if (guardAnimator == null || guardAnimator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter param in guardAnimator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool && param.name == "IsMoving")
            {
                hasIsMovingParam = true;
                break;
            }
        }
    }

    private void SetGuardMovingAnimation(bool moving)
    {
        if (guardAnimator != null && hasIsMovingParam)
        {
            guardAnimator.SetBool("IsMoving", moving);
        }
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
        if (caught)
            return; // quick time event or game over is running

        if (stunned)
        {
            stunTimer -= Time.deltaTime;

            if (stunTimer <= 0f)
                EndStun();

            return;
        }

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
            if (investigating)
            {
                investigating = false;
                investigatingArrived = false;
                SetGuardMovingAnimation(true);
            }

            Chase();
            return; // skip patrol logic while chasing
        }

        if (investigating)
        {
            UpdateInvestigation();
            return;
        }

        Patrol();
    }

    // ---------------- DISTRACTION / INVESTIGATION ----------------

    public static int NotifyThrownRockLanded(Vector3 landingPosition, float maxRadius = 4.5f, float investigateSeconds = 2f)
    {
        GuardPatrol[] guards = FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None);
        int notifiedCount = 0;

        for (int i = 0; i < guards.Length; i++)
        {
            GuardPatrol guard = guards[i];
            if (guard == null || !guard.isActiveAndEnabled)
                continue;

            float radius = maxRadius > 0f ? maxRadius : guard.distractionRadius;
            float dist3D = Vector3.Distance(guard.transform.position, landingPosition);
            float distXZ = Vector2.Distance(
                new Vector2(guard.transform.position.x, guard.transform.position.z),
                new Vector2(landingPosition.x, landingPosition.z));

            if (dist3D <= radius || distXZ <= radius)
            {
                guard.InvestigateLocation(landingPosition, investigateSeconds);
                notifiedCount++;
            }
        }

        return notifiedCount;
    }

    public void InvestigateLocation(Vector3 rockLandingPosition, float duration = 2f)
    {
        if (!isActiveAndEnabled || caught || stunned || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // If actively seeing the player right now, prioritize sight
        if (playerDetected)
            return;

        chasing = false;
        waiting = false;
        investigating = true;
        investigatingArrived = false;
        activeInvestigateDuration = duration > 0f ? duration : (investigateDuration > 0f ? investigateDuration : 2f);
        investigateTimer = activeInvestigateDuration;
        investigateLookPoint = rockLandingPosition;

        Vector3 navDestination = rockLandingPosition;
        if (NavMesh.SamplePosition(rockLandingPosition, out NavMeshHit navHit, 4.5f, NavMesh.AllAreas))
        {
            navDestination = navHit.position;
        }

        investigateTarget = navDestination;
        agent.isStopped = false;
        agent.speed = patrolSpeed > 0.01f ? patrolSpeed : 1f;
        agent.SetDestination(investigateTarget);
        SetGuardMovingAnimation(true);

        if (showDebugMessages)
            Debug.Log("Guard detected thrown rock! Moving to investigate at " + rockLandingPosition, this);
    }

    private void UpdateInvestigation()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            investigating = false;
            investigatingArrived = false;
            return;
        }

        if (!investigatingArrived)
        {
            if (agent.pathPending)
                return;

            float stopDist = Mathf.Max(investigateStoppingDistance, agent.stoppingDistance + 0.15f);
            float flatDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(investigateTarget.x, investigateTarget.z));

            bool reached = agent.remainingDistance <= stopDist
                || flatDist <= stopDist
                || (agent.pathStatus != NavMeshPathStatus.PathComplete && agent.velocity.sqrMagnitude < 0.01f);

            if (reached)
            {
                investigatingArrived = true;
                investigateTimer = activeInvestigateDuration > 0f ? activeInvestigateDuration : 2f;
                agent.ResetPath();
                agent.isStopped = true;
                SetGuardMovingAnimation(false);

                if (showDebugMessages)
                    Debug.Log("Guard investigating rock for " + investigateTimer + " seconds.", this);
            }
        }
        else
        {
            Vector3 lookDir = investigateLookPoint - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }

            investigateTimer -= Time.deltaTime;
            if (investigateTimer <= 0f)
            {
                ResumePatrolAfterInvestigation();
            }
        }
    }

    private void ResumePatrolAfterInvestigation()
    {
        investigating = false;
        investigatingArrived = false;
        waiting = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = patrolSpeed > 0.01f ? patrolSpeed : 1f;

            if (targetPoint == null)
            {
                if (pointA != null && pointB != null)
                {
                    float distA = Vector3.Distance(transform.position, pointA.position);
                    float distB = Vector3.Distance(transform.position, pointB.position);
                    targetPoint = distA < distB ? pointA : pointB;
                }
                else
                {
                    targetPoint = pointB != null ? pointB : pointA;
                }
            }

            if (targetPoint != null)
            {
                agent.SetDestination(targetPoint.position);
            }
        }

        SetGuardMovingAnimation(true);

        if (showDebugMessages)
            Debug.Log("Guard finished investigating rock. Resuming normal patrol.", this);
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
            CatchPlayer();
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

    // ---------------- CATCH / QUICK TIME EVENT ----------------

    private void CatchPlayer()
    {
        // Another guard's quick time event is already running; wait for it.
        if (QuickTimeEvent.Instance != null && QuickTimeEvent.Instance.IsActive)
            return;

        agent.ResetPath();

        if (showDebugMessages)
            Debug.Log("GUARD CAUGHT THE PLAYER!", this);

        if (QuickTimeEvent.Instance != null && PlayerStats.HasStamina)
        {
            // Only lock this guard if the event actually started.
            if (QuickTimeEvent.Instance.StartEvent(OnPlayerDodged, TriggerGameOver))
                caught = true;

            return;
        }

        // No stamina (or no quick time event in this scene): straight to game over.
        if (showDebugMessages && !PlayerStats.HasStamina)
            Debug.Log("No stamina left, no dodge chance.", this);

        TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        caught = true;

        if (GameOverScreen.Instance != null)
            GameOverScreen.Instance.Show();
        else
            Debug.LogWarning(
                "GuardPatrol: No GameOverScreen found in this scene.", this);
    }

    private void OnPlayerDodged()
    {
        caught = false;
        chasing = false;
        playerDetected = false;

        stunned = true;
        stunTimer = stunDuration;

        agent.ResetPath();
        agent.isStopped = true;
        agent.speed = patrolSpeed;

        if (showDebugMessages)
            Debug.Log("Player dodged! Guard stunned.", this);
    }

    private void EndStun()
    {
        stunned = false;
        agent.isStopped = false;
        StopChasing(); // heads back to the nearer patrol point
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