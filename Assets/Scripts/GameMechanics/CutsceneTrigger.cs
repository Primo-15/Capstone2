using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class CutsceneTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;

    [Header("Pan Cutscene")]
    [Tooltip("Optional. If assigned, the player is locked until this cutscene finishes.")]
    [SerializeField] private CameraPanCutscene cutscene;

    [Header("Extra Events")]
    [Tooltip("Runs the moment the player enters. Don't also call the cutscene here, or it plays twice.")]
    [SerializeField] private UnityEvent onPlayerEnter;
    [SerializeField] private UnityEvent onCutsceneFinished;

    private bool used;
    private bool playing;

    private void OnTriggerEnter(Collider other)
    {
        TryTriggerCutscene(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (used && triggerOnce) return;
        if (playing) return;
        if (QuestNPCInteraction.IsInDialogue) return;

        TryTriggerCutscene(other);
    }

    private void TryTriggerCutscene(Collider other)
    {
        if (used && triggerOnce) return;
        if (playing) return;
        if (other == null || !other.CompareTag(playerTag)) return;
        if (MapVideoController.IsVideoPlaying) return;

        // If this GameObject also has a QuestLocationTrigger (e.g. Village Destination / Village Destination2 in M5),
        // only allow the cutscene and its events to fire when that quest objective is currently active and eligible.
        QuestLocationTrigger locationTrigger = GetComponent<QuestLocationTrigger>();
        if (locationTrigger != null)
        {
            locationTrigger.TryTrigger(other);
            if (!locationTrigger.DidTriggerThisFrame)
                return;
        }

        used = true;
        onPlayerEnter.Invoke();

        if (cutscene == null) return;

        playing = true;

        // Locks player movement and blocks NPC dialogue until the pan ends.
        QuestNPCInteraction.SetCutsceneLock(true);
        cutscene.Play(OnCutsceneDone);
    }

    private void OnCutsceneDone()
    {
        playing = false;
        QuestNPCInteraction.SetCutsceneLock(false);
        onCutsceneFinished.Invoke();
    }

    private void OnDisable()
    {
        // Safety: never leave the player frozen if this object is
        // disabled or the scene changes mid-cutscene.
        if (playing)
        {
            playing = false;
            QuestNPCInteraction.SetCutsceneLock(false);
        }
    }
}