
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Definitions")]
    [SerializeField] private QuestData[] availableQuests;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuScene = "Main Menu";

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
            // Only adopt quests from the active scene (ignore additive background scenes like M5 during M4 cinematics)
            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                Instance.SetSceneQuests(availableQuests);
                Instance.ApplyPendingSave();
            }

            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildQuestDatabase();
        ApplyPendingSave();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;

        bool loadedByContinue = SaveSystem.PendingSpawn != null;

        ApplyPendingSave();

        // Returning to the main menu clears the current session.
        // The saved map checkpoint is NOT deleted.
        if (scene.name == mainMenuScene)
        {
            ResetProgress();
            return;
        }

        // Continue must not create or overwrite a checkpoint.
        if (loadedByContinue)
            return;

        // A successful map transition starts the new map fresh.
        if (SaveSystem.PendingMapTransition &&
            scene.name == SaveSystem.PendingMapScene)
        {
            SaveSystem.ClearPendingMapTransition();
            ResetProgress();

            Debug.Log("New map loaded. Quest progress reset.");
        }
    }

    private void BuildQuestDatabase()
    {
        questDatabase.Clear();

        if (availableQuests == null)
            return;

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null ||
                string.IsNullOrWhiteSpace(quest.questID))
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

    public void SetSceneQuests(QuestData[] sceneQuests)
    {
        if (sceneQuests == null || sceneQuests.Length == 0)
            return;

        var list = new List<QuestData>();

        foreach (QuestData quest in sceneQuests)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.questID))
                continue;

            bool idExists = false;
            foreach (QuestData existing in list)
            {
                if (existing != null && existing.questID == quest.questID)
                {
                    idExists = true;
                    break;
                }
            }

            if (!idExists)
                list.Add(quest);
        }

        if (list.Count == 0)
            return;

        availableQuests = list.ToArray();
        BuildQuestDatabase();

        // If an active quest from a previous scene does not belong to this scene, clear it
        if (activeQuest != null && !questDatabase.ContainsKey(activeQuest.questID))
        {
            activeQuest = null;
            currentObjectiveIndex = 0;
            currentObjectiveProgress = 0;
        }
    }

    public void MergeQuests(QuestData[] newQuests)
    {
        if (newQuests == null)
            return;

        var list = new List<QuestData>(
            availableQuests ?? new QuestData[0]);

        bool changed = false;

        foreach (QuestData quest in newQuests)
        {
            if (quest == null ||
                string.IsNullOrWhiteSpace(quest.questID))
                continue;

            if (list.Contains(quest))
                continue;

            bool idExists = false;

            foreach (QuestData existing in list)
            {
                if (existing != null &&
                    existing.questID == quest.questID)
                {
                    idExists = true;
                    break;
                }
            }

            if (idExists)
                continue;

            list.Add(quest);
            changed = true;
        }

        if (!changed)
            return;

        availableQuests = list.ToArray();
        BuildQuestDatabase();
    }

    // ---------- Save / Load / Reset ----------

    public SaveData CreateSaveData()
    {
        SaveData data = new SaveData();

        data.sceneName = SceneManager.GetActiveScene().name;
        data.completedQuestIds =
            new List<string>(completedQuestIDs);

        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            data.hasPosition = true;
            data.playerPosition = player.transform.position;
            data.playerYRotation =
                player.transform.eulerAngles.y;
        }
        else
        {
            Debug.LogWarning(
                "No GameObject tagged 'Player' found."
            );
        }

        return data;
    }

    public void ApplyPendingSave()
    {
        SaveData data = SaveSystem.Pending;

        if (data == null)
            return;

        completedQuestIDs.Clear();

        if (data.completedQuestIds != null)
        {
            foreach (string id in data.completedQuestIds)
                completedQuestIDs.Add(id);
        }

        activeQuest = null;
        currentObjectiveIndex = 0;
        currentObjectiveProgress = 0;

        SaveSystem.Pending = null;

        Debug.Log(
            $"Save restored: {completedQuestIDs.Count} completed quest(s)."
        );
    }

    public void ResetProgress()
    {
        completedQuestIDs.Clear();

        activeQuest = null;
        currentObjectiveIndex = 0;
        currentObjectiveProgress = 0;

        Debug.Log("Quest progress reset.");
    }

    // ---------- Quest logic ----------

    public bool StartQuest(string questID)
    {
        if (activeQuest != null)
        {
            Debug.LogWarning(
                "Finish the active quest before starting another."
            );
            return false;
        }

        if (!questDatabase.TryGetValue(
            questID, out QuestData quest))
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
            foreach (string prerequisiteID in
                quest.prerequisiteQuestIDs)
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

        if (quest.objectives == null ||
            quest.objectives.Length == 0)
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

        OnObjectiveChanged?.Invoke(
            activeQuest, currentObjectiveIndex
        );

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

        if (currentObjectiveIndex >=
            activeQuest.objectives.Length)
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

        if (objective == null ||
            objective.objectiveID != objectiveID)
        {
            Debug.Log(
                $"Current objective is: " +
                $"{objective?.description ?? "Unknown objective"}"
            );
            return false;
        }

        int requiredAmount =
            Mathf.Max(1, objective.requiredAmount);

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
            CompleteCurrentObjective();

        return true;
    }

    // Compatibility method for existing NPC scripts.
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

        OnObjectiveChanged?.Invoke(
            quest, currentObjectiveIndex
        );

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

        Debug.Log(
            $"Quest completed: {finishedQuest.questTitle}"
        );

        activeQuest = null;
        currentObjectiveIndex = 0;
        currentObjectiveProgress = 0;

        // IMPORTANT:
        // Quest completion never saves the game.

        OnQuestCompleted?.Invoke(finishedQuest);
    }

    public bool ArePrerequisitesMet(string questID)
    {
        if (!questDatabase.TryGetValue(
            questID, out QuestData quest))
            return true;

        if (quest.prerequisiteQuestIDs == null)
            return true;

        foreach (string prerequisiteID in
            quest.prerequisiteQuestIDs)
        {
            if (!completedQuestIDs.Contains(prerequisiteID))
                return false;
        }

        return true;
    }

    public bool IsQuestCompleted(string questID)
    {
        return completedQuestIDs.Contains(questID);
    }

    public int GetVisibleObjectiveCount(QuestData quest)
    {
        if (quest == null || quest.objectives == null)
            return 0;

        if (completedQuestIDs.Contains(quest.questID))
            return quest.objectives.Length;

        if (quest == activeQuest)
        {
            return Mathf.Min(
                currentObjectiveIndex + 1,
                quest.objectives.Length
            );
        }

        return Mathf.Min(1, quest.objectives.Length);
    }
}