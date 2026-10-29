
using UnityEngine;

public class QuestLocationTrigger : MonoBehaviour
{
    [Header("Quest Settings")]
    [SerializeField] private string requiredQuestID = "main_quest_05";
    [SerializeField] private string objectiveID = "reach_destination";

    [Header("Player Settings")]
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag(playerTag))
            return;

        QuestManager manager = QuestManager.Instance;

        if (manager == null || manager.ActiveQuest == null)
            return;

        if (manager.ActiveQuest.questID != requiredQuestID)
            return;

        if (manager.CurrentObjectiveIndex >=
            manager.ActiveQuest.objectives.Length)
            return;

        QuestObjectiveData currentObjective =
            manager.ActiveQuest.objectives[manager.CurrentObjectiveIndex];

        if (currentObjective.objectiveID != objectiveID ||
            currentObjective.objectiveType != QuestObjectiveType.ReachLocation)
            return;

        if (manager.AddObjectiveProgress(objectiveID))
        {
            hasTriggered = true;
            Debug.Log("Destination reached! Quest objective completed.");
        }
    }
}
