using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestTrackerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject questTrackerPanel;
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questObjectiveText;

    [Header("Checklist Settings")]
    [SerializeField] private float objectiveSpacing = 35f;

    [Header("Completion Notification")]
    [SerializeField] private float completionDisplayDuration = 4f;

    // Only used when there is no next questline to show at all
    [Header("No Quest Available")]
    [SerializeField] private string noQuestTitle = "Quest";
    [SerializeField] private string noQuestMessage = "No active quest";

    private QuestManager questManager;
    private bool isSubscribed;
    private Coroutine hideCoroutine;

    private readonly List<TMP_Text> objectiveTexts =
        new List<TMP_Text>();

    private void Awake()
    {
        if (gameObject.scene != SceneManager.GetActiveScene())
        {
            if (questTrackerPanel != null)
                questTrackerPanel.SetActive(false);
            gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (gameObject.scene != SceneManager.GetActiveScene())
        {
            if (questTrackerPanel != null)
                questTrackerPanel.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        questManager = QuestManager.Instance;

        if (questManager == null)
            questManager = FindFirstObjectByType<QuestManager>();

        if (questManager == null)
        {
            Debug.LogError(
                "QuestTrackerUI: No active QuestManager was found."
            );

            questTrackerPanel.SetActive(false);
            return;
        }

        questManager.OnQuestStarted += HandleQuestStarted;
        questManager.OnObjectiveChanged += HandleObjectiveChanged;
        questManager.OnQuestCompleted += HandleQuestCompleted;
        questManager.OnObjectiveProgressChanged += HandleObjectiveProgressChanged;
        isSubscribed = true;

        questObjectiveText.gameObject.SetActive(false);

        if (questManager.ActiveQuest != null)
        {
            HandleObjectiveChanged(
                questManager.ActiveQuest,
                questManager.CurrentObjectiveIndex
            );
        }
        else
        {
            ShowNoActiveQuest();
        }
    }

    private void OnDestroy()
    {
        if (questManager != null && isSubscribed)
        {
            questManager.OnQuestStarted -= HandleQuestStarted;
            questManager.OnObjectiveChanged -= HandleObjectiveChanged;
            questManager.OnQuestCompleted -= HandleQuestCompleted;
            questManager.OnObjectiveProgressChanged -= HandleObjectiveProgressChanged;
        }
    }

    private void HandleQuestStarted(QuestData quest)
    {
        CancelHideCoroutine();
        UpdateQuestDisplay(quest, 0);
    }

    private void HandleObjectiveChanged(
        QuestData quest,
        int objectiveIndex)
    {
        CancelHideCoroutine();
        UpdateQuestDisplay(quest, objectiveIndex);
    }

    private void UpdateQuestDisplay(
        QuestData quest,
        int objectiveIndex)
    {
        if (quest == null)
        {
            ShowNoActiveQuest();
            return;
        }

        questTrackerPanel.SetActive(true);
        questTitleText.text = quest.questTitle;

        ClearObjectiveEntries();

        if (quest.objectives == null ||
            quest.objectives.Length == 0)
        {
            return;
        }

        // Only show objectives the player has reached: the ones already
        // completed plus the current one. Later objectives stay hidden
        // until the previous one is finished.
        int visibleCount = Mathf.Min(
            objectiveIndex + 1,
            quest.objectives.Length
        );

        for (int i = 0; i < visibleCount; i++)
        {
            QuestObjectiveData objective = quest.objectives[i];

            TMP_Text entry = CreateObjectiveEntry(i);

            if (i < objectiveIndex)
            {
                entry.text = "/  " + objective.description;
                entry.color = new Color(0.35f, 0.9f, 0.55f);
            }
            else if (i == objectiveIndex)
            {
                entry.text = ">  " + objective.description;
                entry.color = new Color(1f, 0.82f, 0.3f);
            }
            else
            {
                entry.text = "○  " + objective.description;
                entry.color = new Color(0.65f, 0.68f, 0.72f);
            }

            objectiveTexts.Add(entry);
        }
    }

    private TMP_Text CreateObjectiveEntry(int index)
    {
        TMP_Text entry = Instantiate(
            questObjectiveText,
            questObjectiveText.transform.parent
        );

        entry.name = "Objective_" + index;
        entry.gameObject.SetActive(true);

        RectTransform rect = entry.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(
            0f,
            -50f - (index * objectiveSpacing)
        );
        rect.sizeDelta = new Vector2(-20f, objectiveSpacing);

        return entry;
    }


    private void ClearObjectiveEntries()
    {
        foreach (TMP_Text entry in objectiveTexts)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        objectiveTexts.Clear();
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        CancelHideCoroutine();

        questTrackerPanel.SetActive(true);
        questTitleText.text = quest.questTitle;

        ClearObjectiveEntries();

        int objectiveCount = 0;

        if (quest.objectives != null)
        {
            objectiveCount = quest.objectives.Length;

            for (int i = 0; i < objectiveCount; i++)
            {
                TMP_Text entry = CreateObjectiveEntry(i);

                entry.text = "✓  " + quest.objectives[i].description;
                entry.color = new Color(0.35f, 0.9f, 0.55f);

                objectiveTexts.Add(entry);
            }
        }

        // Move the completion message below the checklist.
        questObjectiveText.gameObject.SetActive(true);
        questObjectiveText.text = "QUEST COMPLETED!";
        questObjectiveText.color = new Color(1f, 0.82f, 0.3f);

        RectTransform messageRect = questObjectiveText.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 1f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(
            0f,
            -50f - (objectiveCount * objectiveSpacing)
        );
        messageRect.sizeDelta = new Vector2(-20f, objectiveSpacing);

        hideCoroutine = StartCoroutine(HideTrackerAfterDelay());
    }

    // After the completion message, move on to the next questline's preview.
    private IEnumerator HideTrackerAfterDelay()
    {
        yield return new WaitForSeconds(completionDisplayDuration);

        hideCoroutine = null;

        // Only fall back if no new quest started meanwhile.
        if (questManager == null || questManager.ActiveQuest == null)
            ShowNoActiveQuest();
    }

    private void CancelHideCoroutine()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        // Restore the template for the next quest.
        questObjectiveText.text = "Current objective appears here.";
        questObjectiveText.color = Color.white;
        questObjectiveText.gameObject.SetActive(false);
    }

    // CHANGED: finds the first quest that is not completed and whose
    // prerequisites are done (the next main questline to do).
    private QuestData FindNextQuest()
    {
        if (questManager == null || questManager.AvailableQuests == null)
            return null;

        foreach (QuestData quest in questManager.AvailableQuests)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.questID))
                continue;

            if (quest.objectives == null || quest.objectives.Length == 0)
                continue;

            if (questManager.IsQuestCompleted(quest.questID))
                continue;

            if (!questManager.ArePrerequisitesMet(quest.questID))
                continue;

            return quest;
        }

        return null;
    }

    // CHANGED: with no active quest, preview the next questline (its title and
    // first objective). The placeholder only shows if there is no next quest.
    private void ShowNoActiveQuest()
    {
        CancelHideCoroutine();
        ClearObjectiveEntries();

        questTrackerPanel.SetActive(true);

        QuestData next = FindNextQuest();

        if (next != null)
        {
            questTitleText.text = next.questTitle;

            TMP_Text entry = CreateObjectiveEntry(0);
            entry.text = ">  " + next.objectives[0].description;
            entry.color = new Color(1f, 0.82f, 0.3f);
            objectiveTexts.Add(entry);
            return;
        }

        questTitleText.text = noQuestTitle;

        TMP_Text placeholder = CreateObjectiveEntry(0);
        placeholder.text = noQuestMessage;
        placeholder.color = new Color(0.65f, 0.68f, 0.72f);
        objectiveTexts.Add(placeholder);
    }

    private void HandleObjectiveProgressChanged(
    QuestData quest,
    int currentProgress,
    int requiredAmount)
    {
        if (quest == null || questManager.ActiveQuest != quest)
            return;

        UpdateQuestDisplay(quest, questManager.CurrentObjectiveIndex);

        int currentIndex = questManager.CurrentObjectiveIndex;

        if (currentIndex < quest.objectives.Length &&
            currentIndex < objectiveTexts.Count)
        {
            TMP_Text currentEntry = objectiveTexts[currentIndex];

            if (requiredAmount > 1)
            {
                currentEntry.text =
                    "➤  " +
                    quest.objectives[currentIndex].description +
                    $" ({currentProgress}/{requiredAmount})";
            }
        }
    }
}