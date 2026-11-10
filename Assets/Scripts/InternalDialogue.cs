using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// The player "talks to themselves" using the same dialogue box the NPCs use.
/// Call Play() from CutsceneTrigger's "On Cutscene Finished ()" event.
/// The player stays locked until the last line is done.
public class InternalDialogue : MonoBehaviour
{
    [Header("UI (the same ones the NPC dialogue uses)")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Speaker")]
    [SerializeField] private string playerName = "You";
    [SerializeField] private string playerNameColor = "#4DA6FF";
    [Tooltip("Shows the lines in italics so they read like thoughts.")]
    [SerializeField] private bool italic = true;

    [Header("Lines")]
    [TextArea(2, 4)]
    [SerializeField] private string[] lines;

    [Header("Timing")]
    [Tooltip("Wait after the camera returns before the first line appears.")]
    [SerializeField] private float delayBeforeStart = 0.75f;
    [SerializeField] private KeyCode advanceKey = KeyCode.E;
    [Tooltip("Seconds before a line can be skipped, so the player can't mash through.")]
    [SerializeField] private float advanceCooldown = 0.5f;
    [Tooltip("0 = wait for the key. Above 0, each line also advances by itself after this many seconds.")]
    [SerializeField] private float autoAdvanceSeconds = 0f;
    [SerializeField] private bool playOnce = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onFinished;

    private bool playing;
    private bool played;

    public void Play()
    {
        if (playing || (playOnce && played))
            return;

        if (lines == null || lines.Length == 0 ||
            dialoguePanel == null || dialogueText == null)
        {
            Debug.LogWarning("InternalDialogue: assign the lines and the dialogue UI in the Inspector.");
            return;
        }

        playing = true;
        played = true;

        // Lock right away so there is no gap between the cutscene
        // unlocking the player and the first line appearing.
        QuestNPCInteraction.SetCutsceneLock(true);

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(delayBeforeStart);

        dialoguePanel.SetActive(true);

        for (int i = 0; i < lines.Length; i++)
        {
            ShowLine(lines[i]);

            float canSkipAt = Time.unscaledTime + advanceCooldown;
            float timer = 0f;

            while (true)
            {
                timer += Time.deltaTime;

                if (Time.unscaledTime >= canSkipAt)
                {
                    bool keyPressed = Input.GetKeyDown(advanceKey);
                    bool timedOut = autoAdvanceSeconds > 0f && timer >= autoAdvanceSeconds;

                    if (keyPressed || timedOut)
                        break;
                }

                yield return null;
            }
        }

        Finish();
    }

    private void ShowLine(string line)
    {
        string text = line.Trim();

        if (italic)
            text = "<i>" + text + "</i>";

        dialogueText.text =
            "<color=" + playerNameColor + ">" + playerName + ":</color>\n" + text;
    }

    private void Finish()
    {
        dialoguePanel.SetActive(false);
        QuestNPCInteraction.SetCutsceneLock(false);
        playing = false;
        onFinished.Invoke();
    }

    private void OnDisable()
    {
        // Safety: never leave the player frozen if this object is
        // disabled or the scene changes mid-monologue.
        if (playing)
        {
            playing = false;
            QuestNPCInteraction.SetCutsceneLock(false);

            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);
        }
    }
}