using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuSettings : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public const string BgMusicPrefKey = "Settings_BgMusicVolume";
    public const string SfxPrefKey = "Settings_SfxVolume";
    public const string MouseSensitivityPrefKey = "Settings_MouseSensitivity";

    public const float MinBgMusic = 0.0f;
    public const float MaxBgMusic = 10.0f;
    public const float DefaultBgMusicVolume = 6.0f; // Matches 0.6 normal gameplay BG music volume

    public const float MinSfx = 0.0f;
    public const float MaxSfx = 10.0f;
    public const float DefaultSfxVolume = 5.0f; // Matches 0.5 default footstep volume while preserving QTE balance

    public const float MinMouseSensitivity = 0.1f;
    public const float MaxMouseSensitivity = 10.0f;
    public const float DefaultMouseSensitivity = 1.0f; // Matches 1.0 normal gameplay camera look multiplier

    public const string DeleteConfirmTitle = "Delete Save Data?";
    public const string DeleteConfirmMessage =
        "Are you sure you want to delete all your saved game progress?\n\n" +
        "This will reset your completed chapters, quests, inventory, and unlocked Map Selection.\n\n" +
        "This action cannot be undone.";

    public const string DeleteSuccessTitle = "Save Data Deleted";
    public const string DeleteSuccessMessage =
        "Your saved game progress has been successfully deleted.";

    [Header("Window References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsOpenButton;
    [SerializeField] private Button backButton;

    [Header("Delete Save Data (Main Menu)")]
    [SerializeField] private Button deleteSaveDataButton;
    [SerializeField] private GameObject deleteConfirmWindow;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;
    [SerializeField] private GameObject deleteSuccessWindow;
    [SerializeField] private Button acknowledgeDeleteButton;

    [Header("Background Music (0.0 - 10.0)")]
    [SerializeField] private Slider bgMusicSlider;
    [SerializeField] private TMP_InputField bgMusicInput;

    [Header("Sound FX (0.0 - 10.0)")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_InputField sfxInput;

    [Header("Mouse Sensitivity (0.1 - 10.0)")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_InputField mouseSensitivityInput;

    private static bool isLoaded;
    private static bool sceneHookRegistered;

    public static float BgMusicVolume { get; private set; } = DefaultBgMusicVolume;
    public static float SfxVolume { get; private set; } = DefaultSfxVolume;
    public static float MouseSensitivity { get; private set; } = DefaultMouseSensitivity;

    private static readonly Dictionary<AudioSource, float> baseAudioSourceVolumes =
        new Dictionary<AudioSource, float>();

    private static readonly Dictionary<StarterAssets.ThirdPersonController, float> baseFootstepVolumes =
        new Dictionary<StarterAssets.ThirdPersonController, float>();

    private bool isSyncingUI;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnLoad()
    {
        EnsureLoaded();
        RegisterSceneHook();
        EnsureMainMenuSettingsExists();
        ApplyAllSettings();
    }

    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
            return;

        sceneHookRegistered = true;
        SceneManager.sceneLoaded += OnAnySceneLoaded;
    }

    private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureLoaded();
        EnsureMainMenuSettingsExists();
        ApplyAllSettings();
    }

    public static MenuSettings EnsureMainMenuSettingsExists()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "Main Menu")
            return null;

        MenuSettings existing = FindFirstObjectByType<MenuSettings>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.ResolveReferences();
            existing.ConfigureControls();
            return existing;
        }

        GameObject settingsBtnObj = GameObject.Find("SettingsButton");
        if (settingsBtnObj != null)
        {
            MenuSettings added = settingsBtnObj.AddComponent<MenuSettings>();
            return added;
        }

        return null;
    }

    public static void EnsureLoaded()
    {
        if (isLoaded)
            return;

        isLoaded = true;
        LoadSavedSettings();
    }

    public static void LoadSavedSettings()
    {
        BgMusicVolume = Mathf.Clamp(
            PlayerPrefs.GetFloat(BgMusicPrefKey, DefaultBgMusicVolume),
            MinBgMusic,
            MaxBgMusic
        );

        SfxVolume = Mathf.Clamp(
            PlayerPrefs.GetFloat(SfxPrefKey, DefaultSfxVolume),
            MinSfx,
            MaxSfx
        );

        MouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(MouseSensitivityPrefKey, DefaultMouseSensitivity),
            MinMouseSensitivity,
            MaxMouseSensitivity
        );
    }

    public static float ComputeBgMusicVolume(float baseVolume)
    {
        EnsureLoaded();

        if (BgMusicVolume <= 0.0001f)
            return 0f;

        // Scales proportionally from 0.0 (mute) through baseVolume at DefaultBgMusicVolume (6.0) up to 10.0,
        // preserving the relative volume balance between VillageMusicBG/PalayanMusicBG1 (0.6) and rain (0.4).
        return Mathf.Clamp01(baseVolume * (BgMusicVolume / DefaultBgMusicVolume));
    }

    public static float ComputeSfxVolume(float baseVolume)
    {
        EnsureLoaded();

        if (SfxVolume <= 0.0001f)
            return 0f;

        if (SfxVolume <= DefaultSfxVolume)
        {
            return Mathf.Clamp01(baseVolume * (SfxVolume / DefaultSfxVolume));
        }

        float t = (SfxVolume - DefaultSfxVolume) / (MaxSfx - DefaultSfxVolume);
        return Mathf.Clamp01(Mathf.Lerp(baseVolume, 1f, t));
    }

    public static void ApplyAllSettings()
    {
        EnsureLoaded();
        ApplyBgMusicVolume();
        ApplySfxVolume();
        ApplyMouseSensitivity();
    }

    public static void ApplyBgMusicVolume()
    {
        EnsureLoaded();

        // 1. Apply to SceneMusic (M1 VillageMusicBG, M3 PalayanMusicBG1)
        SceneMusic[] musicControllers =
            FindObjectsByType<SceneMusic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SceneMusic controller in musicControllers)
        {
            if (controller != null)
                controller.ApplySettingsVolumes();
        }

        // 2. Apply to scene background AudioSources (such as liecio-light-rain-109591 in M2 & M5)
        AudioSource[] sources =
            FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioSource source in sources)
        {
            if (source == null)
                continue;

            // SceneMusic manages its own AudioSources
            if (source.GetComponent<SceneMusic>() != null)
                continue;

            if (!IsBackgroundAudioSource(source))
                continue;

            if (!baseAudioSourceVolumes.TryGetValue(source, out float baseVol))
            {
                baseVol = source.volume;
                baseAudioSourceVolumes[source] = baseVol;
            }

            source.volume = ComputeBgMusicVolume(baseVol);
        }
    }

    private static bool IsBackgroundAudioSource(AudioSource source)
    {
        if (source == null)
            return false;

        if (source.clip != null)
        {
            string clipName = source.clip.name;
            if (clipName.IndexOf("rain", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                clipName.IndexOf("VillageMusic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                clipName.IndexOf("PalayanMusic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                clipName.IndexOf("M5BgMusic", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (source.resource != null)
        {
            string resName = source.resource.name;
            if (resName.IndexOf("rain", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                resName.IndexOf("VillageMusic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                resName.IndexOf("PalayanMusic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                resName.IndexOf("M5BgMusic", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return source.loop;
    }

    public static void ApplySfxVolume()
    {
        EnsureLoaded();

        // 1. Apply to QTEBgMusic, QteHitPass, and QTEHitFail in SceneMusic
        SceneMusic[] musicControllers =
            FindObjectsByType<SceneMusic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SceneMusic controller in musicControllers)
        {
            if (controller != null)
                controller.ApplySettingsVolumes();
        }

        // 2. Apply to Player Footsteps in ThirdPersonController
        StarterAssets.ThirdPersonController[] players =
            FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (StarterAssets.ThirdPersonController player in players)
        {
            if (player == null)
                continue;

            if (!baseFootstepVolumes.TryGetValue(player, out float baseFootstep))
            {
                baseFootstep = player.FootstepAudioVolume;
                baseFootstepVolumes[player] = baseFootstep;
            }

            player.FootstepAudioVolume = ComputeSfxVolume(baseFootstep);
        }
    }

    public static void ApplyMouseSensitivity()
    {
        EnsureLoaded();

        StarterAssets.ThirdPersonController[] players =
            FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (StarterAssets.ThirdPersonController player in players)
        {
            if (player != null)
                player.MouseSensitivity = MouseSensitivity;
        }
    }

    private void Awake()
    {
        EnsureLoaded();
        RegisterSceneHook();
        ResolveReferences();
        ConfigureControls();
        RefreshUIFromSavedSettings();
        ApplyAllSettings();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void Start()
    {
        RefreshUIFromSavedSettings();
        ApplyAllSettings();
    }

    private void ResolveReferences()
    {
        if (settingsOpenButton == null)
            settingsOpenButton = GetComponent<Button>();

        if (settingsPanel == null)
        {
            Transform child = transform.Find("SettingsWindow");
            if (child != null)
                settingsPanel = child.gameObject;
        }

        if (settingsPanel == null)
        {
            BuildSettingsWindowRuntime();
        }

        if (settingsPanel != null)
        {
            Transform dialog = settingsPanel.transform.Find("DialogPanel");
            Transform root = dialog != null ? dialog : settingsPanel.transform;

            if (backButton == null)
            {
                Transform t = root.Find("BackButton");
                if (t != null) backButton = t.GetComponent<Button>();
            }

            if (bgMusicSlider == null)
                bgMusicSlider = FindRowSlider(root, "BgMusicRow", "BgMusicSlider");

            if (bgMusicInput == null)
                bgMusicInput = FindRowInput(root, "BgMusicRow", "BgMusicInput");

            if (sfxSlider == null)
                sfxSlider = FindRowSlider(root, "SfxRow", "SfxSlider");

            if (sfxInput == null)
                sfxInput = FindRowInput(root, "SfxRow", "SfxInput");

            if (mouseSensitivitySlider == null)
                mouseSensitivitySlider = FindRowSlider(root, "MouseSensitivityRow", "MouseSensitivitySlider");

            if (mouseSensitivityInput == null)
                mouseSensitivityInput = FindRowInput(root, "MouseSensitivityRow", "MouseSensitivityInput");

            EnsureDeleteSaveDataUI(root as RectTransform);
        }
    }

    private static Slider FindRowSlider(Transform root, string rowName, string legacyName)
    {
        if (root == null) return null;
        Transform row = root.Find(rowName);
        if (row == null) return null;

        Transform t = row.Find(legacyName);
        if (t == null) t = row.Find("Slider");
        if (t != null)
        {
            Slider s = t.GetComponent<Slider>();
            if (s != null) return s;
        }

        return row.GetComponentInChildren<Slider>(true);
    }

    private static TMP_InputField FindRowInput(Transform root, string rowName, string legacyName)
    {
        if (root == null) return null;
        Transform row = root.Find(rowName);
        if (row == null) return null;

        Transform t = row.Find(legacyName);
        if (t == null) t = row.Find("InputField");
        if (t != null)
        {
            TMP_InputField inp = t.GetComponent<TMP_InputField>();
            if (inp != null) return inp;
        }

        return row.GetComponentInChildren<TMP_InputField>(true);
    }

    private static bool HasValidPersistentCall(Button button, Object expectedTarget, string expectedMethod)
    {
        if (button == null)
            return false;

        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentTarget(i) == expectedTarget &&
                button.onClick.GetPersistentMethodName(i) == expectedMethod)
            {
                return true;
            }
        }
        return false;
    }

    private void ConfigureControls()
    {
        if (settingsOpenButton != null && !HasValidPersistentCall(settingsOpenButton, this, nameof(OpenSettings)))
        {
            settingsOpenButton.onClick.RemoveListener(OpenSettings);
            settingsOpenButton.onClick.AddListener(OpenSettings);
        }

        if (backButton != null && !HasValidPersistentCall(backButton, this, nameof(CloseSettings)))
        {
            backButton.onClick.RemoveListener(CloseSettings);
            backButton.onClick.AddListener(CloseSettings);
        }

        ConfigureSlider(bgMusicSlider, MinBgMusic, MaxBgMusic, OnBgMusicSliderChanged);
        ConfigureInputField(bgMusicInput, OnBgMusicInputChanged, OnBgMusicInputEndEdit);

        ConfigureSlider(sfxSlider, MinSfx, MaxSfx, OnSfxSliderChanged);
        ConfigureInputField(sfxInput, OnSfxInputChanged, OnSfxInputEndEdit);

        ConfigureSlider(mouseSensitivitySlider, MinMouseSensitivity, MaxMouseSensitivity, OnMouseSensitivitySliderChanged);
        ConfigureInputField(mouseSensitivityInput, OnMouseSensitivityInputChanged, OnMouseSensitivityInputEndEdit);

        if (deleteSaveDataButton != null && !HasValidPersistentCall(deleteSaveDataButton, this, nameof(OnDeleteSaveDataClicked)))
        {
            deleteSaveDataButton.onClick.RemoveListener(OnDeleteSaveDataClicked);
            deleteSaveDataButton.onClick.AddListener(OnDeleteSaveDataClicked);
        }

        if (confirmDeleteButton != null && !HasValidPersistentCall(confirmDeleteButton, this, nameof(ConfirmDeleteSaveData)))
        {
            confirmDeleteButton.onClick.RemoveListener(ConfirmDeleteSaveData);
            confirmDeleteButton.onClick.AddListener(ConfirmDeleteSaveData);
        }

        if (cancelDeleteButton != null && !HasValidPersistentCall(cancelDeleteButton, this, nameof(CancelDeleteSaveData)))
        {
            cancelDeleteButton.onClick.RemoveListener(CancelDeleteSaveData);
            cancelDeleteButton.onClick.AddListener(CancelDeleteSaveData);
        }

        if (acknowledgeDeleteButton != null && !HasValidPersistentCall(acknowledgeDeleteButton, this, nameof(AcknowledgeDeleteSuccess)))
        {
            acknowledgeDeleteButton.onClick.RemoveListener(AcknowledgeDeleteSuccess);
            acknowledgeDeleteButton.onClick.AddListener(AcknowledgeDeleteSuccess);
        }
    }

    private bool IsMainMenuContext()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.name == "Main Menu")
            return true;

        return gameObject.scene.IsValid() && gameObject.scene.name == "Main Menu";
    }

    private void EnsureDeleteSaveDataUI(RectTransform dialogRect)
    {
        if (settingsPanel == null || dialogRect == null)
            return;

        if (!IsMainMenuContext())
            return;

        TMP_FontAsset fontAsset = null;
        TMP_Text existingTmp = dialogRect.GetComponentInChildren<TMP_Text>(true);
        if (existingTmp == null)
            existingTmp = GetComponentInChildren<TMP_Text>(true);
        if (existingTmp != null)
            fontAsset = existingTmp.font;

        Sprite buttonSprite = null;
        if (backButton != null && backButton.TryGetComponent<Image>(out var backImg))
            buttonSprite = backImg.sprite;

        // Expand DialogPanel cleanly to accommodate Delete Save Data separated from audio/mouse controls
        dialogRect.sizeDelta = new Vector2(540f, 370f);

        SetChildAnchoredPosition(dialogRect, "TitleText", new Vector2(0f, 145f));
        SetChildAnchoredPosition(dialogRect, "BgMusicRow", new Vector2(0f, 82f));
        SetChildAnchoredPosition(dialogRect, "SfxRow", new Vector2(0f, 29f));
        SetChildAnchoredPosition(dialogRect, "MouseSensitivityRow", new Vector2(0f, -24f));
        SetChildAnchoredPosition(dialogRect, "BackButton", new Vector2(0f, -148f));

        // Visual separator line between settings rows and Delete Save Data
        Transform dividerTrans = dialogRect.Find("SaveDataDivider");
        if (dividerTrans == null)
        {
            GameObject dividerObj = new GameObject("SaveDataDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerObj.layer = 5;
            dividerObj.transform.SetParent(dialogRect, false);
            RectTransform divRect = dividerObj.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.5f, 0.5f);
            divRect.anchorMax = new Vector2(0.5f, 0.5f);
            divRect.pivot = new Vector2(0.5f, 0.5f);
            divRect.anchoredPosition = new Vector2(0f, -66f);
            divRect.sizeDelta = new Vector2(460f, 2f);
            Image divImg = dividerObj.GetComponent<Image>();
            divImg.color = new Color(1f, 1f, 1f, 0.16f);
            divImg.raycastTarget = false;
        }

        // Delete Save Data Button inside Settings DialogPanel
        if (deleteSaveDataButton == null)
        {
            Transform delTrans = dialogRect.Find("DeleteSaveDataButton");
            if (delTrans != null)
                deleteSaveDataButton = delTrans.GetComponent<Button>();
        }

        if (deleteSaveDataButton == null)
        {
            deleteSaveDataButton = CreatePopupButton(
                dialogRect,
                "DeleteSaveDataButton",
                "Delete Save Data",
                new Vector2(0f, -98f),
                new Vector2(220f, 34f),
                new Color(0.72f, 0.20f, 0.20f, 1f),
                Color.white,
                17f,
                fontAsset,
                buttonSprite);
        }
        else
        {
            RectTransform delRect = deleteSaveDataButton.GetComponent<RectTransform>();
            if (delRect != null)
            {
                delRect.anchoredPosition = new Vector2(0f, -98f);
                delRect.sizeDelta = new Vector2(220f, 34f);
            }
        }

        // Delete Save Data Confirmation Window
        if (deleteConfirmWindow == null)
        {
            Transform confirmTrans = settingsPanel.transform.Find("DeleteSaveConfirmWindow");
            if (confirmTrans != null)
                deleteConfirmWindow = confirmTrans.gameObject;
        }

        if (deleteConfirmWindow == null)
        {
            deleteConfirmWindow = BuildDeleteConfirmWindow(settingsPanel.transform, dialogRect.localScale, fontAsset, buttonSprite);
        }
        else
        {
            Transform confirmDialog = deleteConfirmWindow.transform.Find("DialogPanel");
            if (confirmDialog != null)
            {
                if (confirmDeleteButton == null)
                {
                    Transform btn = confirmDialog.Find("DeleteDataButton");
                    if (btn != null) confirmDeleteButton = btn.GetComponent<Button>();
                }
                if (cancelDeleteButton == null)
                {
                    Transform btn = confirmDialog.Find("CancelButton");
                    if (btn != null) cancelDeleteButton = btn.GetComponent<Button>();
                }
            }
        }

        // Save Data Deleted Success Window
        if (deleteSuccessWindow == null)
        {
            Transform successTrans = settingsPanel.transform.Find("DeleteSaveSuccessWindow");
            if (successTrans != null)
                deleteSuccessWindow = successTrans.gameObject;
        }

        if (deleteSuccessWindow == null)
        {
            deleteSuccessWindow = BuildDeleteSuccessWindow(settingsPanel.transform, dialogRect.localScale, fontAsset, buttonSprite);
        }
        else
        {
            Transform successDialog = deleteSuccessWindow.transform.Find("DialogPanel");
            if (successDialog != null && acknowledgeDeleteButton == null)
            {
                Transform btn = successDialog.Find("OkButton");
                if (btn != null) acknowledgeDeleteButton = btn.GetComponent<Button>();
            }
        }
    }

    private static void SetChildAnchoredPosition(RectTransform parent, string childName, Vector2 pos)
    {
        if (parent == null) return;
        Transform child = parent.Find(childName);
        if (child != null && child.TryGetComponent<RectTransform>(out var rt))
        {
            rt.anchoredPosition = pos;
        }
    }

    private GameObject BuildDeleteConfirmWindow(
        Transform parentWindow,
        Vector3 panelScale,
        TMP_FontAsset fontAsset,
        Sprite buttonSprite)
    {
        GameObject overlayObj = new GameObject("DeleteSaveConfirmWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObj.layer = 5;
        overlayObj.transform.SetParent(parentWindow, false);
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = new Vector2(2400f, 2400f);
        Image overlayImg = overlayObj.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.78f);
        overlayImg.raycastTarget = true;

        GameObject dialogObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dialogObj.layer = 5;
        dialogObj.transform.SetParent(overlayObj.transform, false);
        RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(510f, 290f);
        dialogRect.localScale = panelScale;

        Image dialogImg = dialogObj.GetComponent<Image>();
        dialogImg.sprite = buttonSprite;
        dialogImg.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        dialogImg.color = new Color(0.18f, 0.15f, 0.13f, 0.99f);
        dialogImg.raycastTarget = true;

        // Title: Delete Save Data?
        CreatePopupText(
            dialogObj.transform,
            "TitleText",
            DeleteConfirmTitle,
            new Vector2(0f, 106f),
            new Vector2(460f, 36f),
            24f,
            FontStyles.Bold,
            Color.white,
            fontAsset);

        // Warning Message
        CreatePopupText(
            dialogObj.transform,
            "WarningText",
            DeleteConfirmMessage,
            new Vector2(0f, 8f),
            new Vector2(455f, 140f),
            15f,
            FontStyles.Normal,
            new Color(0.94f, 0.91f, 0.88f, 1f),
            fontAsset);

        // Delete Data Button
        confirmDeleteButton = CreatePopupButton(
            dialogObj.transform,
            "DeleteDataButton",
            "Delete Data",
            new Vector2(-100f, -104f),
            new Vector2(165f, 38f),
            new Color(0.75f, 0.20f, 0.20f, 1f),
            Color.white,
            18f,
            fontAsset,
            buttonSprite);

        // Cancel Button
        cancelDeleteButton = CreatePopupButton(
            dialogObj.transform,
            "CancelButton",
            "Cancel",
            new Vector2(100f, -104f),
            new Vector2(165f, 38f),
            Color.white,
            new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f),
            18f,
            fontAsset,
            buttonSprite);

        overlayObj.SetActive(false);
        return overlayObj;
    }

    private GameObject BuildDeleteSuccessWindow(
        Transform parentWindow,
        Vector3 panelScale,
        TMP_FontAsset fontAsset,
        Sprite buttonSprite)
    {
        GameObject overlayObj = new GameObject("DeleteSaveSuccessWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObj.layer = 5;
        overlayObj.transform.SetParent(parentWindow, false);
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = new Vector2(2400f, 2400f);
        Image overlayImg = overlayObj.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.78f);
        overlayImg.raycastTarget = true;

        GameObject dialogObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dialogObj.layer = 5;
        dialogObj.transform.SetParent(overlayObj.transform, false);
        RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(460f, 215f);
        dialogRect.localScale = panelScale;

        Image dialogImg = dialogObj.GetComponent<Image>();
        dialogImg.sprite = buttonSprite;
        dialogImg.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        dialogImg.color = new Color(0.18f, 0.15f, 0.13f, 0.99f);
        dialogImg.raycastTarget = true;

        // Title: Save Data Deleted
        CreatePopupText(
            dialogObj.transform,
            "TitleText",
            DeleteSuccessTitle,
            new Vector2(0f, 66f),
            new Vector2(410f, 36f),
            24f,
            FontStyles.Bold,
            Color.white,
            fontAsset);

        // Message: Your saved game progress has been successfully deleted.
        CreatePopupText(
            dialogObj.transform,
            "MessageText",
            DeleteSuccessMessage,
            new Vector2(0f, 6f),
            new Vector2(410f, 64f),
            16.5f,
            FontStyles.Normal,
            new Color(0.94f, 0.91f, 0.88f, 1f),
            fontAsset);

        // OK Button
        acknowledgeDeleteButton = CreatePopupButton(
            dialogObj.transform,
            "OkButton",
            "OK",
            new Vector2(0f, -64f),
            new Vector2(150f, 36f),
            Color.white,
            new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f),
            18f,
            fontAsset,
            buttonSprite);

        overlayObj.SetActive(false);
        return overlayObj;
    }

    private static void CreatePopupText(
        Transform parent,
        string name,
        string content,
        Vector2 anchoredPos,
        Vector2 size,
        float fontSize,
        FontStyles style,
        Color color,
        TMP_FontAsset fontAsset)
    {
        GameObject txtObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.layer = 5;
        txtObj.transform.SetParent(parent, false);
        RectTransform rect = txtObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
    }

    private static Button CreatePopupButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        Color bgColor,
        Color textColor,
        float fontSize,
        TMP_FontAsset fontAsset,
        Sprite buttonSprite)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.layer = 5;
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = buttonSprite;
        img.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = bgColor;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.layer = 5;
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }

    private void BuildSettingsWindowRuntime()
    {
        TMP_FontAsset fontAsset = null;
        TMP_Text existingTmp = GetComponentInChildren<TMP_Text>(true);
        if (existingTmp == null)
            existingTmp = FindFirstObjectByType<TMP_Text>();
        if (existingTmp != null)
            fontAsset = existingTmp.font;

        Sprite buttonSprite = null;
        Image existingBtnImg = GetComponent<Image>();
        if (existingBtnImg != null)
            buttonSprite = existingBtnImg.sprite;

        GameObject windowObj = new GameObject("SettingsWindow", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Image));
        windowObj.transform.SetParent(transform, false);

        RectTransform windowRect = windowObj.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(2400f, 2400f);

        Canvas windowCanvas = windowObj.GetComponent<Canvas>();
        windowCanvas.overrideSorting = true;
        windowCanvas.sortingOrder = 15;

        Image dimImg = windowObj.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);
        dimImg.raycastTarget = true;

        GameObject dialogObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dialogObj.transform.SetParent(windowObj.transform, false);
        RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(540f, 310f);

        Image dialogImg = dialogObj.GetComponent<Image>();
        dialogImg.sprite = buttonSprite;
        dialogImg.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        dialogImg.color = new Color(0.18f, 0.15f, 0.13f, 0.98f);
        dialogImg.raycastTarget = true;

        // TitleText
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(dialogObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 112f);
        titleRect.sizeDelta = new Vector2(460f, 40f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleTmp.font = fontAsset;
        titleTmp.text = "Settings";
        titleTmp.fontSize = 26f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;

        // Rows
        BuildOptionRow(dialogRect, "BgMusicRow", "Background Music", 45f, MinBgMusic, MaxBgMusic, DefaultBgMusicVolume, fontAsset, buttonSprite, out bgMusicSlider, out bgMusicInput);
        BuildOptionRow(dialogRect, "SfxRow", "Sound FX", -10f, MinSfx, MaxSfx, DefaultSfxVolume, fontAsset, buttonSprite, out sfxSlider, out sfxInput);
        BuildOptionRow(dialogRect, "MouseSensitivityRow", "Mouse Sensitivity", -65f, MinMouseSensitivity, MaxMouseSensitivity, DefaultMouseSensitivity, fontAsset, buttonSprite, out mouseSensitivitySlider, out mouseSensitivityInput);

        // BackButton
        GameObject backObj = new GameObject("BackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        backObj.transform.SetParent(dialogObj.transform, false);
        RectTransform backRect = backObj.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.5f);
        backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(0f, -118f);
        backRect.sizeDelta = new Vector2(160f, 36f);

        Image backImg = backObj.GetComponent<Image>();
        backImg.sprite = buttonSprite;
        backImg.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backImg.color = Color.white;

        backButton = backObj.GetComponent<Button>();
        backButton.targetGraphic = backImg;

        GameObject backTextObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        backTextObj.transform.SetParent(backObj.transform, false);
        RectTransform backTextRect = backTextObj.GetComponent<RectTransform>();
        backTextRect.anchorMin = Vector2.zero;
        backTextRect.anchorMax = Vector2.one;
        backTextRect.anchoredPosition = Vector2.zero;
        backTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI backTmp = backTextObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) backTmp.font = fontAsset;
        backTmp.text = "Back";
        backTmp.fontSize = 20f;
        backTmp.fontStyle = FontStyles.Bold;
        backTmp.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        backTmp.alignment = TextAlignmentOptions.Center;

        settingsPanel = windowObj;
        settingsPanel.SetActive(false);
    }

    private static void BuildOptionRow(
        RectTransform parent,
        string rowName,
        string labelText,
        float yPos,
        float minVal,
        float maxVal,
        float defaultVal,
        TMP_FontAsset fontAsset,
        Sprite sprite,
        out Slider sliderOut,
        out TMP_InputField inputOut)
    {
        GameObject rowObj = new GameObject(rowName, typeof(RectTransform));
        rowObj.transform.SetParent(parent, false);
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, yPos);
        rowRect.sizeDelta = new Vector2(490f, 42f);

        // Label
        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(92f, 0f);
        labelRect.sizeDelta = new Vector2(180f, 36f);

        TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) labelTmp.font = fontAsset;
        labelTmp.text = labelText;
        labelTmp.fontSize = 18f;
        labelTmp.color = Color.white;
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.raycastTarget = false;

        // InputField
        GameObject inputObj = new GameObject("InputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(rowObj.transform, false);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0.5f);
        inputRect.anchorMax = new Vector2(0f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(224f, 0f);
        inputRect.sizeDelta = new Vector2(68f, 32f);

        Image inputImg = inputObj.GetComponent<Image>();
        inputImg.sprite = sprite;
        inputImg.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        inputImg.color = Color.white;

        GameObject textAreaObj = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaObj.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = textAreaObj.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.anchoredPosition = Vector2.zero;
        textAreaRect.sizeDelta = new Vector2(-10f, -6f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(textAreaObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI inputTextTmp = textObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) inputTextTmp.font = fontAsset;
        inputTextTmp.fontSize = 16f;
        inputTextTmp.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        inputTextTmp.alignment = TextAlignmentOptions.Center;

        inputOut = inputObj.GetComponent<TMP_InputField>();
        inputOut.targetGraphic = inputImg;
        inputOut.textViewport = textAreaRect;
        inputOut.textComponent = inputTextTmp;
        inputOut.text = FormatValue(defaultVal);

        // Slider
        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(rowObj.transform, false);
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(0f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(380f, 0f);
        sliderRect.sizeDelta = new Vector2(210f, 24f);

        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.sprite = sprite;
        bgImg.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bgImg.color = new Color(0.28f, 0.24f, 0.22f, 1f);

        GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.anchoredPosition = new Vector2(-5f, 0f);
        fillAreaRect.sizeDelta = new Vector2(-20f, 0f);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(10f, 0f);
        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.sprite = sprite;
        fillImg.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        fillImg.color = new Color(0.85f, 0.78f, 0.68f, 1f);

        GameObject handleSlideObj = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideObj.transform.SetParent(sliderObj.transform, false);
        RectTransform handleSlideRect = handleSlideObj.GetComponent<RectTransform>();
        handleSlideRect.anchorMin = Vector2.zero;
        handleSlideRect.anchorMax = Vector2.one;
        handleSlideRect.anchoredPosition = Vector2.zero;
        handleSlideRect.sizeDelta = new Vector2(-20f, 0f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObj.transform.SetParent(handleSlideObj.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(20f, 0f);
        Image handleImg = handleObj.GetComponent<Image>();
        handleImg.color = Color.white;

        sliderOut = sliderObj.GetComponent<Slider>();
        sliderOut.fillRect = fillRect;
        sliderOut.handleRect = handleRect;
        sliderOut.targetGraphic = handleImg;
        sliderOut.direction = Slider.Direction.LeftToRight;
        sliderOut.minValue = minVal;
        sliderOut.maxValue = maxVal;
        sliderOut.value = defaultVal;
    }

    private static void ConfigureSlider(
        Slider slider,
        float min,
        float max,
        UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null)
            return;

        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
        slider.onValueChanged.RemoveListener(callback);
        slider.onValueChanged.AddListener(callback);
    }

    private static void ConfigureInputField(
        TMP_InputField inputField,
        UnityEngine.Events.UnityAction<string> onChanged,
        UnityEngine.Events.UnityAction<string> onEndEdit)
    {
        if (inputField == null)
            return;

        inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        inputField.characterLimit = 6;
        inputField.onValueChanged.RemoveListener(onChanged);
        inputField.onValueChanged.AddListener(onChanged);
        inputField.onEndEdit.RemoveListener(onEndEdit);
        inputField.onEndEdit.AddListener(onEndEdit);
    }

    public void OpenSettings()
    {
        ResolveReferences();
        ConfigureControls();
        EnsureLoaded();
        RefreshUIFromSavedSettings();

        if (deleteConfirmWindow != null)
            deleteConfirmWindow.SetActive(false);

        if (deleteSuccessWindow != null)
            deleteSuccessWindow.SetActive(false);

        if (settingsPanel != null)
        {
            CenterWindowOnCanvas();
            settingsPanel.SetActive(true);
        }

        if (settingsOpenButton != null)
            settingsOpenButton.enabled = false;
    }

    public void CloseSettings()
    {
        if (deleteConfirmWindow != null)
            deleteConfirmWindow.SetActive(false);

        if (deleteSuccessWindow != null)
            deleteSuccessWindow.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (settingsOpenButton != null)
            settingsOpenButton.enabled = true;
    }

    public void OnDeleteSaveDataClicked()
    {
        ResolveReferences();
        ConfigureControls();

        if (deleteSuccessWindow != null)
            deleteSuccessWindow.SetActive(false);

        if (deleteConfirmWindow != null)
        {
            deleteConfirmWindow.SetActive(true);
            deleteConfirmWindow.transform.SetAsLastSibling();
        }
    }

    public void CancelDeleteSaveData()
    {
        if (deleteConfirmWindow != null)
            deleteConfirmWindow.SetActive(false);
    }

    public void ConfirmDeleteSaveData()
    {
        // 1. Delete gameplay save data while preserving Settings preferences
        SaveSystem.DeleteAllGameplaySaveData();

        // 2. Immediately update Main Menu UI (disable Continue, lock & grey out Select Map)
        MainMenu mainMenu = FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (mainMenu != null)
        {
            mainMenu.RefreshMenuButtons();
        }

        MapSelectMenu mapSelectMenu = FindFirstObjectByType<MapSelectMenu>(FindObjectsInactive.Include);
        if (mapSelectMenu != null)
        {
            mapSelectMenu.UpdateMapSelectButtonState();
        }

        // 3. Close confirmation window and display the Save Data Deleted feedback window
        if (deleteConfirmWindow != null)
            deleteConfirmWindow.SetActive(false);

        if (deleteSuccessWindow != null)
        {
            deleteSuccessWindow.SetActive(true);
            deleteSuccessWindow.transform.SetAsLastSibling();
        }
    }

    public void AcknowledgeDeleteSuccess()
    {
        if (deleteSuccessWindow != null)
            deleteSuccessWindow.SetActive(false);

        if (deleteConfirmWindow != null)
            deleteConfirmWindow.SetActive(false);

        CloseSettings();
    }

    public void RefreshUIFromSavedSettings()
    {
        EnsureLoaded();
        isSyncingUI = true;
        try
        {
            if (bgMusicSlider != null)
                bgMusicSlider.SetValueWithoutNotify(BgMusicVolume);
            if (bgMusicInput != null)
                bgMusicInput.SetTextWithoutNotify(FormatValue(BgMusicVolume));

            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(SfxVolume);
            if (sfxInput != null)
                sfxInput.SetTextWithoutNotify(FormatValue(SfxVolume));

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(MouseSensitivity);
            if (mouseSensitivityInput != null)
                mouseSensitivityInput.SetTextWithoutNotify(FormatValue(MouseSensitivity));
        }
        finally
        {
            isSyncingUI = false;
        }
    }

    // ---------- Background Music Handlers ----------

    private void OnBgMusicSliderChanged(float value)
    {
        if (isSyncingUI) return;

        float clamped = RoundOneDecimal(Mathf.Clamp(value, MinBgMusic, MaxBgMusic));
        SetBgMusicSetting(clamped, updateSlider: false, updateInput: true);
    }

    private void OnBgMusicInputChanged(string text)
    {
        if (isSyncingUI) return;

        if (!TryParseFloat(text, out float parsed))
            return;

        if (parsed > MaxBgMusic)
        {
            SetBgMusicSetting(MaxBgMusic, updateSlider: true, updateInput: true);
        }
        else if (parsed >= MinBgMusic)
        {
            SetBgMusicSetting(RoundOneDecimal(parsed), updateSlider: true, updateInput: false);
        }
    }

    private void OnBgMusicInputEndEdit(string text)
    {
        if (isSyncingUI) return;

        if (TryParseFloat(text, out float parsed))
        {
            float clamped = RoundOneDecimal(Mathf.Clamp(parsed, MinBgMusic, MaxBgMusic));
            SetBgMusicSetting(clamped, updateSlider: true, updateInput: true);
        }
        else
        {
            // Invalid or empty input -> restore last valid value
            SetBgMusicSetting(BgMusicVolume, updateSlider: true, updateInput: true);
        }
    }

    private void SetBgMusicSetting(float value, bool updateSlider, bool updateInput)
    {
        isSyncingUI = true;
        try
        {
            BgMusicVolume = Mathf.Clamp(value, MinBgMusic, MaxBgMusic);
            PlayerPrefs.SetFloat(BgMusicPrefKey, BgMusicVolume);
            PlayerPrefs.Save();

            if (updateSlider && bgMusicSlider != null)
                bgMusicSlider.SetValueWithoutNotify(BgMusicVolume);

            if (updateInput && bgMusicInput != null)
                bgMusicInput.SetTextWithoutNotify(FormatValue(BgMusicVolume));

            ApplyBgMusicVolume();
        }
        finally
        {
            isSyncingUI = false;
        }
    }

    // ---------- Sound FX Handlers ----------

    private void OnSfxSliderChanged(float value)
    {
        if (isSyncingUI) return;

        float clamped = RoundOneDecimal(Mathf.Clamp(value, MinSfx, MaxSfx));
        SetSfxSetting(clamped, updateSlider: false, updateInput: true);
    }

    private void OnSfxInputChanged(string text)
    {
        if (isSyncingUI) return;

        if (!TryParseFloat(text, out float parsed))
            return;

        if (parsed > MaxSfx)
        {
            SetSfxSetting(MaxSfx, updateSlider: true, updateInput: true);
        }
        else if (parsed >= MinSfx)
        {
            SetSfxSetting(RoundOneDecimal(parsed), updateSlider: true, updateInput: false);
        }
    }

    private void OnSfxInputEndEdit(string text)
    {
        if (isSyncingUI) return;

        if (TryParseFloat(text, out float parsed))
        {
            float clamped = RoundOneDecimal(Mathf.Clamp(parsed, MinSfx, MaxSfx));
            SetSfxSetting(clamped, updateSlider: true, updateInput: true);
        }
        else
        {
            SetSfxSetting(SfxVolume, updateSlider: true, updateInput: true);
        }
    }

    private void SetSfxSetting(float value, bool updateSlider, bool updateInput)
    {
        isSyncingUI = true;
        try
        {
            SfxVolume = Mathf.Clamp(value, MinSfx, MaxSfx);
            PlayerPrefs.SetFloat(SfxPrefKey, SfxVolume);
            PlayerPrefs.Save();

            if (updateSlider && sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(SfxVolume);

            if (updateInput && sfxInput != null)
                sfxInput.SetTextWithoutNotify(FormatValue(SfxVolume));

            ApplySfxVolume();
        }
        finally
        {
            isSyncingUI = false;
        }
    }

    // ---------- Mouse Sensitivity Handlers ----------

    private void OnMouseSensitivitySliderChanged(float value)
    {
        if (isSyncingUI) return;

        float clamped = RoundOneDecimal(Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
        SetMouseSensitivitySetting(clamped, updateSlider: false, updateInput: true);
    }

    private void OnMouseSensitivityInputChanged(string text)
    {
        if (isSyncingUI) return;

        if (!TryParseFloat(text, out float parsed))
            return;

        if (parsed > MaxMouseSensitivity)
        {
            SetMouseSensitivitySetting(MaxMouseSensitivity, updateSlider: true, updateInput: true);
        }
        else if (parsed >= MinMouseSensitivity)
        {
            SetMouseSensitivitySetting(RoundOneDecimal(parsed), updateSlider: true, updateInput: false);
        }
    }

    private void OnMouseSensitivityInputEndEdit(string text)
    {
        if (isSyncingUI) return;

        if (TryParseFloat(text, out float parsed))
        {
            float clamped = RoundOneDecimal(Mathf.Clamp(parsed, MinMouseSensitivity, MaxMouseSensitivity));
            SetMouseSensitivitySetting(clamped, updateSlider: true, updateInput: true);
        }
        else
        {
            SetMouseSensitivitySetting(MouseSensitivity, updateSlider: true, updateInput: true);
        }
    }

    private void SetMouseSensitivitySetting(float value, bool updateSlider, bool updateInput)
    {
        isSyncingUI = true;
        try
        {
            MouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
            PlayerPrefs.SetFloat(MouseSensitivityPrefKey, MouseSensitivity);
            PlayerPrefs.Save();

            if (updateSlider && mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(MouseSensitivity);

            if (updateInput && mouseSensitivityInput != null)
                mouseSensitivityInput.SetTextWithoutNotify(FormatValue(MouseSensitivity));

            ApplyMouseSensitivity();
        }
        finally
        {
            isSyncingUI = false;
        }
    }

    // ---------- Helpers ----------

    private void CenterWindowOnCanvas()
    {
        if (settingsPanel == null)
            return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.rootCanvas != null)
            parentCanvas = parentCanvas.rootCanvas;

        RectTransform windowRect = settingsPanel.transform as RectTransform;
        RectTransform canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;

        if (windowRect != null && canvasRect != null)
        {
            windowRect.position = canvasRect.position;
        }
    }

    private static float RoundOneDecimal(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }

    private static string FormatValue(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static bool TryParseFloat(string text, out float value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0f;
            return false;
        }

        string normalized = text.Trim().Replace(',', '.');
        return float.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );
    }

    public void OnPointerDown(PointerEventData eventData) { }
    public void OnPointerUp(PointerEventData eventData) { }
    public void OnPointerClick(PointerEventData eventData) { }
}
