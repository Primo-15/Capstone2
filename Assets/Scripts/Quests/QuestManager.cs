
using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Definitions")]
    [SerializeField] private QuestData[] availableQuests;

    private readonly Dictionary<string, QuestData> questDatabase =
        new Dictionary<string, QuestData>();

    private readonly HashSet<string> completedQuestIDs =
        new HashSet<string>();

    private QuestData activeQuest;
    private int currentObjectiveIndex;
    private int currentObjectiveProgress;

    public event Action<QuestData, int> OnObjectiveChanged;
    public event Action<QuestData> OnQuestStarted;
    public event Action<QuestData> OnQuestCompleted;
    public event Action<QuestData, int, int> OnObjectiveProgressChanged;

    public QuestData ActiveQuest => activeQuest;
    public int CurrentObjectiveIndex => currentObjectiveIndex;
    public int CurrentObjectiveProgress => currentObjectiveProgress;
    public QuestData[] AvailableQuests => availableQuests;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildQuestDatabase();
    }

    private void BuildQuestDatabase()
    {
        questDatabase.Clear();

        if (availableQuests == null)
            return;

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.questID))
            {
                Debug.LogWarning(
                    "A quest is missing its data or quest ID."
                );
                continue;
            }

            if (questDatabase.ContainsKey(quest.questID))
            {
                Debug.LogWarning(
                    $"Duplicate quest ID found: {quest.questID}"
                );
                continue;
            }

            questDatabase.Add(quest.questID, quest);
        }
    }

    public bool StartQuest(string questID)
    {
        if (activeQuest != null)
        {
            Debug.LogWarning(
                "Finish the active quest before starting another."
            );
            return false;
        }

        if (!questDatabase.TryGetValue(questID, out QuestData quest))
        {
            Debug.LogWarning($"Quest not found: {questID}");
            return false;
        }

        if (completedQuestIDs.Contains(questID))
        {
            Debug.Log("This quest has already been completed.");
            return false;
        }

        if (quest.prerequisiteQuestIDs != null)
        {
            foreach (string prerequisiteID in quest.prerequisiteQuestIDs)
            {
                if (!completedQuestIDs.Contains(prerequisiteID))
                {
                    Debug.Log(
                        $"Complete {prerequisiteID} before starting " +
                        $"{quest.questTitle}."
                    );
                    return false;
                }
            }
        }

        if (quest.objectives == null || quest.objectives.Length == 0)
        {
            Debug.LogWarning(
                $"Quest {quest.questTitle} has no objectives."
            );
            return false;
        }

        activeQuest = quest;
        currentObjectiveIndex = 0;
        currentObjectiveProgress = 0;

        Debug.Log($"Quest started: {activeQuest.questTitle}");

        OnQuestStarted?.Invoke(activeQuest);
        OnObjectiveChanged?.Invoke(activeQuest, currentObjectiveIndex);
        OnObjectiveProgressChanged?.Invoke(
            activeQuest,
            currentObjectiveProgress,
            activeQuest.objectives[0].requiredAmount
        );

        return true;
    }

    public bool AddObjectiveProgress(
        string objectiveID,
        int amount = 1)
    {
        if (activeQuest == null)
            return false;

        if (currentObjectiveIndex >= activeQuest.objectives.Length)
            return false;

        if (amount <= 0)
        {
            Debug.LogWarning(
                "Objective progress amount must be greater than zero."
            );
            return false;
        }

        QuestObjectiveData objective =
            activeQuest.objectives[currentObjectiveIndex];

        if (objective == null || objective.objectiveID != objectiveID)
        {
            Debug.Log(
                $"Current objective is: " +
                $"{objective?.description ?? "Unknown objective"}"
            );
            return false;
        }

        int requiredAmount = Mathf.Max(1, objective.requiredAmount);

        currentObjectiveProgress = Mathf.Min(
            currentObjectiveProgress + amount,
            requiredAmount
        );

        Debug.Log(
            $"Objective progress: {objective.description} " +
            $"({currentObjectiveProgress}/{requiredAmount})"
        );

        OnObjectiveProgressChanged?.Invoke(
            activeQuest,
            currentObjectiveProgress,
            requiredAmount
        );

        if (currentObjectiveProgress >= requiredAmount)
        {
            CompleteCurrentObjective();
        }

        return true;
    }

    // Compatibility method for existing NPC scripts.
    // The amount represents progress to add, not a total target.
    public bool CompleteCurrentObjective(
        string objectiveID,
        int amount = 1)
    {
        return AddObjectiveProgress(objectiveID, amount);
    }

    private void CompleteCurrentObjective()
    {
        QuestData quest = activeQuest;

        Debug.Log(
            $"Objective completed: " +
            $"{quest.objectives[currentObjectiveIndex].description}"
        );

        currentObjectiveIndex++;
        currentObjectiveProgress = 0;

        if (currentObjectiveIndex >= quest.objectives.Length)
        {
            FinishActiveQuest();
            return;
        }

        OnObjectiveChanged?.Invoke(quest, currentObjectiveIndex);

        QuestObjectiveData nextObjective =
            quest.objectives[currentObjectiveIndex];

        OnObjectiveProgressChanged?.Invoke(
            quest,
            0,
            Mathf.Max(1, nextObjective.requiredAmount)
        );
    }

    private void FinishActiveQuest()
    {
        QuestData finishedQuest = activeQuest;

        completedQuestIDs.Add(finishedQuest.questID);

        Debug.Log($"Quest completed: {finishedQuest.questTitle}");

        activeQuest = null;
        currentObjectiveIndex = 0;
        currentObjectiveProgress = 0;

        OnQuestCompleted?.Invoke(finishedQuest);
    }

    public bool IsQuestCompleted(string questID)
    {
        return completedQuestIDs.Contains(questID);
    }
}
