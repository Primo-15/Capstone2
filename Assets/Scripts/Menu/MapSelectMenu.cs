using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapSelectMenu : MonoBehaviour
{
    public static MapSelectMenu Instance { get; private set; }

    [System.Serializable]
    public class ChapterEntry
    {
        public string chapterNumber = "Chapter 1";
        public string chapterTitle = "Help my Inay";
        public string sceneName = "M1";
        public Texture2D imageTexture;
    }

    [Header("Main Menu Button")]
    [SerializeField] private Button mapSelectButton;

    [Header("Window References")]
    [SerializeField] private GameObject chapterSelectPanel;
    [SerializeField] private RectTransform dialogPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeXButton;

    [Header("Chapter Entries")]
    [SerializeField]
    private ChapterEntry[] chapterEntries = new ChapterEntry[]
    {
        new ChapterEntry { chapterNumber = "CHAPTER 1", chapterTitle = "Help my Inay", sceneName = "M1" },
        new ChapterEntry { chapterNumber = "CHAPTER 2", chapterTitle = "Chaos", sceneName = "M2" },
        new ChapterEntry { chapterNumber = "CHAPTER 3", chapterTitle = "Find help", sceneName = "M3" },
        new ChapterEntry { chapterNumber = "CHAPTER 4", chapterTitle = "Gathering Informations", sceneName = "M4" },
        new ChapterEntry { chapterNumber = "CHAPTER 5", chapterTitle = "Rescue Inay", sceneName = "M5" },
    };

    private static readonly Color LockedTextColor = new Color(0.196f, 0.196f, 0.196f, 0.45f);
    private static readonly Color UnlockedTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);

    private static Sprite defaultRoundedSprite;
    private static TMP_FontAsset defaultFontAsset;

    private void Awake()
    {
        Instance = this;

        // Ensure Chapter Replay mode is cleared when arriving at Main Menu
        SaveSystem.IsChapterReplayMode = false;

        ResolveReferences();
        EnsureChapterTexturesLoaded();
        BuildOrRefreshUI();

        if (chapterSelectPanel != null)
        {
            chapterSelectPanel.SetActive(false);
        }
    }

    private void Start()
    {
        UpdateMapSelectButtonState();
    }

    private void OnEnable()
    {
        UpdateMapSelectButtonState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureChapterTexturesLoaded();
    }
