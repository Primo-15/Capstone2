
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestLogUI : MonoBehaviour
{
    [Header("Main Quest Log")]
    [SerializeField] private GameObject questLogPanel;
    [SerializeField] private TMP_Text questLogTitleText;
    [SerializeField] private TMP_Text questLogHintText;

    [Header("Quest List")]
    [SerializeField] private GameObject questListPanel;
    [SerializeField] private TMP_Text questListTitleText;
    [SerializeField] private TMP_Text questListContent;

    [Header("Quest Details")]
    [SerializeField] private TMP_Text questDetailsTitleText;
    [SerializeField] private TMP_Text questDetailsDescriptionText;
    [SerializeField] private TMP_Text questDetailsStatusText;
    [SerializeField] private TMP_Text questDetailsObjectiveText;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.J;

    [Header("Quest Button Appearance")]
    [SerializeField] private float buttonHeight = 46f;
    [SerializeField] private float buttonSpacing = 6f;
    [SerializeField] private float firstButtonOffset = 55f;
    [SerializeField] private int buttonFontSize = 16;

    private QuestManager questManager;
    private bool isSubscribed;

    private readonly List<GameObject> questButtons =
        new List<GameObject>();

    private string selectedQuestID;

    private void Start()
    {
        questManager = QuestManager.Instance;

        if (questManager == null)
        {
            questManager = FindFirstObjectByType<QuestManager>();
        }

        if (questManager == null)
        {
            Debug.LogError(
                "QuestLogUI: No active QuestManager was found."
            );

            enabled = false;
            return;
        }

        questManager.OnQuestStarted += HandleQuestStarted;
        questManager.OnObjectiveChanged += HandleObjectiveChanged;
        questManager.OnQuestCompleted += HandleQuestCompleted;

        isSubscribed = true;

        questLogTitleText.text = "QUEST LOG";
        questLogHintText.text = "Press J to close";
        questListTitleText.text = "AVAILABLE QUESTS";

        if (questListContent != null)
        {
            questListContent.gameObject.SetActive(false);
        }

        questLogPanel.SetActive(false);

        RefreshQuestLog();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            bool shouldOpen = !questLogPanel.activeSelf;

            questLogPanel.SetActive(shouldOpen);

            if (shouldOpen)
            {
                // Unlock and show the cursor for UI interaction.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                questLogHintText.text = "Press J to close";
                RefreshQuestLog();
            }
            else
            {
                // Restore the normal third-person gameplay cursor.
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (questManager == null || !isSubscribed)
        {
            return;
        }

        questManager.OnQuestStarted -= HandleQuestStarted;
        questManager.OnObjectiveChanged -= HandleObjectiveChanged;
        questManager.OnQuestCompleted -= HandleQuestCompleted;
    }

    private void HandleQuestStarted(QuestData quest)
    {
        RefreshQuestLog();
    }

    private void HandleObjectiveChanged(
        QuestData quest,
        int objectiveIndex)
    {
        RefreshQuestLog();
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        RefreshQuestLog();
    }

    private void RefreshQuestLog()
    {
        if (questManager == null)
        {
            return;
        }

        ClearQuestButtons();

        QuestData[] quests = questManager.AvailableQuests;
        QuestData questToSelect = null;

        if (quests != null)
        {
            foreach (QuestData quest in quests)
            {
                if (quest == null)
                {
                    continue;
                }

                CreateQuestButton(quest);

                if (quest.questID == selectedQuestID)
                {
                    questToSelect = quest;
                }
            }
        }

        // Keep the selected quest if it still exists.
        // Otherwise, select the first available entry in the list.
        if (questToSelect == null && quests != null)
        {
            foreach (QuestData quest in quests)
            {
                if (quest != null)
                {
                    questToSelect = quest;
                    break;
                }
            }
        }

        if (questToSelect != null)
        {
            SelectQuest(questToSelect);
        }
        else
        {
            selectedQuestID = null;
            ShowNoQuests();
        }
    }

    private void CreateQuestButton(QuestData quest)
    {
        GameObject buttonObject = new GameObject(
            "QuestButton_" + quest.questID,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        buttonObject.transform.SetParent(
            questListPanel.transform,
            false
        );

        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();

        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);

        int index = questButtons.Count;

        buttonRect.anchoredPosition = new Vector2(
            0f,
            -firstButtonOffset - index * (buttonHeight + buttonSpacing)
        );

        buttonRect.sizeDelta = new Vector2(
            -16f,
            buttonHeight
        );

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.16f, 0.19f, 0.24f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject labelObject = new GameObject(
            "QuestButtonLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        labelObject.transform.SetParent(
            buttonObject.transform,
            false
        );

        RectTransform labelRect =
            labelObject.GetComponent<RectTransform>();

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);

        TextMeshProUGUI label =
            labelObject.GetComponent<TextMeshProUGUI>();

        label.text = GetQuestButtonLabel(quest);
        label.fontSize = buttonFontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        // Capture this specific quest for the button callback.
        QuestData selectedQuest = quest;

        button.onClick.AddListener(
            () => SelectQuest(selectedQuest)
        );

        questButtons.Add(buttonObject);
    }

    private string GetQuestButtonLabel(QuestData quest)
    {
        if (questManager.IsQuestCompleted(quest.questID))
        {
            return "[Completed] " + quest.questTitle;
        }

        if (questManager.ActiveQuest != null &&
            questManager.ActiveQuest.questID == quest.questID)
        {
            return "[In Progress] " + quest.questTitle;
        }

        if (!ArePrerequisitesMet(quest))
        {
            return "[Locked] " + quest.questTitle;
        }

        return quest.questTitle;
    }

    private void SelectQuest(QuestData quest)
    {
        if (quest == null)
        {
            return;
        }

        selectedQuestID = quest.questID;

        questDetailsTitleText.text = quest.questTitle;
        questDetailsDescriptionText.text = quest.questDescription;

        UpdateQuestStatus(quest);
        UpdateQuestObjectives(quest);

        HighlightSelectedButton();
    }

    private void UpdateQuestStatus(QuestData quest)
    {
        if (questManager.IsQuestCompleted(quest.questID))
        {
            questDetailsStatusText.text = "Status: COMPLETED";
            questDetailsStatusText.color =
                new Color(0.35f, 0.9f, 0.55f);
        }
        else if (questManager.ActiveQuest != null &&
                 questManager.ActiveQuest.questID == quest.questID)
        {
            questDetailsStatusText.text = "Status: IN PROGRESS";
            questDetailsStatusText.color =
                new Color(1f, 0.82f, 0.3f);
        }
        else if (!ArePrerequisitesMet(quest))
        {
            questDetailsStatusText.text =
                "Status: LOCKED\nComplete the required quests first.";

            questDetailsStatusText.color =
                new Color(1f, 0.55f, 0.45f);
        }
        else if (questManager.ActiveQuest != null)
        {
            questDetailsStatusText.text =
                "Status: AVAILABLE\nFinish your current quest before starting this one.";

            questDetailsStatusText.color = Color.white;
        }
        else
        {
            questDetailsStatusText.text =
                "Status: AVAILABLE\nTalk to the quest giver to begin.";

            questDetailsStatusText.color = Color.white;
        }
    }

    private void UpdateQuestObjectives(QuestData quest)
    {
        if (quest.objectives == null || quest.objectives.Length == 0)
        {
            questDetailsObjectiveText.text = "No objectives configured.";
            return;
        }

        bool isCompleted =
            questManager.IsQuestCompleted(quest.questID);

        bool isActive =
            questManager.ActiveQuest != null &&
            questManager.ActiveQuest.questID == quest.questID;

        bool isLocked = !isCompleted &&
                        !isActive &&
                        !ArePrerequisitesMet(quest);

        int currentIndex = isActive
            ? questManager.CurrentObjectiveIndex
            : 0;

        StringBuilder text = new StringBuilder();
        text.AppendLine("OBJECTIVES");
        text.AppendLine();

        for (int i = 0; i < quest.objectives.Length; i++)
        {
            QuestObjectiveData objective = quest.objectives[i];

            if (objective == null)
            {
                continue;
            }

            if (isCompleted || (isActive && i < currentIndex))
            {
                text.Append("<color=#59E68C>/  ");
                text.Append(objective.description);
                text.AppendLine("</color>");
            }
            else if (isActive && i == currentIndex)
            {
                text.Append("<color=#FFD65A>>  ");
                text.Append(objective.description);
                text.AppendLine("</color>");
            }
            else if (isLocked)
            {
                text.Append("<color=#999999>X  ");
                text.Append(objective.description);
                text.AppendLine("</color>");
            }
            else
            {
                text.Append("<color=#C0C5CC>O  ");
                text.Append(objective.description);
                text.AppendLine("</color>");
            }

            text.AppendLine();
        }

        questDetailsObjectiveText.text = text.ToString();
    }

    private bool ArePrerequisitesMet(QuestData quest)
    {
        if (quest.prerequisiteQuestIDs == null)
        {
            return true;
        }

        foreach (string prerequisiteID in quest.prerequisiteQuestIDs)
        {
            if (!questManager.IsQuestCompleted(prerequisiteID))
            {
                return false;
            }
        }

        return true;
    }

    private void HighlightSelectedButton()
    {
        foreach (GameObject buttonObject in questButtons)
        {
            if (buttonObject == null)
            {
                continue;
            }

            Image background = buttonObject.GetComponent<Image>();

            bool isSelected =
                buttonObject.name == "QuestButton_" + selectedQuestID;

            background.color = isSelected
                ? new Color(0.28f, 0.36f, 0.46f, 1f)
                : new Color(0.16f, 0.19f, 0.24f, 1f);
        }
    }

    private void ClearQuestButtons()
    {
        foreach (GameObject buttonObject in questButtons)
        {
            if (buttonObject != null)
            {
                Destroy(buttonObject);
            }
        }

        questButtons.Clear();
    }

    private void ShowNoQuests()
    {
        questDetailsTitleText.text = "No Quests";
        questDetailsDescriptionText.text =
            "There are currently no quests in the quest database.";
        questDetailsStatusText.text = "";
        questDetailsObjectiveText.text = "";
    }
}
