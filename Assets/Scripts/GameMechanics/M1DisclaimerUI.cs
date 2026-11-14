using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class M1DisclaimerUI : MonoBehaviour
{
    public const string TargetSceneName = "M1";

    public const string DisclaimerTitle = "DISCLAIMER";

    public const string DisclaimerParagraph1 =
        "This game contains depictions of war, armed conflict, violence, threats, captivity, and other situations inspired by the hardships experienced during the Japanese occupation of the Philippines.";

    public const string DisclaimerParagraph2 =
        "Some scenes may be emotionally distressing or uncomfortable for certain players.";

    public const string DisclaimerParagraph3 =
        "Player discretion is advised.";

    public const string FullDisclaimerBody =
        DisclaimerParagraph1 + "\n\n" + DisclaimerParagraph2 + "\n\n" + DisclaimerParagraph3;

    public const string UnderstandButtonLabel = "I Understand";

    public static M1DisclaimerUI Instance { get; private set; }
    public static bool IsDisclaimerOpen { get; private set; }

    [Header("Disclaimer UI References")]
    [SerializeField] private Canvas disclaimerCanvas;
    [SerializeField] private GameObject disclaimerOverlay;
    [SerializeField] private GameObject disclaimerWindow;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button understandButton;
    [SerializeField] private TMP_Text understandButtonText;
    [SerializeField] private string followCameraName = "PlayerFollowCamera";

    private bool hasShownThisSession;
    private ThirdPersonController playerController;
    private StarterAssetsInputs playerInputs;
    private CursorAltToggle cursorAltToggle;
#if ENABLE_INPUT_SYSTEM
    private readonly List<PlayerInput> disabledPlayerInputs = new List<PlayerInput>();
#endif
    private readonly List<Behaviour> disabledCameraBehaviours = new List<Behaviour>();
    private bool restoreCursorThisFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureExistsInM1(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExistsInM1(scene);
    }

    public static void EnsureExistsInM1(Scene scene)
    {
        if (!scene.IsValid() || scene.name != TargetSceneName)
            return;

        if (Instance != null)
            return;

        M1DisclaimerUI existing = FindFirstObjectByType<M1DisclaimerUI>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject canvasObj = new GameObject("M1DisclaimerCanvas");
        SceneManager.MoveGameObjectToScene(canvasObj, scene);
        canvasObj.AddComponent<M1DisclaimerUI>();
    }

    private void Awake()
    {
        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
        if (sceneName != TargetSceneName)
        {
            Destroy(gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        IsDisclaimerOpen = false;
        hasShownThisSession = false;

        EnsureDisclaimerUI();

        if (disclaimerOverlay != null)
        {
            disclaimerOverlay.SetActive(false);
        }

        MapVideoController.OnIntroVideoFinished -= HandleIntroVideoFinished;
        MapVideoController.OnIntroVideoFinished += HandleIntroVideoFinished;
    }

    private void Start()
    {
        if (hasShownThisSession)
            return;

        // Wait for M1Intro to finish before displaying the disclaimer.
        if (MapVideoController.IsVideoPlaying ||
            (MapVideoController.Instance != null && MapVideoController.Instance.HasIntroVideo))
        {
            MapVideoController.OnIntroVideoFinished -= HandleIntroVideoFinished;
            MapVideoController.OnIntroVideoFinished += HandleIntroVideoFinished;
        }
        else
        {
            OpenDisclaimer();
        }
    }

    private void HandleIntroVideoFinished()
    {
        MapVideoController.OnIntroVideoFinished -= HandleIntroVideoFinished;

        if (this != null && gameObject.activeInHierarchy && !hasShownThisSession)
        {
            OpenDisclaimer();
        }
    }

    private void OnDisable()
    {
        if (IsDisclaimerOpen)
        {
            IsDisclaimerOpen = false;
            Time.timeScale = 1f;
            UnlockPlayerAndRestoreCursor();
        }
    }

    private void OnDestroy()
    {
        MapVideoController.OnIntroVideoFinished -= HandleIntroVideoFinished;

        if (Instance == this)
        {
            Instance = null;
            if (IsDisclaimerOpen)
            {
                IsDisclaimerOpen = false;
                Time.timeScale = 1f;
            }
        }
    }

    private void Update()
    {
        if (!IsDisclaimerOpen)
            return;

        // Keep gameplay paused and player inputs locked while disclaimer is open.
        // Do NOT allow Escape, E, or any keyboard shortcut to close the disclaimer.
        if (Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
        }

        KeepPlayerLockedAndCursorUnlocked();
    }

    private void LateUpdate()
    {
        if (IsDisclaimerOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (restoreCursorThisFrame)
        {
            restoreCursorThisFrame = false;
            if (playerInputs != null)
            {
                playerInputs.move = Vector2.zero;
                playerInputs.look = Vector2.zero;
                playerInputs.jump = false;
                playerInputs.sprint = false;
            }
            ApplyGameplayCursorState();
        }
    }

    public void OpenDisclaimer()
    {
        if (hasShownThisSession)
            return;

        EnsureDisclaimerUI();

        if (disclaimerOverlay == null)
            return;

        hasShownThisSession = true;
        IsDisclaimerOpen = true;

        disclaimerOverlay.transform.SetAsLastSibling();
        disclaimerOverlay.SetActive(true);
        if (disclaimerWindow != null)
        {
            disclaimerWindow.SetActive(true);
        }

        Time.timeScale = 0f;
        CacheAndLockPlayer();
        KeepPlayerLockedAndCursorUnlocked();
    }

    public void OnUnderstandClicked()
    {
        CloseDisclaimer();
    }

    public void CloseDisclaimer()
    {
        if (!IsDisclaimerOpen && (disclaimerOverlay == null || !disclaimerOverlay.activeSelf))
            return;

        IsDisclaimerOpen = false;

        if (disclaimerOverlay != null)
        {
            disclaimerOverlay.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Time.timeScale = 1f;
        UnlockPlayerAndRestoreCursor();
    }

    private void EnsureDisclaimerUI()
    {
        if (disclaimerCanvas == null)
        {
            disclaimerCanvas = GetComponent<Canvas>();
            if (disclaimerCanvas == null)
            {
                disclaimerCanvas = gameObject.AddComponent<Canvas>();
            }
        }

        disclaimerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        disclaimerCanvas.overrideSorting = true;
        disclaimerCanvas.sortingOrder = 100;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        if (disclaimerOverlay == null)
        {
            Transform existingOverlay = transform.Find("DisclaimerOverlay");
            if (existingOverlay != null)
            {
                disclaimerOverlay = existingOverlay.gameObject;
            }
        }

        if (disclaimerOverlay == null)
        {
            disclaimerOverlay = new GameObject("DisclaimerOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            disclaimerOverlay.transform.SetParent(transform, false);

            RectTransform overlayRect = disclaimerOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImg = disclaimerOverlay.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.68f);
            overlayImg.raycastTarget = true; // Blocks clicks outside window without dismissing
        }

        if (disclaimerWindow == null)
        {
            Transform existingWindow = disclaimerOverlay.transform.Find("DisclaimerWindow");
            if (existingWindow != null)
            {
                disclaimerWindow = existingWindow.gameObject;
            }
        }

        if (disclaimerWindow == null)
        {
            disclaimerWindow = new GameObject("DisclaimerWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            disclaimerWindow.transform.SetParent(disclaimerOverlay.transform, false);

            RectTransform winRect = disclaimerWindow.GetComponent<RectTransform>();
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.anchoredPosition = Vector2.zero;
            winRect.sizeDelta = new Vector2(1160f, 640f);

            Image outerWoodImg = disclaimerWindow.GetComponent<Image>();
            outerWoodImg.color = new Color(0.18f, 0.08f, 0.03f, 0.98f);
            outerWoodImg.raycastTarget = true;

            // Bronze border frame (matches M3Tooltip theme)
            CreateInsetPanel(disclaimerWindow.transform, "BronzeBorder", 10f, new Color(0.48f, 0.29f, 0.14f, 1f));

            // Warm aged parchment fill
            CreateInsetPanel(disclaimerWindow.transform, "ParchmentBackground", 16f, new Color(0.92f, 0.82f, 0.63f, 1f));

            // Inner decorative frame line
            CreateInsetPanel(disclaimerWindow.transform, "InnerFrameBorder", 28f, new Color(0.48f, 0.29f, 0.14f, 0.85f));

            // Inner parchment surface
            CreateInsetPanel(disclaimerWindow.transform, "InnerParchment", 32f, new Color(0.95f, 0.87f, 0.70f, 1f));

            // Top Header Banner Border (like Stage 3 Palayan! plaque in M3Tooltip)
            GameObject headerBorderObj = new GameObject("HeaderBannerBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerBorderObj.transform.SetParent(disclaimerWindow.transform, false);
            RectTransform headerBorderRect = headerBorderObj.GetComponent<RectTransform>();
            headerBorderRect.anchorMin = new Vector2(0.5f, 1f);
            headerBorderRect.anchorMax = new Vector2(0.5f, 1f);
            headerBorderRect.pivot = new Vector2(0.5f, 1f);
            headerBorderRect.anchoredPosition = new Vector2(0f, -38f);
            headerBorderRect.sizeDelta = new Vector2(468f, 76f);
            Image headerBorderImg = headerBorderObj.GetComponent<Image>();
            headerBorderImg.color = new Color(0.55f, 0.35f, 0.16f, 1f);
            headerBorderImg.raycastTarget = false;

            GameObject headerBannerObj = new GameObject("HeaderBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerBannerObj.transform.SetParent(headerBorderObj.transform, false);
            RectTransform headerBannerRect = headerBannerObj.GetComponent<RectTransform>();
            headerBannerRect.anchorMin = Vector2.zero;
            headerBannerRect.anchorMax = Vector2.one;
            headerBannerRect.offsetMin = new Vector2(4f, 4f);
            headerBannerRect.offsetMax = new Vector2(-4f, -4f);
            Image headerBannerImg = headerBannerObj.GetComponent<Image>();
            headerBannerImg.color = new Color(0.24f, 0.12f, 0.05f, 0.98f);
            headerBannerImg.raycastTarget = false;

            // Title Text ("DISCLAIMER")
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(headerBannerObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            titleText = titleObj.GetComponent<TextMeshProUGUI>();

            // Top Divider
            GameObject topDivObj = new GameObject("TopDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            topDivObj.transform.SetParent(disclaimerWindow.transform, false);
            RectTransform topDivRect = topDivObj.GetComponent<RectTransform>();
            topDivRect.anchorMin = new Vector2(0.5f, 1f);
            topDivRect.anchorMax = new Vector2(0.5f, 1f);
            topDivRect.pivot = new Vector2(0.5f, 0.5f);
            topDivRect.anchoredPosition = new Vector2(0f, -130f);
            topDivRect.sizeDelta = new Vector2(960f, 2f);
            Image topDivImg = topDivObj.GetComponent<Image>();
            topDivImg.color = new Color(0.48f, 0.29f, 0.14f, 0.55f);
            topDivImg.raycastTarget = false;

            // Disclaimer Body Text
            GameObject bodyObj = new GameObject("DisclaimerBodyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            bodyObj.transform.SetParent(disclaimerWindow.transform, false);
            RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(84f, 146f);
            bodyRect.offsetMax = new Vector2(-84f, -146f);
            bodyText = bodyObj.GetComponent<TextMeshProUGUI>();

            // Bottom Divider
            GameObject botDivObj = new GameObject("BottomDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            botDivObj.transform.SetParent(disclaimerWindow.transform, false);
            RectTransform botDivRect = botDivObj.GetComponent<RectTransform>();
            botDivRect.anchorMin = new Vector2(0.5f, 0f);
            botDivRect.anchorMax = new Vector2(0.5f, 0f);
            botDivRect.pivot = new Vector2(0.5f, 0.5f);
            botDivRect.anchoredPosition = new Vector2(0f, 130f);
            botDivRect.sizeDelta = new Vector2(960f, 2f);
            Image botDivImg = botDivObj.GetComponent<Image>();
            botDivImg.color = new Color(0.48f, 0.29f, 0.14f, 0.55f);
            botDivImg.raycastTarget = false;

            // Confirmation Button Border
            GameObject btnBorderObj = new GameObject("ButtonBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            btnBorderObj.transform.SetParent(disclaimerWindow.transform, false);
            RectTransform btnBorderRect = btnBorderObj.GetComponent<RectTransform>();
            btnBorderRect.anchorMin = new Vector2(0.5f, 0f);
            btnBorderRect.anchorMax = new Vector2(0.5f, 0f);
            btnBorderRect.pivot = new Vector2(0.5f, 0.5f);
            btnBorderRect.anchoredPosition = new Vector2(0f, 74f);
            btnBorderRect.sizeDelta = new Vector2(288f, 66f);
            Image btnBorderImg = btnBorderObj.GetComponent<Image>();
            btnBorderImg.color = new Color(0.55f, 0.35f, 0.16f, 1f);
            btnBorderImg.raycastTarget = false;

            // Confirmation Button ("I Understand")
            GameObject btnObj = new GameObject("UnderstandButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(disclaimerWindow.transform, false);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0f, 74f);
            btnRect.sizeDelta = new Vector2(280f, 58f);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = Color.white;
            btnImg.raycastTarget = true;

            understandButton = btnObj.GetComponent<Button>();
            understandButton.targetGraphic = btnImg;

            GameObject btnTxtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTxtRect = btnTxtObj.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.offsetMin = Vector2.zero;
            btnTxtRect.offsetMax = Vector2.zero;
            understandButtonText = btnTxtObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            if (titleText == null)
            {
                Transform t = disclaimerWindow.transform.Find("HeaderBannerBorder/HeaderBanner/TitleText");
                if (t != null) titleText = t.GetComponent<TMP_Text>();
            }
            if (bodyText == null)
            {
                Transform t = disclaimerWindow.transform.Find("DisclaimerBodyText");
                if (t != null) bodyText = t.GetComponent<TMP_Text>();
            }
            if (understandButton == null)
            {
                Transform t = disclaimerWindow.transform.Find("UnderstandButton");
                if (t != null) understandButton = t.GetComponent<Button>();
            }
            if (understandButtonText == null && understandButton != null)
            {
                understandButtonText = understandButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        // Enforce exact text and styling
        if (titleText != null)
        {
            titleText.text = DisclaimerTitle;
            titleText.fontSize = 38f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.97f, 0.91f, 0.78f, 1f);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;
        }

        if (bodyText != null)
        {
            bodyText.text = FullDisclaimerBody;
            bodyText.fontSize = 28f;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 18f;
            bodyText.fontSizeMax = 28f;
            bodyText.fontStyle = FontStyles.Normal;
            bodyText.color = new Color(0.16f, 0.08f, 0.03f, 1f);
            bodyText.alignment = TextAlignmentOptions.Center;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            bodyText.lineSpacing = 6f;
            bodyText.raycastTarget = false;
        }

        if (understandButton != null)
        {
            Navigation nav = understandButton.navigation;
            nav.mode = Navigation.Mode.None;
            understandButton.navigation = nav;

            Image targetImg = understandButton.targetGraphic as Image;
            if (targetImg != null)
            {
                targetImg.color = Color.white;
            }

            ColorBlock cb = understandButton.colors;
            cb.normalColor = new Color(0.24f, 0.12f, 0.05f, 0.96f);
            cb.highlightedColor = new Color(0.38f, 0.20f, 0.09f, 1f);
            cb.pressedColor = new Color(0.15f, 0.07f, 0.02f, 1f);
            cb.selectedColor = new Color(0.24f, 0.12f, 0.05f, 0.96f);
            cb.disabledColor = new Color(0.24f, 0.12f, 0.05f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            understandButton.colors = cb;

            understandButton.onClick.RemoveListener(OnUnderstandClicked);
            if (!HasPersistentListener(understandButton, nameof(OnUnderstandClicked)) &&
                !HasPersistentListener(understandButton, nameof(CloseDisclaimer)))
            {
                understandButton.onClick.AddListener(OnUnderstandClicked);
            }
        }

        if (understandButtonText != null)
        {
            understandButtonText.text = UnderstandButtonLabel;
            understandButtonText.fontSize = 26f;
            understandButtonText.fontStyle = FontStyles.Bold;
            understandButtonText.color = new Color(0.97f, 0.91f, 0.78f, 1f);
            understandButtonText.alignment = TextAlignmentOptions.Center;
            understandButtonText.raycastTarget = false;
        }
    }

    private static bool HasPersistentListener(Button button, string methodName)
    {
        if (button == null)
            return false;

        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null &&
                button.onClick.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }
        return false;
    }

    private static GameObject CreateInsetPanel(Transform parent, string name, float inset, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);

        Image img = obj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return obj;
    }

    private void CacheAndLockPlayer()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<ThirdPersonController>();

        if (playerController != null)
        {
            if (playerInputs == null)
                playerInputs = playerController.GetComponent<StarterAssetsInputs>();
            if (cursorAltToggle == null)
                cursorAltToggle = playerController.GetComponent<CursorAltToggle>();
        }
        else
        {
            if (playerInputs == null)
                playerInputs = FindFirstObjectByType<StarterAssetsInputs>();
            if (cursorAltToggle == null)
                cursorAltToggle = FindFirstObjectByType<CursorAltToggle>();
        }

        if (playerController != null)
        {
            playerController.StopMovementImmediately();
            playerController.LockCameraPosition = true;
            playerController.enabled = false;
        }

        if (playerInputs != null)
        {
            playerInputs.move = Vector2.zero;
            playerInputs.look = Vector2.zero;
            playerInputs.jump = false;
            playerInputs.sprint = false;
            playerInputs.cursorInputForLook = false;
        }

        if (cursorAltToggle != null)
        {
            cursorAltToggle.enabled = false;
        }

#if ENABLE_INPUT_SYSTEM
        disabledPlayerInputs.Clear();
        foreach (PlayerInput pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
        {
            if (pi != null && pi.enabled)
            {
                pi.enabled = false;
                disabledPlayerInputs.Add(pi);
            }
        }
#endif

        disabledCameraBehaviours.Clear();
        GameObject cam = GameObject.Find(followCameraName);
        if (cam != null)
        {
            foreach (Behaviour b in cam.GetComponents<Behaviour>())
            {
                if (b != null && b.enabled && b.GetType().Name == "CinemachineCamera")
                {
                    b.enabled = false;
                    disabledCameraBehaviours.Add(b);
                }
            }
        }
    }

    private void KeepPlayerLockedAndCursorUnlocked()
    {
        if (playerController == null || playerInputs == null)
            CacheAndLockPlayer();

        if (playerController != null && playerController.enabled)
        {
            playerController.StopMovementImmediately();
            playerController.LockCameraPosition = true;
            playerController.enabled = false;
        }

        if (playerInputs != null)
        {
            playerInputs.move = Vector2.zero;
            playerInputs.look = Vector2.zero;
            playerInputs.jump = false;
            playerInputs.sprint = false;
            playerInputs.cursorInputForLook = false;
        }

        if (cursorAltToggle != null && cursorAltToggle.enabled)
            cursorAltToggle.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void UnlockPlayerAndRestoreCursor()
    {
#if ENABLE_INPUT_SYSTEM
        for (int i = 0; i < disabledPlayerInputs.Count; i++)
        {
            if (disabledPlayerInputs[i] != null)
                disabledPlayerInputs[i].enabled = true;
        }
        disabledPlayerInputs.Clear();
#endif

        if (playerInputs != null)
        {
            playerInputs.move = Vector2.zero;
            playerInputs.look = Vector2.zero;
            playerInputs.jump = false;
            playerInputs.sprint = false;
            playerInputs.cursorInputForLook = true;
            playerInputs.cursorLocked = true;
        }

        if (playerController != null)
        {
            playerController.LockCameraPosition = false;
            playerController.enabled = true;
        }

        for (int i = 0; i < disabledCameraBehaviours.Count; i++)
        {
            if (disabledCameraBehaviours[i] != null)
                disabledCameraBehaviours[i].enabled = true;
        }
        disabledCameraBehaviours.Clear();

        if (cursorAltToggle != null)
        {
            cursorAltToggle.enabled = true;
        }

        ApplyGameplayCursorState();
        restoreCursorThisFrame = true;
    }

    private void ApplyGameplayCursorState()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
    }
}

