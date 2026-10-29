
using UnityEngine;

public class QuestLocationTransition : MonoBehaviour
{
    [Header("Final Quest Settings")]
    [SerializeField] private string finalQuestID = "main_quest_08";
    [SerializeField] private string finalObjectiveID = "reach_mountain_pass";

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "Map_03";

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    private bool isProcessing;

    private void OnTriggerEnter(Collider other)
    {
        if (isProcessing)
            return;

        if (!other.CompareTag(playerTag))
            return;

        QuestSceneTransition transition = QuestSceneTransition.Instance;

        if (transition == null || transition.IsTransitioning)
            return;

        QuestManager questManager = QuestManager.Instance;

        if (questManager == null || questManager.ActiveQuest == null)
            return;

        // Only accept the designated final quest.
        if (questManager.ActiveQuest.questID != finalQuestID)
            return;

        int index = questManager.CurrentObjectiveIndex;

        if (index < 0 ||
            index >= questManager.ActiveQuest.objectives.Length)
            return;

        QuestObjectiveData objective =
            questManager.ActiveQuest.objectives[index];

        // Only trigger when the correct ReachLocation objective is active.
        if (objective == null ||
            objective.objectiveID != finalObjectiveID ||
            objective.objectiveType != QuestObjectiveType.ReachLocation)
            return;

        isProcessing = true;

        bool accepted =
            questManager.AddObjectiveProgress(finalObjectiveID);

        // A map transition is allowed only if this objective completed
        // the designated quest.
        if (!accepted ||
            !questManager.IsQuestCompleted(finalQuestID))
        {
            isProcessing = false;

            Debug.LogWarning(
                "The final location objective did not complete the quest."
            );
            return;
        }

        Debug.Log(
            $"Final location reached. Loading {nextSceneName}."
        );

        if (!transition.LoadNextScene(nextSceneName))
        {
            isProcessing = false;

            Debug.LogError(
                "The quest completed, but the next scene could not load."
            );
        }
    }
}
