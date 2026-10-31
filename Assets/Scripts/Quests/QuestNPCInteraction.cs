using UnityEngine;
using TMPro;

[System.Serializable]
public class ObjectiveDialogue
{
    [Tooltip("Use these lines while this objective is the active one.")]
    public string objectiveID;

    [TextArea(2, 4)]
    public string[] lines;
}

public class QuestNPCInteraction : MonoBehaviour
{
    [Header("NPC Settings")]
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private Transform player;

    [Header("Dialogue Settings")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [TextArea(2, 4)]
    [SerializeField] private string[] dialogueLines;
    [SerializeField] private string[] finishedBuyingLines;

    [Header("Dialogue By Quest Progress")]
    [SerializeField] private ObjectiveDialogue[] objectiveDialogues;

    [Tooltip("Shown when this NPC needs an item and the player doesn't have it.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] missingItemLines;

    [Tooltip("Shown after this NPC's quest is finished.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] completedQuestLines;

    [Tooltip("Shown when the player talks to this NPC before the quest has started (e.g. before talking to the NPC that starts it). Ignored on the NPC that starts the quest.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] questNotStartedLines;

    [Tooltip("Shown when the player talks to this NPC again after already receiving her reward item.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] alreadyReceivedLines;

    [Tooltip("Shown while this NPC's quest is active but no objective entry matches.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] inProgressLines;

    [Tooltip("Shown whenever the player talks to this NPC again after the first conversation, if no quest-specific lines apply. Example: 'You can go to Rosa now.'")]
    [TextArea(2, 4)]
    [SerializeField] private string[] repeatLines;

    // True while ANY NPC dialogue is open. The player movement script
    // checks this to block movement until the dialogue is finished.
    public static bool IsInDialogue { get; private set; }

    private string[] currentLines;
    private int currentDialogueIndex;
    private bool dialogueActive;

    [Header("Quest Settings")]
    [SerializeField] private string questID;
    [SerializeField] private bool startsQuest;
    [SerializeField] private string objectiveID;
    [SerializeField] private string deliveryObjectiveID;

    [Header("Dialogue Requirements")]
    [SerializeField] private bool requiresAlingElenaInteraction;
    [SerializeField] private string alingElenaObjectiveID;
    [SerializeField] private string requiredItemForDialogue;

    [Header("Item Settings")]
    [SerializeField] private string rewardItemID;
    [SerializeField] private string requiredItemID;

    private bool hasGivenReward;
    private bool hasTalkedBefore;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(
            player.position,
            transform.position
        );

        if (distance > interactionRange)
        {
            if (dialogueActive)
                EndDialogue();

            return;
        }

        if (Input.GetKeyDown(interactionKey))
        {
            // E advances the dialogue instead of repeating
            // the quest interaction.
            if (dialogueActive)
            {
                NextDialogue();
                return;
            }

            FacePlayer();

            // Pick the lines BEFORE Interact(), because Interact()
            // can advance the quest and change the active objective.
            string[] linesToShow = ChooseLines();

            // Run your existing quest and inventory logic.
            Interact();

            // Show the dialogue for this NPC.
            StartDialogue(linesToShow);
        }
    }

    private void FacePlayer()
    {
        if (animator != null)
            animator.SetTrigger("Talk");

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    // Decides which lines to show based on quest progress.
    private string[] ChooseLines()
    {
        QuestManager manager = QuestManager.Instance;

        // No quest on this NPC (or no manager): always the default lines.
        if (manager == null || string.IsNullOrWhiteSpace(questID))
            return GetDefaultOrRepeatLines();

        // The quest is already finished.
        if (manager.IsQuestCompleted(questID) &&
            completedQuestLines != null && completedQuestLines.Length > 0)
            return completedQuestLines;

        QuestData quest = manager.ActiveQuest;

        // The quest hasn't started yet (player skipped the NPC that starts it).
        if (!startsQuest &&
            !manager.IsQuestCompleted(questID) &&
            (quest == null || quest.questID != questID) &&
            questNotStartedLines != null && questNotStartedLines.Length > 0)
            return questNotStartedLines;

        // The quest is in progress.
        if (quest != null &&
            quest.questID == questID &&
            manager.CurrentObjectiveIndex < quest.objectives.Length)
        {
            string currentObjective =
                quest.objectives[manager.CurrentObjectiveIndex].objectiveID;

            // "Do you have it or not?" check.
            if (currentObjective == deliveryObjectiveID &&
                !string.IsNullOrWhiteSpace(requiredItemID) &&
                missingItemLines != null && missingItemLines.Length > 0)
            {
                InventoryManager inventory = InventoryManager.Instance;

                if (inventory != null && !inventory.HasItem(requiredItemID))
                    return missingItemLines;
            }

            // Already gave the reward item: use the "already received" lines.
            if (!string.IsNullOrWhiteSpace(rewardItemID) &&
                alreadyReceivedLines != null && alreadyReceivedLines.Length > 0)
            {
                InventoryManager inv = InventoryManager.Instance;

                if (hasGivenReward || (inv != null && inv.HasItem(rewardItemID)))
                    return alreadyReceivedLines;
            }

            // Lines written for the current objective.
            if (objectiveDialogues != null)
            {
                foreach (ObjectiveDialogue entry in objectiveDialogues)
                {
                    if (entry != null &&
                        entry.lines != null && entry.lines.Length > 0 &&
                        entry.objectiveID == currentObjective)
                        return entry.lines;
                }
            }

            // Quest is active but no entry matched this objective.
            if (inProgressLines != null && inProgressLines.Length > 0)
                return inProgressLines;
        }

        // Nothing special applies, so use the default (or repeat) lines.
        return GetDefaultOrRepeatLines();
    }

    // First conversation: the normal lines. Every conversation after
    // that: the repeat lines (if any were written in the Inspector).
    private string[] GetDefaultOrRepeatLines()
    {
        if (hasTalkedBefore && repeatLines != null && repeatLines.Length > 0)
            return repeatLines;

        return dialogueLines;
    }

    private void StartDialogue(string[] lines)
    {
        if (requiresAlingElenaInteraction)
        {
            QuestManager manager = QuestManager.Instance;

            if (manager == null || manager.ActiveQuest == null)
                return;
        }

        if (lines == null || lines.Length == 0)
            return;

        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogWarning(
                npcName + ": Assign the dialogue UI in the Inspector."
            );
            return;
        }

        currentLines = lines;
        currentDialogueIndex = 0;
        dialogueActive = true;
        IsInDialogue = true;
        hasTalkedBefore = true;
        dialoguePanel.SetActive(true);

        ShowCurrentDialogue();
    }

    private void NextDialogue()
    {
        currentDialogueIndex++;

        if (currentLines == null ||
            currentDialogueIndex >= currentLines.Length)
        {
            EndDialogue();
            return;
        }

        ShowCurrentDialogue();
    }

    private void ShowCurrentDialogue()
    {
        dialogueText.text =
            npcName + ":\n" + currentLines[currentDialogueIndex];
    }

    private void OnDisable()
    {
        // Safety: never leave the player frozen if this NPC is
        // disabled or the scene changes mid-dialogue.
        if (dialogueActive)
        {
            dialogueActive = false;
            IsInDialogue = false;
        }
    }

    private void EndDialogue()
    {
        dialogueActive = false;
        IsInDialogue = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void Interact()
    {
        QuestManager manager = QuestManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("QuestManager was not found.");
            return;
        }

        // CASE 1: Start a new quest.
        if (startsQuest && manager.ActiveQuest == null)
        {
            if (!manager.StartQuest(questID))
                return;

            Debug.Log($"{npcName}: A new quest has started!");

            if (!string.IsNullOrWhiteSpace(objectiveID))
                manager.CompleteCurrentObjective(objectiveID);

            return;
        }

        // Verify the active quest.
        if (manager.ActiveQuest == null ||
            manager.ActiveQuest.questID != questID)
        {
            Debug.Log(
                $"{npcName}: I have nothing for you right now. " +
                $"(my questID = '{questID}', active quest = " +
                $"'{(manager.ActiveQuest != null ? manager.ActiveQuest.questID : "none")}')"
            );
            return;
        }

        // CASE 2: Give an item to the player.
        if (!string.IsNullOrWhiteSpace(rewardItemID))
        {
            InventoryManager inventory = InventoryManager.Instance;

            if (inventory == null)
            {
                Debug.LogWarning("InventoryManager was not found.");
                return;
            }

            if (hasGivenReward || inventory.HasItem(rewardItemID))
            {
                Debug.Log($"{npcName}: You already have that item.");
                return;
            }

            if (manager.CompleteCurrentObjective(objectiveID))
            {
                inventory.AddItem(rewardItemID);
                hasGivenReward = true;

                Debug.Log($"{npcName}: Here is the {rewardItemID}!");
            }

            return;
        }

        // CASE 3: Receive a required item.
        if (!string.IsNullOrWhiteSpace(requiredItemID))
        {
            InventoryManager inventory = InventoryManager.Instance;

            if (inventory == null)
            {
                Debug.LogWarning("InventoryManager was not found.");
                return;
            }

            if (!inventory.HasItem(requiredItemID))
            {
                Debug.Log(
                    $"{npcName}: You need to bring me {requiredItemID}."
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(deliveryObjectiveID))
            {
                Debug.LogWarning(
                    $"{npcName}: Delivery Objective ID is not configured."
                );
                return;
            }

            if (manager.CompleteCurrentObjective(deliveryObjectiveID))
            {
                inventory.RemoveItem(requiredItemID);

                Debug.Log(
                    $"{npcName}: Thank you for the {requiredItemID}!"
                );
            }

            return;
        }

        // CASE 4: Complete a normal interaction objective.
        if (!string.IsNullOrWhiteSpace(objectiveID))
        {
            if (manager.CompleteCurrentObjective(objectiveID))
                Debug.Log($"{npcName}: Interaction completed.");

            return;
        }

        Debug.Log($"{npcName}: Hello!");
    }
}