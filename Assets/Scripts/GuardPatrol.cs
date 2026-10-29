
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

    private NavMeshAgent agent;
    private Transform targetPoint;
    private float waitTimer;
    private bool waiting;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError(
                "GuardPatrol: Assign both patrol points.", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "GuardPatrol: The guard is not on a baked NavMesh.",
                this);
            enabled = false;
            return;
        }

        targetPoint = pointB;
        agent.SetDestination(targetPoint.position);
    }

    private void Update()
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
}
