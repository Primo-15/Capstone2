
using UnityEngine;

public class QuestLocationTrigger : MonoBehaviour
{
    [Header("Quest Settings")]
    [SerializeField] private string requiredQuestID = "main_quest_05";
    [SerializeField] private string objectiveID = "reach_destination";

    [Header("Player Settings")]
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;
    private int lastTriggeredFrame = -1;

    public bool HasTriggered => hasTriggered;
    public bool DidTriggerThisFrame => lastTriggeredFrame == Time.frameCount;

    public bool CanTriggerNow()
    {
        if (hasTriggered)
            return false;

        QuestManager manager = QuestManager.Instance;
        if (manager == null || manager.ActiveQuest == null)
            return false;

        if (manager.ActiveQuest.questID != requiredQuestID)
            return false;

        if (manager.CurrentObjectiveIndex < 0 ||
            manager.CurrentObjectiveIndex >= manager.ActiveQuest.objectives.Length)
            return false;

        QuestObjectiveData currentObjective =
            manager.ActiveQuest.objectives[manager.CurrentObjectiveIndex];

        if (currentObjective == null ||
            currentObjective.objectiveID != objectiveID ||
            currentObjective.objectiveType != QuestObjectiveType.ReachLocation)
            return false;

        return true;
    }

    public bool TryTrigger(Collider other)
    {
        if (other == null || !other.CompareTag(playerTag))
            return false;

        if (DidTriggerThisFrame)
            return true;

        if (!CanTriggerNow())
            return false;

        QuestManager manager = QuestManager.Instance;
        if (manager != null && manager.AddObjectiveProgress(objectiveID))
        {
            hasTriggered = true;
            lastTriggeredFrame = Time.frameCount;
            Debug.Log("Destination reached! Quest objective completed.");
            return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasTriggered || QuestNPCInteraction.IsInDialogue)
            return;

        TryTrigger(other);
    }
}
