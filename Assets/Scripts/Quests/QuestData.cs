
using System;
using UnityEngine;

public enum QuestType
{
    Main,
    Side
}

public enum QuestObjectiveType
{
    Interact,
    Collect,
    ReachLocation
}

[Serializable]
public class QuestObjectiveData
{
    [Header("Objective Information")]
    public string objectiveID;
    public string description;

    [Header("Objective Settings")]
    public QuestObjectiveType objectiveType = QuestObjectiveType.Interact;

    [Min(1)]
    public int requiredAmount = 1;
}

[CreateAssetMenu(
    fileName = "NewQuest",
    menuName = "Quests/Quest Data"
)]
public class QuestData : ScriptableObject
{
    [Header("Quest Information")]
    public string questID;
    public string questTitle;

    [TextArea(3, 5)]
    public string questDescription;

    public QuestType questType = QuestType.Main;

    [Header("Quest Requirements")]
    [Tooltip("Quest IDs that must be completed before this quest becomes available.")]
    public string[] prerequisiteQuestIDs;

    [Header("Quest Objectives")]
    public QuestObjectiveData[] objectives;
}
