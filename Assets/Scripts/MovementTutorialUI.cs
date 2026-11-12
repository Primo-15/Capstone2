using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Shows a small top-right checklist: move with WASD, run with Shift,
// interact with an NPC using E. Each line turns green when the player does it.
// When all are done, the panel blinks for a few seconds and then disappears.
public class MovementTutorialUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string moveMessage = "Press W, A, S, D to move";
    [SerializeField] private string runMessage = "Press Shift to run";
    [SerializeField] private string interactMessage = "Press E to interact";

    [Header("Look")]
    [SerializeField] private int fontSize = 28;
    [SerializeField] private float panelWidth = 480f;      // CHANGED (height is now automatic)
    [SerializeField] private float lineHeight = 50f;       // CHANGED
    [SerializeField] private float padding = 15f;          // CHANGED
    [SerializeField] private float screenMargin = 30f;
    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color doneColor = new Color(0.35f, 0.9f, 0.55f);

    // E only counts when the player is this close to an NPC
    [Header("Interact")]
    [SerializeField] private float npcInteractRange = 3.5f;

    [Header("Finish")]
    [SerializeField] private float blinkDuration = 3f;
    [SerializeField] private float blinkInterval = 0.25f;

    private GameObject canvasObject;
    private CanvasGroup panelGroup;
    private TMP_Text moveText;
    private TMP_Text runText;
    private TMP_Text interactText;

    private bool moveDone;
    private bool runDone;
    private bool interactDone;
    private bool finished;

    private void Start()
    {
        BuildUI();
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void Update()
    {
        if (finished || canvasObject == null)
            return;

        // Ignore keys pressed while the game is paused
        if (Time.timeScale == 0f)
            return;

        if (!moveDone && MovePressed())
        {
            moveDone = true;
            moveText.color = doneColor;
        }

        if (!runDone && RunPressed())
        {
            runDone = true;
            runText.color = doneColor;
        }

        // E only counts when it is pressed next to an NPC
        if (!interactDone && InteractPressed() && NearNpc())
        {
            interactDone = true;
            interactText.color = doneColor;
        }

        if (moveDone && runDone && interactDone)
        {
            finished = true;
            StartCoroutine(BlinkAndHide());
        }
    }

    private bool MovePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return false;
        return k.wKey.isPressed || k.aKey.isPressed ||
               k.sKey.isPressed || k.dKey.isPressed;
#else
        return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
               Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
#endif
    }

    private bool RunPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return false;
        return k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    private bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    // True if the player is within range of any quest NPC in the scene
    private bool NearNpc()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return false;

        QuestNPCInteraction[] npcs =
            FindObjectsByType<QuestNPCInteraction>(FindObjectsSortMode.None);

        foreach (QuestNPCInteraction npc in npcs)
        {
            if (npc == null) continue;

            float distance = Vector3.Distance(
                player.transform.position,
                npc.transform.position
            );

            if (distance <= npcInteractRange)
                return true;
        }

        return false;
    }

    private IEnumerator BlinkAndHide()
    {
        // Let the player see the last line turn green first
        yield return new WaitForSecondsRealtime(0.4f);

        float elapsed = 0f;
        bool visible = true;

        while (elapsed < blinkDuration)
        {
            visible = !visible;
            panelGroup.alpha = visible ? 1f : 0.15f;

            yield return new WaitForSecondsRealtime(blinkInterval);
            elapsed += blinkInterval;
        }

        Destroy(canvasObject);
        canvasObject = null;
    }

    // ---------- UI building ----------

    private void BuildUI()
    {
        canvasObject = new GameObject("MovementTutorialCanvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // CHANGED: height is calculated from the number of lines (3),
        // so the background always fits all of them.
        const int lineCount = 3;
        float textHeight = lineHeight - 10f;
        float panelHeight = padding + (lineCount - 1) * lineHeight + textHeight + padding;

        // Panel in the top-right corner
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObject.transform, false);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);
        background.raycastTarget = false;

        panelGroup = panel.AddComponent<CanvasGroup>();
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-screenMargin, -screenMargin);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        moveText = CreateLine(panel.transform, "MoveText", moveMessage, 0, textHeight);
        runText = CreateLine(panel.transform, "RunText", runMessage, 1, textHeight);
        interactText = CreateLine(panel.transform, "InteractText", interactMessage, 2, textHeight);
    }

    // CHANGED: lines are placed by index instead of a fixed pixel value
    private TMP_Text CreateLine(Transform parent, string objectName, string message, int index, float textHeight)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = fontSize;
        text.color = pendingColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -(padding + index * lineHeight));
        rect.sizeDelta = new Vector2(-30f, textHeight);

        return text;
    }
}