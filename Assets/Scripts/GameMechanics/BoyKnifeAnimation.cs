using UnityEngine;

/// <summary>
/// Keeps an NPC seated (Sitting Idle) until the existing quest objective that
/// represents handing over the knife is completed, then lets the Animator move
/// to the standing idle. Uses the existing QuestManager state, so the result is
/// also correct after a save/load or when the scene is re-entered.
/// </summary>
[RequireComponent(typeof(Animator))]
public class BoyKnifeAnimation : MonoBehaviour
{
    private const string StandingStateName = "Man Idle 1";

    [Header("Quest (delivery of the knife)")]
    [SerializeField] private string questID = "main_quest_03";
    [SerializeField] private string handoverObjectiveID = "Free_Pepe";

    [Header("Animator")]
    [SerializeField] private string receivedKnifeParameter = "HasReceivedKnife";

    private Animator animator;
    private QuestManager subscribedManager;
    private bool standing;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Boy must stay where he was placed while sitting.
        animator.applyRootMotion = false;
    }

    private void Start()
    {
        Subscribe();

        if (IsHandoverComplete())
            StandUp(true);
    }

    private void OnEnable()
    {
        if (subscribedManager == null && QuestManager.Instance != null)
            Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (QuestManager.Instance == null || subscribedManager == QuestManager.Instance)
            return;

        Unsubscribe();
        subscribedManager = QuestManager.Instance;
        subscribedManager.OnObjectiveChanged += HandleObjectiveChanged;
        subscribedManager.OnQuestCompleted += HandleQuestCompleted;
    }

    private void Unsubscribe()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnObjectiveChanged -= HandleObjectiveChanged;
        subscribedManager.OnQuestCompleted -= HandleQuestCompleted;
        subscribedManager = null;
    }

    private void HandleObjectiveChanged(QuestData quest, int index)
    {
        if (!standing && IsHandoverComplete())
            StandUp(false);
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        if (!standing && IsHandoverComplete())
            StandUp(false);
    }

    private bool IsHandoverComplete()
    {
        QuestManager qm = QuestManager.Instance;
        if (qm == null)
            return false;

        if (qm.IsQuestCompleted(questID))
            return true;

        QuestData active = qm.ActiveQuest;
        if (active == null || active.questID != questID || active.objectives == null)
            return false;

        for (int i = 0; i < active.objectives.Length; i++)
        {
            if (active.objectives[i].objectiveID == handoverObjectiveID)
                return qm.CurrentObjectiveIndex > i;
        }

        return false;
    }

    private void StandUp(bool instant)
    {
        if (standing || animator == null)
            return;

        standing = true;
        animator.SetBool(receivedKnifeParameter, true);

        if (instant)
        {
            // Already handed over earlier: start directly in the standing idle.
            animator.Play(StandingStateName, 0, 0f);
            animator.Update(0f);
        }
    }
}