#endif

    private void EnsureChapterTexturesLoaded()
    {
        if (chapterEntries == null || chapterEntries.Length != 5)
        {
            chapterEntries = new ChapterEntry[]
            {
                new ChapterEntry { chapterNumber = "CHAPTER 1", chapterTitle = "Help my Inay", sceneName = "M1" },
                new ChapterEntry { chapterNumber = "CHAPTER 2", chapterTitle = "Chaos", sceneName = "M2" },
                new ChapterEntry { chapterNumber = "CHAPTER 3", chapterTitle = "Find help", sceneName = "M3" },
                new ChapterEntry { chapterNumber = "CHAPTER 4", chapterTitle = "Gathering Informations", sceneName = "M4" },
                new ChapterEntry { chapterNumber = "CHAPTER 5", chapterTitle = "Rescue Inay", sceneName = "M5" },
            };
        }

        string[] fileNames = { "ChapterM1.PNG", "ChapterM2.PNG", "ChapterM3.PNG", "ChapterM4.PNG", "ChapterM5.PNG" };

        for (int i = 0; i < chapterEntries.Length; i++)
        {
            if (chapterEntries[i].imageTexture == null)
            {
                string assetPath = "Assets/Prefabs/Chapters/" + fileNames[i];
#if UNITY_EDITOR
                chapterEntries[i].imageTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#endif
                if (chapterEntries[i].imageTexture == null)
                {
                    chapterEntries[i].imageTexture = LoadTextureFromDisk(assetPath);
                }
            }
        }
    }

    private static Texture2D LoadTextureFromDisk(string relativePath)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", relativePath);
        if (File.Exists(fullPath))
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                return tex;
            }
        }
        return null;
    }

    public void ResolveReferences()
    {
        if (chapterSelectPanel == null)
        {
            chapterSelectPanel = gameObject;
        }

        if (mapSelectButton == null)
        {
            GameObject btnObj = GameObject.Find("MapSelectButton");
            if (btnObj != null)
            {
                mapSelectButton = btnObj.GetComponent<Button>();
            }
        }

        if (mapSelectButton != null)
        {
            mapSelectButton.onClick.RemoveListener(OpenChapterSelect);
            mapSelectButton.onClick.AddListener(OpenChapterSelect);
        }
    }

    public void UpdateMapSelectButtonState()
    {
        if (mapSelectButton == null)
        {
            ResolveReferences();
        }

        if (mapSelectButton == null)
            return;

        bool isUnlocked = SaveSystem.IsGameCompleted();
        mapSelectButton.interactable = isUnlocked;

        TMP_Text btnText = mapSelectButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
        {
            btnText.color = isUnlocked ? UnlockedTextColor : LockedTextColor;
        }
    }

    public void OpenChapterSelect()
    {
        if (!SaveSystem.IsGameCompleted())
        {
            Debug.LogWarning("[MapSelectMenu] Map Selection is locked. Complete M5 to unlock.");
            return;
        }

        // Close other panels to avoid overlapping
        MenuSettings settings = FindFirstObjectByType<MenuSettings>();
        if (settings != null)
        {
            settings.CloseSettings();
        }

        AboutPanelController about = FindFirstObjectByType<AboutPanelController>();
        if (about != null)
        {
            about.CloseAbout();
        }

        BuildOrRefreshUI();

        if (chapterSelectPanel != null)
        {
            chapterSelectPanel.SetActive(true);
        }
    }

    public void CloseChapterSelect()
    {
        if (chapterSelectPanel != null)
        {
            chapterSelectPanel.SetActive(false);
        }
    }

    public void PlayChapter(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[MapSelectMenu] Cannot play chapter: scene name is empty.");
            return;
        }

        Debug.Log($"[MapSelectMenu] Starting Chapter Replay Mode for scene: {sceneName}");

        SaveSystem.IsChapterReplayMode = true;
        SaveSystem.Pending = null;
        SaveSystem.PendingSpawn = null;
        SaveSystem.ClearPendingMapTransition();

        PlayerStats.ResetAll();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetProgress();
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearInventory();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void BuildOrRefreshUI()
    {
        // Cache font and sprite from existing UI elements
        if (defaultFontAsset == null)
        {
            TMP_Text existingText = FindFirstObjectByType<TMP_Text>();
            if (existingText != null)
                defaultFontAsset = existingText.font;
        }

        if (defaultRoundedSprite == null)
        {
            Image existingImage = GetComponent<Image>();
            if (existingImage != null && existingImage.sprite != null)
                defaultRoundedSprite = existingImage.sprite;
        }

        // Ensure canvas override sorting so it appears above the entire Main Menu
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = 16;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        // Ensure full-screen dim background overlay
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.color = new Color(0f, 0f, 0f, 0.72f);
            bgImage.raycastTarget = true;
        }

        // If DialogPanel already exists, destroy old children to cleanly rebuild
        Transform existingDialog = transform.Find("DialogPanel");
        if (existingDialog != null)
        {
            dialogPanel = existingDialog.GetComponent<RectTransform>();
            // If already fully configured, return
            if (dialogPanel.Find("CardsContainer") != null)
            {
                return;
            }
        }

        if (dialogPanel == null)
        {
            GameObject dialogObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dialogObj.transform.SetParent(transform, false);
            dialogPanel = dialogObj.GetComponent<RectTransform>();
        }

        dialogPanel.anchorMin = new Vector2(0.5f, 0.5f);
        dialogPanel.anchorMax = new Vector2(0.5f, 0.5f);
        dialogPanel.pivot = new Vector2(0.5f, 0.5f);
        dialogPanel.anchoredPosition = Vector2.zero;
        dialogPanel.sizeDelta = new Vector2(1380, 640);

        Image panelImg = dialogPanel.GetComponent<Image>();
        panelImg.sprite = defaultRoundedSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.18f, 0.15f, 0.13f, 0.98f); // Matches SettingsWindow theme

        // 1. Header Title
        CreateText(dialogPanel, "Title", "CHAPTER REPLAY", 32, FontStyles.Bold, new Color(0.96f, 0.92f, 0.84f),
            new Vector2(0, 260), new Vector2(800, 45));

        // 2. Subtitle
        CreateText(dialogPanel, "Subtitle", "Select any chapter to replay. Completed replays return to the Main Menu.",
            15, FontStyles.Normal, new Color(0.75f, 0.70f, 0.62f), new Vector2(0, 225), new Vector2(800, 30));

        // 3. Top-Right Close "X" Button
        closeXButton = CreateButton(dialogPanel, "CloseXButton", "X", 18,
            new Vector2(650, 280), new Vector2(36, 36),
            new Color(0.35f, 0.28f, 0.22f, 0.9f), Color.white, CloseChapterSelect);

        // 4. Cards Container
        GameObject cardsContainer = new GameObject("CardsContainer", typeof(RectTransform));
        cardsContainer.transform.SetParent(dialogPanel, false);
        RectTransform cardsRt = cardsContainer.GetComponent<RectTransform>();
        cardsRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardsRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRt.pivot = new Vector2(0.5f, 0.5f);
        cardsRt.anchoredPosition = new Vector2(0, -10);
        cardsRt.sizeDelta = new Vector2(1300, 400);

        // Build the 5 cards
        float cardWidth = 240f;
        float cardHeight = 360f;
        float spacing = 20f;
        float totalWidth = 5 * cardWidth + 4 * spacing;
        float startX = -totalWidth / 2f + cardWidth / 2f;

        EnsureChapterTexturesLoaded();

        for (int i = 0; i < chapterEntries.Length; i++)
        {
            ChapterEntry entry = chapterEntries[i];
            float posX = startX + i * (cardWidth + spacing);
            CreateChapterCard(cardsRt, entry, new Vector2(posX, 0), new Vector2(cardWidth, cardHeight));
        }

        // 5. Bottom Back Button
        backButton = CreateButton(dialogPanel, "BackButton", "BACK", 20,
            new Vector2(0, -265), new Vector2(180, 50),
            new Color(0.30f, 0.25f, 0.20f, 1f), Color.white, CloseChapterSelect);
    }

    private void CreateChapterCard(RectTransform parent, ChapterEntry entry, Vector2 pos, Vector2 size)
    {
        // Card Root with Button component so the entire card is clickable
        GameObject cardObj = new GameObject("Card_" + entry.sceneName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cardObj.transform.SetParent(parent, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = pos;
        cardRt.sizeDelta = size;

        Image cardBg = cardObj.GetComponent<Image>();
        cardBg.sprite = defaultRoundedSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = new Color(0.12f, 0.10f, 0.08f, 0.95f);

        Button cardBtn = cardObj.GetComponent<Button>();
        ColorBlock colors = cardBtn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        cardBtn.colors = colors;

        string targetScene = entry.sceneName;
        cardBtn.onClick.AddListener(() => PlayChapter(targetScene));

        // 1. Chapter Header (e.g. "CHAPTER 1")
        CreateText(cardRt, "Header", entry.chapterNumber, 17, FontStyles.Bold, new Color(0.92f, 0.86f, 0.76f),
            new Vector2(0, 150), new Vector2(size.x - 20, 30));

        // 2. Image Frame Container
        GameObject imgFrame = new GameObject("ImageFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgFrame.transform.SetParent(cardRt, false);
        RectTransform frameRt = imgFrame.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0.5f);
        frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.pivot = new Vector2(0.5f, 0.5f);
        frameRt.anchoredPosition = new Vector2(0, 50);
        frameRt.sizeDelta = new Vector2(220, 135);

        Image frameImg = imgFrame.GetComponent<Image>();
        frameImg.sprite = defaultRoundedSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = new Color(0.06f, 0.05f, 0.04f, 1f);

        // 3. Chapter RawImage with AspectRatioFitter
        if (entry.imageTexture != null)
        {
            GameObject rawImgObj = new GameObject("PreviewImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            rawImgObj.transform.SetParent(frameRt, false);
            RectTransform rawRt = rawImgObj.GetComponent<RectTransform>();
            rawRt.anchorMin = Vector2.zero;
            rawRt.anchorMax = Vector2.one;
            rawRt.offsetMin = new Vector2(4, 4);
            rawRt.offsetMax = new Vector2(-4, -4);

            RawImage rawImg = rawImgObj.GetComponent<RawImage>();
            rawImg.texture = entry.imageTexture;
            rawImg.color = Color.white;
            rawImg.raycastTarget = false;

            AspectRatioFitter fitter = rawImgObj.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)entry.imageTexture.width / Mathf.Max(1, entry.imageTexture.height);
        }

        // 4. Chapter Title (e.g. "Help my Inay")
        CreateText(cardRt, "Title", entry.chapterTitle, 14, FontStyles.Normal, new Color(0.78f, 0.74f, 0.68f),
            new Vector2(0, -50), new Vector2(size.x - 20, 35));

        // 5. Play Button
        CreateButton(cardRt, "PlayButton", "REPLAY", 16,
            new Vector2(0, -125), new Vector2(160, 42),
            new Color(0.62f, 0.52f, 0.38f, 1f), Color.white, () => PlayChapter(targetScene));
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize,
        FontStyles fontStyle, Color color, Vector2 anchoredPos, Vector2 size)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (defaultFontAsset != null)
            tmp.font = defaultFontAsset;

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        return tmp;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, float fontSize,
        Vector2 anchoredPos, Vector2 size, Color bgColor, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = defaultRoundedSprite;
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        btn.colors = colors;

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        CreateText(rt, "Text", label, fontSize, FontStyles.Bold, textColor, Vector2.zero, size);

        return btn;
    }
}
