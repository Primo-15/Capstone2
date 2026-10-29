
using UnityEngine;

public class QuestDoorInteraction : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private string doorName = "Exit Door";
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private Transform player;

    [Header("Final Quest Settings")]
    [SerializeField] private string finalQuestID = "main_quest_04";
    [SerializeField] private string finalObjectiveID = "unlock_exit_door";

    [Header("Required Item")]
    [SerializeField] private string requiredItemID = "storage_key";

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "Map_02";

    private bool isProcessing;

    private void Update()
    {
        if (player == null || isProcessing)
            return;

        if (QuestSceneTransition.Instance != null &&
            QuestSceneTransition.Instance.IsTransitioning)
            return;

        float distance = Vector3.Distance(
            player.position,
            transform.position
        );

        if (distance > interactionRange)
            return;

        if (Input.GetKeyDown(interactionKey))
            InteractWithDoor();
    }

    private void InteractWithDoor()
    {
        QuestManager questManager = QuestManager.Instance;
        InventoryManager inventory = InventoryManager.Instance;
        QuestSceneTransition transition = QuestSceneTransition.Instance;

        if (questManager == null || inventory == null || transition == null)
        {
            Debug.LogWarning(
                "QuestManager, InventoryManager, or " +
                "QuestSceneTransition was not found."
            );
            return;
        }

        if (questManager.ActiveQuest == null ||
            questManager.ActiveQuest.questID != finalQuestID)
        {
            Debug.Log($"{doorName}: The door is still locked.");
            return;
        }

        int index = questManager.CurrentObjectiveIndex;

        if (index < 0 ||
            index >= questManager.ActiveQuest.objectives.Length)
            return;

        QuestObjectiveData objective =
            questManager.ActiveQuest.objectives[index];

        if (objective == null ||
            objective.objectiveID != finalObjectiveID ||
            objective.objectiveType != QuestObjectiveType.Interact)
        {
            Debug.Log(
                $"{doorName}: Complete the earlier objectives first."
            );
            return;
        }

        if (!inventory.HasItem(requiredItemID))
        {
            Debug.Log(
                $"{doorName}: You need {requiredItemID} to open this door."
            );
            return;
        }

        isProcessing = true;

        // Complete the final objective.
        bool accepted =
            questManager.AddObjectiveProgress(finalObjectiveID);

        if (!accepted ||
            !questManager.IsQuestCompleted(finalQuestID))
        {
            isProcessing = false;

            Debug.LogWarning(
                "The final quest was not completed. " +
                "The scene will not change."
            );
            return;
        }

        // Consume the required item.
        if (!inventory.RemoveItem(requiredItemID))
        {
            isProcessing = false;

            Debug.LogError(
                "The quest completed, but the required item " +
                "could not be removed."
            );
            return;
        }

        // Load the destination scene through the shared manager.
        if (!transition.LoadNextScene(nextSceneName))
        {
            isProcessing = false;

            Debug.LogError(
                "The quest is complete, but the next scene could not load."
            );
        }
    }
}
