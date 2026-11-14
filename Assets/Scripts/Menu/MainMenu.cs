using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenu : MonoBehaviour
{
    public const string QuitConfirmTitle = "Quit Game?";
    public const string QuitConfirmMessage =
        "Are you sure you want to quit the game?\n" +
        "Your saved progress will be preserved.";

    [SerializeField] private Button continueButton;
    [SerializeField] private Button mapSelectButton;
    [SerializeField] private MapSelectMenu mapSelectMenu;

    [Header("Game Logo (Assets/Prefabs/GameLogo)")]
    [SerializeField] private Image gameLogoImage;
    [SerializeField] private Sprite gameLogoSprite;
    [SerializeField] private Texture2D gameLogoTexture;

    [Header("Quit Confirmation")]
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject quitConfirmWindow;
    [SerializeField] private Button confirmQuitButton;
    [SerializeField] private Button cancelQuitButton;

    private Sprite runtimeLogoSprite;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gameLogoSprite == null)
        {
            gameLogoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/GameLogo.png");
        }
        if (gameLogoTexture == null)
        {
            gameLogoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/GameLogo.png");
        }
    }
#endif

    private void Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Ensure Chapter Replay mode is cleared when at Main Menu
        SaveSystem.IsChapterReplayMode = false;

        ApplyMainMenuButtonOrder();
        EnsureGameLogo();
        EnsureQuitConfirmationUI();
        MenuSettings.EnsureMainMenuSettingsExists();

        RefreshMenuButtons();
    }

    public void RefreshMenuButtons()
    {
        if (continueButton == null)
        {
            GameObject contObj = GameObject.Find("ContinueButton");
            if (contObj != null)
                continueButton = contObj.GetComponent<Button>();
        }

        if (continueButton != null)
            continueButton.interactable = SaveSystem.HasSave();

        UpdateMapSelectButton();
    }

    private Transform FindMainCanvasTransform()
    {
        if (continueButton != null && continueButton.transform.parent != null)
            return continueButton.transform.parent;

        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj != null)
            return canvasObj.transform;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private void EnsureGameLogo()
    {
        Transform canvasTransform = FindMainCanvasTransform();
        if (canvasTransform == null)
            return;

        if (gameLogoImage == null)
        {
            Transform existingLogo = canvasTransform.Find("GameLogo");
            if (existingLogo != null)
            {
                gameLogoImage = existingLogo.GetComponent<Image>();
            }
        }

        if (gameLogoImage == null)
        {
            GameObject logoObj = new GameObject("GameLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            logoObj.layer = 5;
            logoObj.transform.SetParent(canvasTransform, false);
            logoObj.transform.SetSiblingIndex(0);
            gameLogoImage = logoObj.GetComponent<Image>();
        }
        else
        {
            gameLogoImage.transform.SetSiblingIndex(0);
        }

        RectTransform logoRect = gameLogoImage.GetComponent<RectTransform>();
        if (logoRect != null)
        {
            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            // Directly above StartButton (which is at (280, 103) with height 60 -> top edge y = 133).
            // At y = 315 with height 280 -> bottom edge y = 175 (42px gap above StartButton), top edge y = 455.
            logoRect.anchoredPosition = new Vector2(280f, 315f);
            logoRect.sizeDelta = new Vector2(700f, 280f);
            logoRect.localScale = Vector3.one;
        }

#if UNITY_EDITOR
        if (gameLogoSprite == null)
        {
            gameLogoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/GameLogo.png");
        }
        if (gameLogoTexture == null)
        {
            gameLogoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/GameLogo.png");
        }
#endif

        Sprite targetSprite = gameLogoSprite;
        if (targetSprite == null && gameLogoTexture != null)
        {
            if (runtimeLogoSprite == null)
            {
                runtimeLogoSprite = Sprite.Create(
                    gameLogoTexture,
                    new Rect(0f, 0f, gameLogoTexture.width, gameLogoTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            targetSprite = runtimeLogoSprite;
        }

        if (targetSprite != null)
        {
            gameLogoImage.sprite = targetSprite;
        }

        gameLogoImage.color = Color.white;
        gameLogoImage.type = Image.Type.Simple;
        gameLogoImage.preserveAspect = true;
        gameLogoImage.raycastTarget = false;
    }

    private void EnsureQuitConfirmationUI()
    {
        Transform canvasTransform = FindMainCanvasTransform();
        if (canvasTransform == null)
            return;

        if (quitButton == null)
        {
            Transform quitTrans = canvasTransform.Find("QuitButton");
            if (quitTrans != null)
                quitButton = quitTrans.GetComponent<Button>();
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            if (quitButton.onClick.GetPersistentEventCount() == 0)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        if (quitConfirmWindow == null)
        {
            Transform existingWin = canvasTransform.Find("QuitConfirmWindow");
            if (existingWin != null)
                quitConfirmWindow = existingWin.gameObject;
        }

        if (quitConfirmWindow == null)
        {
            quitConfirmWindow = BuildQuitConfirmWindow(canvasTransform);
        }
        else
        {
            Transform dialog = quitConfirmWindow.transform.Find("DialogPanel");
            if (dialog != null)
            {
                Transform titleTrans = dialog.Find("TitleText");
                if (titleTrans != null && titleTrans.TryGetComponent<TMP_Text>(out var titleTmp))
                    titleTmp.text = QuitConfirmTitle;

                Transform msgTrans = dialog.Find("MessageText");
                if (msgTrans != null && msgTrans.TryGetComponent<TMP_Text>(out var msgTmp))
                    msgTmp.text = QuitConfirmMessage;

                if (confirmQuitButton == null)
                {
                    Transform confirmTrans = dialog.Find("QuitGameButton");
                    if (confirmTrans != null)
                        confirmQuitButton = confirmTrans.GetComponent<Button>();
                }

                if (cancelQuitButton == null)
                {
                    Transform cancelTrans = dialog.Find("CancelButton");
                    if (cancelTrans != null)
                        cancelQuitButton = cancelTrans.GetComponent<Button>();
                }
            }
        }

        if (confirmQuitButton != null)
        {
            confirmQuitButton.onClick.RemoveListener(ConfirmQuitGame);
            confirmQuitButton.onClick.AddListener(ConfirmQuitGame);
        }

        if (cancelQuitButton != null)
        {
            cancelQuitButton.onClick.RemoveListener(CancelQuitGame);
            cancelQuitButton.onClick.AddListener(CancelQuitGame);
        }

        quitConfirmWindow.SetActive(false);
    }

    private GameObject BuildQuitConfirmWindow(Transform canvasTransform)
    {
        TMP_FontAsset font = null;
        TMP_Text existingText = canvasTransform.GetComponentInChildren<TMP_Text>(true);
        if (existingText != null)
            font = existingText.font;

        Sprite buttonSprite = null;
        if (continueButton != null && continueButton.TryGetComponent<Image>(out var contImg))
            buttonSprite = contImg.sprite;

        GameObject overlayObj = new GameObject("QuitConfirmWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObj.layer = 5;
        overlayObj.transform.SetParent(canvasTransform, false);
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = Vector2.zero;
        Image overlayImg = overlayObj.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.72f);
        overlayImg.raycastTarget = true;

        GameObject dialogObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dialogObj.layer = 5;
        dialogObj.transform.SetParent(overlayObj.transform, false);
        RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(460f, 230f);
        dialogRect.localScale = new Vector3(2f, 2f, 2f);
        Image dialogImg = dialogObj.GetComponent<Image>();
        dialogImg.color = new Color(0.18f, 0.15f, 0.13f, 0.98f);
        dialogImg.raycastTarget = true;

        // Title: Quit Game?
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.layer = 5;
        titleObj.transform.SetParent(dialogObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 72f);
        titleRect.sizeDelta = new Vector2(400f, 38f);
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (font != null) titleTmp.font = font;
        titleTmp.text = QuitConfirmTitle;
        titleTmp.fontSize = 26f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;

        // Message
        GameObject msgObj = new GameObject("MessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        msgObj.layer = 5;
        msgObj.transform.SetParent(dialogObj.transform, false);
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.5f, 0.5f);
        msgRect.anchorMax = new Vector2(0.5f, 0.5f);
        msgRect.pivot = new Vector2(0.5f, 0.5f);
        msgRect.anchoredPosition = new Vector2(0f, 8f);
        msgRect.sizeDelta = new Vector2(410f, 75f);
        TextMeshProUGUI msgTmp = msgObj.GetComponent<TextMeshProUGUI>();
        if (font != null) msgTmp.font = font;
        msgTmp.text = QuitConfirmMessage;
        msgTmp.fontSize = 16.5f;
        msgTmp.fontStyle = FontStyles.Normal;
        msgTmp.color = new Color(0.93f, 0.91f, 0.88f, 1f);
        msgTmp.alignment = TextAlignmentOptions.Center;
        msgTmp.textWrappingMode = TextWrappingModes.Normal;
        msgTmp.raycastTarget = false;

        // Quit Game Button
        confirmQuitButton = CreateDialogButton(
            dialogObj.transform,
            "QuitGameButton",
            "Quit Game",
            new Vector2(-95f, -68f),
            new Vector2(160f, 38f),
            new Color(0.75f, 0.22f, 0.22f, 1f),
            Color.white,
            font,
            buttonSprite);

        // Cancel Button
        cancelQuitButton = CreateDialogButton(
            dialogObj.transform,
            "CancelButton",
            "Cancel",
            new Vector2(95f, -68f),
            new Vector2(160f, 38f),
            Color.white,
            new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f),
            font,
            buttonSprite);

        return overlayObj;
    }

    private static Button CreateDialogButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        Color bgColor,
        Color textColor,
        TMP_FontAsset font,
        Sprite buttonSprite)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.layer = 5;
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = anchoredPos;
        btnRect.sizeDelta = size;

        Image btnImg = btnObj.GetComponent<Image>();
        if (buttonSprite != null)
        {
            btnImg.sprite = buttonSprite;
            btnImg.type = Image.Type.Sliced;
        }
        btnImg.color = bgColor;
        btnImg.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;

        GameObject txtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.layer = 5;
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.anchoredPosition = Vector2.zero;
        txtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }

    private void ApplyMainMenuButtonOrder()
    {
        // Enforces top-to-bottom order:
        // 1. Start New Game -> (280, 103)
        // 2. Continue       -> (280, 25)
        // 3. Select Map     -> (280, -53)
        // 4. Settings       -> (280, -135)
        // 5. About          -> (280, -218)
        // 6. Quit           -> (280, -296)
        string[] buttonNames =
        {
            "StartButton",
            "ContinueButton",
            "MapSelectButton",
            "SettingsButton",
            "AboutButton",
            "QuitButton"
        };

        float[] yPositions = { 103f, 25f, -53f, -135f, -218f, -296f };

        for (int i = 0; i < buttonNames.Length; i++)
        {
            GameObject btnObj = GameObject.Find(buttonNames[i]);
            if (btnObj != null)
            {
                RectTransform rt = btnObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(280f, yPositions[i]);
                }
            }
        }
    }

    public void UpdateMapSelectButton()
    {
        if (mapSelectMenu != null)
        {
            mapSelectMenu.UpdateMapSelectButtonState();
        }
        else if (mapSelectButton != null)
        {
            bool isUnlocked = SaveSystem.IsGameCompleted();
            mapSelectButton.interactable = isUnlocked;

            TMP_Text btnText = mapSelectButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                btnText.color = isUnlocked ? new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f) : new Color(0.196f, 0.196f, 0.196f, 0.45f);
            }
        }
    }

    public void StartGame()
    {
        SaveSystem.IsChapterReplayMode = false;

        // Delete the previous checkpoint.
        SaveSystem.Delete();
        PlayerStats.ResetAll();

        // Clear any progress from the previous session.
        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetProgress();

        SceneManager.LoadScene("M1");
    }

    public void ContinueGame()
    {
        SaveSystem.IsChapterReplayMode = false;

        SaveData data = SaveSystem.Load();

        if (data == null)
        {
            Debug.LogWarning("No save file found.");
            return;
        }

        // Always start the saved map from its beginning.
        data.completedQuestIds.Clear();
        data.hasPosition = false;

        // Preserve remaining hearts when continuing M3 so the player cannot refill hearts via Main Menu.
        if (data.sceneName == "M3")
        {
            int savedHearts = data.m3Hearts > 0 ? data.m3Hearts : PlayerStats.MaxHearts;
            PlayerStats.SetHearts(savedHearts);
        }
        else
        {
            PlayerStats.ResetAll();
        }

        SaveSystem.Pending = data;
        SaveSystem.PendingSpawn = data;

        // Restore empty quest progress.
        if (QuestManager.Instance != null)
            QuestManager.Instance.ApplyPendingSave();

        Debug.Log(
            $"Continuing from the beginning of {data.sceneName}"
        );

        SceneManager.LoadScene(data.sceneName);
    }

    /// <summary>
    /// Opens the Quit Confirmation Window instead of closing the application immediately.
    /// Does NOT delete any save data.
    /// </summary>
    public void QuitGame()
    {
        EnsureQuitConfirmationUI();

        if (quitConfirmWindow != null)
        {
            quitConfirmWindow.SetActive(true);
            quitConfirmWindow.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Called when the player presses "Quit Game" inside the Quit Confirmation Window.
    /// Preserves all saved progress and closes the application.
    /// </summary>
    public void ConfirmQuitGame()
    {
        PlayerPrefs.Save();
        Debug.Log("Game Quit");
        Application.Quit();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Called when the player presses "Cancel" inside the Quit Confirmation Window.
    /// </summary>
    public void CancelQuitGame()
    {
        if (quitConfirmWindow != null)
        {
            quitConfirmWindow.SetActive(false);
        }
    }
}
