using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoStartQuest : MonoBehaviour
{
    [SerializeField] private string questID = "main_quest_04";

    private IEnumerator Start()
    {
        if (gameObject.scene != SceneManager.GetActiveScene())
            yield break;

        // Wait a moment so the quest HUD and log have subscribed to the events.
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);

        QuestManager manager = QuestManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("AutoStartQuest: no QuestManager found.", this);
            yield break;
        }

        if (manager.IsQuestCompleted(questID))
            yield break; // already done, nothing to start

        if (manager.ActiveQuest != null)
        {
            if (manager.ActiveQuest.questID == questID)
                Debug.Log("AutoStartQuest: quest is already active.", this);
            else
                Debug.LogWarning(
                    "AutoStartQuest: another quest is still active (" +
                    manager.ActiveQuest.questTitle + ").", this);

            yield break;
        }

        manager.StartQuest(questID);
    }
}