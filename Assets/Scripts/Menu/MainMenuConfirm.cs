using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuConfirm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public const string NeverShowAgainPrefKey = "MainMenuConfirm_NeverShowAgain";
    public const string WarningMessage = "Warning, going back to main menu will make you lose progress to this current map.";

    [Header("References")]
    [SerializeField] private PauseMenu pauseMenu;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private GameObject confirmationWindow;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button goBackButton;
    [SerializeField] private Toggle neverShowAgainToggle;

    private static Sprite cachedCheckmarkSprite;
    private bool isBypassingConfirmation;

    public bool IsBypassingConfirmation => isBypassingConfirmation;

    public static bool IsNeverShowAgainEnabled()
    {
        return PlayerPrefs.GetInt(NeverShowAgainPrefKey, 0) == 1;
    }

    public static void SetNeverShowAgainEnabled(bool enabled)
    {
        if (enabled)
        {
            PlayerPrefs.SetInt(NeverShowAgainPrefKey, 1);
        }
        else
        {
            PlayerPrefs.DeleteKey(NeverShowAgainPrefKey);
        }

        PlayerPrefs.Save();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureUI();

        if (confirmationWindow != null)
            confirmationWindow.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (pauseMenu == null)
        {
            Transform root = transform.root;
            if (root != null)
                pauseMenu = root.GetComponentInChildren<PauseMenu>(true);
            if (pauseMenu == null)
                pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        }

        if (mainMenuButton == null)
            mainMenuButton = GetComponent<Button>();

        if (confirmationWindow == null)
        {
            Transform child = transform.Find("ConfirmationWindow");
            if (child != null)
                confirmationWindow = child.gameObject;
        }

        if (confirmationWindow != null)
        {
            if (warningText == null)
            {
                Transform t = confirmationWindow.transform.Find("DialogPanel/WarningText");
                if (t != null) warningText = t.GetComponent<TMP_Text>();
            }

            if (confirmButton == null)
            {
                Transform t = confirmationWindow.transform.Find("DialogPanel/ConfirmButton");
                if (t != null) confirmButton = t.GetComponent<Button>();
            }

            if (goBackButton == null)
            {
                Transform t = confirmationWindow.transform.Find("DialogPanel/GoBackButton");
                if (t != null) goBackButton = t.GetComponent<Button>();
            }

            if (neverShowAgainToggle == null)
            {
                Transform t = confirmationWindow.transform.Find("DialogPanel/NeverShowAgainToggle");
                if (t != null) neverShowAgainToggle = t.GetComponent<Toggle>();
            }
        }
    }

    private void ConfigureUI()
    {
        if (warningText != null)
            warningText.text = WarningMessage;

        if (neverShowAgainToggle != null)
        {
            neverShowAgainToggle.SetIsOnWithoutNotify(false);

            if (neverShowAgainToggle.graphic is Image checkImg && checkImg.sprite == null)
                checkImg.sprite = GetOrCreateCheckmarkSprite();
        }

        if (mainMenuButton != null && mainMenuButton.onClick.GetPersistentEventCount() == 0)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuButtonClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        }

        if (confirmButton != null && confirmButton.onClick.GetPersistentEventCount() == 0)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (goBackButton != null && goBackButton.onClick.GetPersistentEventCount() == 0)
        {
            goBackButton.onClick.RemoveListener(OnGoBackClicked);
            goBackButton.onClick.AddListener(OnGoBackClicked);
        }
    }

    public void OnMainMenuButtonClicked()
    {
        ResolveReferences();

        // If the player previously checked "Never show this again" and clicked Confirm,
        // skip the confirmation window and go straight to the Main Menu.
        if (IsNeverShowAgainEnabled())
        {
            CloseConfirmationWindow();
            ProceedToMainMenu();
            return;
        }

        if (confirmationWindow != null && confirmationWindow.activeSelf)
            return;

        ShowConfirmationWindow();
    }

    public void ShowConfirmationWindow()
    {
        ResolveReferences();

        if (warningText != null)
            warningText.text = WarningMessage;

        // Always start unchecked when opening the window (unsaved checks from "Go Back" are discarded).
        if (neverShowAgainToggle != null)
            neverShowAgainToggle.SetIsOnWithoutNotify(false);

        if (confirmationWindow != null)
        {
            CenterWindowOnCanvas();
            confirmationWindow.SetActive(true);
        }

        if (mainMenuButton != null)
            mainMenuButton.enabled = false;
    }

    public void OnConfirmClicked()
    {
        // Only save the "Never show this again" preference when the checkbox is checked AND Confirm is clicked.
        if (neverShowAgainToggle != null && neverShowAgainToggle.isOn)
        {
            SetNeverShowAgainEnabled(true);
        }

        CloseConfirmationWindow();
        ProceedToMainMenu();
    }

    public void OnGoBackClicked()
    {
        // Do NOT save the preference when Go Back is clicked.
        if (neverShowAgainToggle != null)
            neverShowAgainToggle.SetIsOnWithoutNotify(false);

        CloseConfirmationWindow();
    }

    public void CloseConfirmationWindow()
    {
        if (neverShowAgainToggle != null)
            neverShowAgainToggle.SetIsOnWithoutNotify(false);

        if (confirmationWindow != null)
            confirmationWindow.SetActive(false);

        if (mainMenuButton != null)
            mainMenuButton.enabled = true;
    }

    private void ProceedToMainMenu()
    {
        // Reset current map's quest progress.
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetProgress();
        }

        if (pauseMenu == null)
            ResolveReferences();

        if (pauseMenu != null)
        {
            isBypassingConfirmation = true;
            try
            {
                pauseMenu.ExecuteGoToMainMenu();
            }
            finally
            {
                isBypassingConfirmation = false;
            }
        }
    }

    private void CenterWindowOnCanvas()
    {
        if (confirmationWindow == null)
            return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.rootCanvas != null)
            parentCanvas = parentCanvas.rootCanvas;

        RectTransform windowRect = confirmationWindow.transform as RectTransform;
        RectTransform canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;

        if (windowRect != null && canvasRect != null)
        {
            windowRect.position = canvasRect.position;
        }
    }

    private static Sprite GetOrCreateCheckmarkSprite()
    {
        if (cachedCheckmarkSprite != null)
            return cachedCheckmarkSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        // Draw a clean checkmark stroke
        DrawThickLine(tex, new Vector2(6f, 16f), new Vector2(13f, 9f), 2.6f, white);
        DrawThickLine(tex, new Vector2(13f, 9f), new Vector2(26f, 23f), 2.6f, white);

        tex.Apply();
        cachedCheckmarkSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return cachedCheckmarkSprite;
    }

    private static void DrawThickLine(Texture2D tex, Vector2 a, Vector2 b, float radius, Color color)
    {
        int w = tex.width;
        int h = tex.height;
        Vector2 ab = b - a;
        float abLenSq = ab.sqrMagnitude;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = abLenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq) : 0f;
                Vector2 closest = a + t * ab;
                float dist = Vector2.Distance(p, closest);
                if (dist <= radius)
                {
                    tex.SetPixel(x, y, color);
                }
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData) { }
    public void OnPointerUp(PointerEventData eventData) { }
    public void OnPointerClick(PointerEventData eventData) { }
}
