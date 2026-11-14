using UnityEngine;
using UnityEngine.AI;

/// Moves this NPC (Rosa in M2) to a new spot once the "Find_a_basket" objective
/// is reached (acquired) or passed (completed). Moves only once.
public class NextNPCLocation : MonoBehaviour
{
    [Header("Quest Trigger")]
    [SerializeField] private string questID = "main_quest_02";
    [SerializeField] private string objectiveID = "Find_a_basket";

    [Header("Destination (editable)")]
    [SerializeField]
    private Vector3 targetPosition =
        new Vector3(71.9034348f, 5.05300045f, 19.7952976f);

    [SerializeField]
    private Vector3 targetRotation =
        new Vector3(0f, 239.610901f, 0f);

    [Header("Ground Snapping")]
    [Tooltip("If on, Y is calculated from the terrain at the target X/Z, so the Y in Target Position is ignored.")]
    [SerializeField] private bool snapToTerrain = true;

    [Tooltip("Extra height above the terrain. Raise slightly (e.g. 0.05) if her feet clip into the ground.")]
    [SerializeField] private float groundOffset = 0f;

    private QuestManager questManager;
    private bool hasMoved;

    private void Start()
    {
        questManager = QuestManager.Instance;

        if (questManager == null)
        {
            Debug.LogWarning("NextNPCLocation: no QuestManager found.", this);
            return;
        }

        questManager.OnObjectiveChanged += HandleObjectiveChanged;
        questManager.OnQuestCompleted += HandleQuestCompleted;

        // Quest may already be acquired/completed when the scene loads.
        CheckCurrentState();
    }

    private void OnDestroy()
    {
        if (questManager != null)
        {
            questManager.OnObjectiveChanged -= HandleObjectiveChanged;
            questManager.OnQuestCompleted -= HandleQuestCompleted;
        }
    }

    private void CheckCurrentState()
    {
        if (questManager.IsQuestCompleted(questID))
        {
            MoveToTarget();
            return;
        }

        QuestData active = questManager.ActiveQuest;
        if (active != null && active.questID == questID)
            HandleObjectiveChanged(active, questManager.CurrentObjectiveIndex);
    }

    private void HandleObjectiveChanged(QuestData quest, int objectiveIndex)
    {
        if (hasMoved || quest == null || quest.questID != questID || quest.objectives == null)
            return;

        for (int i = 0; i < quest.objectives.Length; i++)
        {
            if (quest.objectives[i] != null && quest.objectives[i].objectiveID == objectiveID)
            {
                // Reached (acquired) or already passed (completed) the objective
                if (objectiveIndex >= i)
                    MoveToTarget();
                return;
            }
        }
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        if (quest != null && quest.questID == questID)
            MoveToTarget();
    }

    private void MoveToTarget()
    {
        if (hasMoved) return;
        hasMoved = true;

        Vector3 finalPosition = snapToTerrain ? GetGroundedPosition(targetPosition) : targetPosition;
        Quaternion rotation = Quaternion.Euler(targetRotation);

        CharacterController cc = GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (ccWasEnabled) cc.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(finalPosition);

        transform.SetPositionAndRotation(finalPosition, rotation);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = finalPosition;
            rb.rotation = rotation;
        }

        if (ccWasEnabled) cc.enabled = true;

        Physics.SyncTransforms();
    }

    /// Returns the given position with Y set to the terrain surface at that X/Z.
    private Vector3 GetGroundedPosition(Vector3 pos)
    {
        Terrain terrain = null;

        // Find the terrain that actually covers this X/Z (in case there are several).
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 tp = t.transform.position;
            Vector3 size = t.terrainData.size;

            if (pos.x >= tp.x && pos.x <= tp.x + size.x &&
                pos.z >= tp.z && pos.z <= tp.z + size.z)
            {
                terrain = t;
                break;
            }
        }

        if (terrain == null)
            terrain = Terrain.activeTerrain;

        if (terrain == null)
        {
            Debug.LogWarning("NextNPCLocation: no Terrain found, using the Y from Target Position.", this);
            return pos;
        }

        // SampleHeight is relative to the terrain's own Y, so add it back.
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + groundOffset;
        return pos;
    }
}