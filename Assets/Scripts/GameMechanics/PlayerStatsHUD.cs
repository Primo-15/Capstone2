using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerStatsHUD : MonoBehaviour
{
    public static bool IsTooltipOpen { get; private set; }

    [SerializeField] private Image[] heartIcons;
    [SerializeField] private Image[] staminaIcons;
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.2f;

    [Header("M3 Stage Tooltip (Assets/Prefabs/M3ToolTip)")]
    [SerializeField] private GameObject m3TooltipPanel;
    [SerializeField] private Button m3TooltipCloseButton;
    [SerializeField] private Sprite m3TooltipSprite;
    [SerializeField] private Texture2D m3TooltipTexture;
    [SerializeField] private bool showTooltipOnStart = true;
    [SerializeField] private string followCameraName = "PlayerFollowCamera";

    private Color[] heartBase;
    private Color[] staminaBase;
    private Sprite runtimeTooltipSprite;

    private ThirdPersonController playerController;
    private StarterAssetsInputs playerInputs;
    private CursorAltToggle cursorAltToggle;
    private readonly List<Behaviour> disabledCameraBehaviours = new List<Behaviour>();
    private bool restoreCursorThisFrame;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (m3TooltipSprite == null)
        {
            string sceneName = gameObject.scene.name;
            if (sceneName == "M5")
            {
                m3TooltipSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/M5ToolTip.png");
            }
            else
            {
                m3TooltipSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/M3ToolTip.png");
            }
        }
        if (m3TooltipTexture == null)
        {
            string sceneName = gameObject.scene.name;
            if (sceneName == "M5")
            {
                m3TooltipTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/M5ToolTip.png");
            }
            else
            {
                m3TooltipTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/M3ToolTip.png");
            }
        }
    }
#endif

    private void Awake()
    {
        IsTooltipOpen = false;
        heartBase = CaptureColors(heartIcons);
        staminaBase = CaptureColors(staminaIcons);
    }

    private void Start()
    {
        if (showTooltipOnStart)
        {
            EnsureTooltipUI();

            if (MapVideoController.IsVideoPlaying)
            {
                MapVideoController.OnIntroVideoFinished += HandleIntroFinishedForTooltip;
            }
            else
            {
                OpenTooltip();
            }
        }
    }

    private void HandleIntroFinishedForTooltip()
    {
        MapVideoController.OnIntroVideoFinished -= HandleIntroFinishedForTooltip;
        if (this != null && gameObject.activeInHierarchy && showTooltipOnStart)
        {
            OpenTooltip();
        }
    }

    private void OnDestroy()
    {
        MapVideoController.OnIntroVideoFinished -= HandleIntroFinishedForTooltip;
    }

    private void OnEnable()
    {
        PlayerStats.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerStats.OnChanged -= Refresh;
        if (IsTooltipOpen)
        {
            CloseTooltip();
        }
    }

    private void Update()
    {
        if (!IsTooltipOpen)
            return;

        KeepPlayerLockedAndCursorUnlocked();

        if (WasCloseKeyPressed())
        {
            CloseTooltip();
        }
    }

    private void LateUpdate()
    {
        if (IsTooltipOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (restoreCursorThisFrame)
        {
            restoreCursorThisFrame = false;
            ApplyGameplayCursorState();
        }
    }

    private bool WasCloseKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    private void EnsureTooltipUI()
    {
        if (m3TooltipPanel == null)
        {
            Transform existing = transform.Find("M3ToolTip");
            if (existing != null)
            {
                m3TooltipPanel = existing.gameObject;
            }
        }

        Sprite targetSprite = m3TooltipSprite;
        if (targetSprite == null && m3TooltipTexture != null)
        {
            if (runtimeTooltipSprite == null)
            {
                runtimeTooltipSprite = Sprite.Create(
                    m3TooltipTexture,
                    new Rect(0f, 0f, m3TooltipTexture.width, m3TooltipTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            targetSprite = runtimeTooltipSprite;
        }

        if (m3TooltipPanel == null)
        {
            m3TooltipPanel = new GameObject("M3ToolTip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            m3TooltipPanel.transform.SetParent(transform, false);

            RectTransform rect = m3TooltipPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1560f, 780f);
        }

        Image panelImage = m3TooltipPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            if (targetSprite != null)
                panelImage.sprite = targetSprite;
            panelImage.color = Color.white;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = true;
        }

        if (m3TooltipCloseButton == null)
        {
            Transform existingBtn = m3TooltipPanel.transform.Find("CloseButton");
            if (existingBtn != null)
            {
                m3TooltipCloseButton = existingBtn.GetComponent<Button>();
            }
        }

        if (m3TooltipCloseButton == null)
        {
            GameObject btnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(m3TooltipPanel.transform, false);

            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 1f);
            btnRect.anchoredPosition = new Vector2(-28f, -22f);
            btnRect.sizeDelta = new Vector2(115f, 44f);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.24f, 0.12f, 0.05f, 0.95f);

            m3TooltipCloseButton = btnObj.GetComponent<Button>();
            m3TooltipCloseButton.targetGraphic = btnImg;

            GameObject txtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnObj.transform, false);

            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.anchoredPosition = Vector2.zero;
            txtRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.text = "Close";
            tmp.fontSize = 22f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.97f, 0.91f, 0.78f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        if (m3TooltipCloseButton != null)
        {
            m3TooltipCloseButton.onClick.RemoveListener(CloseTooltip);
            m3TooltipCloseButton.onClick.AddListener(CloseTooltip);
        }
    }

    public void OpenTooltip()
    {
        if (m3TooltipPanel == null)
            return;

        IsTooltipOpen = true;
        m3TooltipPanel.transform.SetAsLastSibling();
        m3TooltipPanel.SetActive(true);

        CacheAndLockPlayer();
        KeepPlayerLockedAndCursorUnlocked();
    }

    public void CloseTooltip()
    {
        if (!IsTooltipOpen && (m3TooltipPanel == null || !m3TooltipPanel.activeSelf))
            return;

        IsTooltipOpen = false;

        if (m3TooltipPanel != null)
            m3TooltipPanel.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        UnlockPlayerAndRestoreCursor();
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
            playerController.enabled = false;

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

    private Color[] CaptureColors(Image[] icons)
    {
        if (icons == null) return new Color[0];
        var colors = new Color[icons.Length];
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) colors[i] = icons[i].color;
        return colors;
    }

    private void Refresh()
    {
        Apply(heartIcons, heartBase, PlayerStats.Hearts);
        Apply(staminaIcons, staminaBase, PlayerStats.Stamina);
    }

    private void Apply(Image[] icons, Color[] baseColors, int value)
    {
        if (icons == null || baseColors == null) return;
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null) continue;

            Color c = baseColors[i];
            if (i >= value) c.a = emptyAlpha;   // spent icon fades out
            icons[i].color = c;
        }
    }
}
