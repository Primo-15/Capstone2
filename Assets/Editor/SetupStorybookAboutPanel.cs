using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Rebuilds the About panel in the Main Menu scene to look like
/// an open storybook with an animated open/close.
/// Run via: Tools > Setup Storybook About Panel
/// </summary>
public class SetupStorybookAboutPanel : MonoBehaviour
{
    [MenuItem("Tools/Setup Storybook About Panel")]
    public static void Run()
    {
        // ── 1. Load Main Menu scene ──────────────────────────────────────
        var mainMenuScene = "Assets/Scenes/Main Menu.unity";
        if (!EditorSceneManager.GetActiveScene().name.Contains("Main Menu"))
        {
            if (EditorUtility.DisplayDialog("Switch Scene?",
                "The Main Menu scene is not active. Open it now?", "Yes", "Cancel"))
            {
                EditorSceneManager.OpenScene(mainMenuScene);
            }
            else return;
        }

        // ── 2. Find the Canvas ───────────────────────────────────────────
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[StorybookAbout] Canvas not found!"); return; }

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();

        // ── 3. Remove old AboutPanel ─────────────────────────────────────
        var oldPanel = GameObject.Find("AboutPanel");
        if (oldPanel != null)
        {
            Undo.DestroyObjectImmediate(oldPanel);
            Debug.Log("[StorybookAbout] Removed old AboutPanel");
        }

        // Also remove any existing dim overlay
        var oldDim = GameObject.Find("AboutDimOverlay");
        if (oldDim != null) Undo.DestroyObjectImmediate(oldDim);

        // ── 4. Create Dim Overlay ────────────────────────────────────────
        var dimGO = new GameObject("AboutDimOverlay");
        Undo.RegisterCreatedObjectUndo(dimGO, "Create Dim Overlay");
        dimGO.transform.SetParent(canvas.transform, false);
        dimGO.SetActive(false);

        var dimRT = dimGO.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget = true;

        // ── 5. Create Book Root Panel ────────────────────────────────────
        // The book is sized to match what the user showed in the screenshot:
        // roughly 55% of screen width, full-ish height, left-side-of-centre.
        var bookGO = new GameObject("AboutPanel");
        Undo.RegisterCreatedObjectUndo(bookGO, "Create Storybook Panel");
        bookGO.transform.SetParent(canvas.transform, false);
        bookGO.SetActive(false);

        var bookRT = bookGO.AddComponent<RectTransform>();
        // Anchored to canvas centre-left, like the screenshot
        bookRT.anchorMin = new Vector2(0f, 0f);
        bookRT.anchorMax = new Vector2(0f, 0f);
        bookRT.pivot = new Vector2(0.5f, 0.5f);
        bookRT.anchoredPosition = new Vector2(480f, 540f);  // roughly left half, vertically centred
        bookRT.sizeDelta = new Vector2(880f, 980f);         // big book page
        bookRT.localScale = Vector3.zero;                    // start collapsed for animation

        // Book background – warm parchment-tan colour (matches image)
        var bookImg = bookGO.AddComponent<Image>();
        bookImg.color = new Color(0.945f, 0.882f, 0.773f, 1f);  // #F1E1C5
        bookImg.type = Image.Type.Sliced;
        bookImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bookImg.raycastTarget = true;

        // ── 6. Brown border frame ────────────────────────────────────────
        var borderGO = new GameObject("BookBorder");
        borderGO.transform.SetParent(bookGO.transform, false);

        var borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(10f, 10f);
        borderRT.offsetMax = new Vector2(-10f, -10f);

        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(0.42f, 0.235f, 0.098f, 1f);  // dark brown
        borderImg.type = Image.Type.Sliced;
        borderImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // Inner fill to show parchment inside border
        var innerGO = new GameObject("BookInner");
        innerGO.transform.SetParent(bookGO.transform, false);

        var innerRT = innerGO.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(22f, 22f);
        innerRT.offsetMax = new Vector2(-22f, -22f);

        var innerImg = innerGO.AddComponent<Image>();
        innerImg.color = new Color(0.945f, 0.882f, 0.773f, 1f);
        innerImg.type = Image.Type.Sliced;
        innerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ── 7. Title "About" at top ──────────────────────────────────────
        var titleGO = new GameObject("BookTitle");
        titleGO.transform.SetParent(bookGO.transform, false);

        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -28f);
        titleRT.sizeDelta = new Vector2(-80f, 80f);

        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "About the Game";
        titleTxt.fontSize = 46f;
        titleTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.28f, 0.14f, 0.04f, 1f);  // dark brown ink

        // ── 8. Divider line under title ──────────────────────────────────
        var div1GO = new GameObject("Divider1");
        div1GO.transform.SetParent(bookGO.transform, false);

        var div1RT = div1GO.AddComponent<RectTransform>();
        div1RT.anchorMin = new Vector2(0.05f, 1f);
        div1RT.anchorMax = new Vector2(0.95f, 1f);
        div1RT.pivot = new Vector2(0.5f, 1f);
        div1RT.anchoredPosition = new Vector2(0f, -100f);
        div1RT.sizeDelta = new Vector2(0f, 3f);

        var div1Img = div1GO.AddComponent<Image>();
        div1Img.color = new Color(0.42f, 0.235f, 0.098f, 0.6f);

        // ── 9. Main scrollable text ──────────────────────────────────────
        // Using a ScrollRect so the text can scroll if needed
        var scrollGO = new GameObject("TextScroll");
        scrollGO.transform.SetParent(bookGO.transform, false);

        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.04f, 0.08f);
        scrollRT.anchorMax = new Vector2(0.96f, 0.87f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;

        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0f);  // transparent

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;

        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);

        var viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        var viewportMask = viewportGO.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        viewportGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        scroll.viewport = viewportRT;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);

        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 0f);

        var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var vLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(10, 10, 10, 20);
        vLayout.spacing = 24f;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandHeight = false;

        scroll.content = contentRT;

        // ── 10. Text paragraphs ──────────────────────────────────────────
        string[] paragraphs = new string[]
        {
            "The story begins with <b>Isay</b>, a young student who seeks her grandmother, " +
            "<b>Lola Ligaya</b>, about her experiences during the Japanese occupation of the Philippines. " +
            "As Lola Ligaya shares her memories, Isay falls asleep and finds herself experiencing " +
            "the events of her grandmother\u2019s past.",

            "Set in a peaceful rural community, the story follows a Filipino family facing various " +
            "struggles like its suddenly disrupted by the arrival of Japanese military forces. " +
            "Separated from her family during the invasion, the young <b>Isayya</b> must find a way " +
            "to survive while watching enemy soldiers and navigating dangerous surroundings.",

            "Along her journey, she encounters members of the <b>LUKBA-LALAYA (Bukkno ng Bayan)</b> \u2013 " +
            "a Filipino resistance movement fighting against Japanese occupation. She becomes involved " +
            "in their efforts by secretly gathering and delivering information to help the resistance " +
            "reclaim their community."
        };

        Color inkColor = new Color(0.15f, 0.09f, 0.04f, 1f);

        for (int i = 0; i < paragraphs.Length; i++)
        {
            // Paragraph text
            var paraGO = new GameObject($"Paragraph{i + 1}");
            paraGO.transform.SetParent(contentGO.transform, false);

            var paraRT = paraGO.AddComponent<RectTransform>();
            paraRT.sizeDelta = new Vector2(0f, 0f);

            var paraFitter = paraGO.AddComponent<ContentSizeFitter>();
            paraFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var paraTxt = paraGO.AddComponent<TextMeshProUGUI>();
            paraTxt.text = paragraphs[i];
            paraTxt.fontSize = 26f;
            paraTxt.alignment = TextAlignmentOptions.TopJustified;
            paraTxt.color = inkColor;
            paraTxt.enableWordWrapping = true;

            // Decorative divider between paragraphs (not after last)
            if (i < paragraphs.Length - 1)
            {
                var divGO = new GameObject($"ParaDivider{i + 1}");
                divGO.transform.SetParent(contentGO.transform, false);

                var divRT = divGO.AddComponent<RectTransform>();
                divRT.sizeDelta = new Vector2(0f, 24f);

                var divImg2 = divGO.AddComponent<Image>();
                divImg2.color = new Color(0f, 0f, 0f, 0f);  // invisible spacer
            }
        }

        // ── 11. Close Button ─────────────────────────────────────────────
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(bookGO.transform, false);

        var closeBtnRT = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(0.5f, 0f);
        closeBtnRT.anchorMax = new Vector2(0.5f, 0f);
        closeBtnRT.pivot = new Vector2(0.5f, 0f);
        closeBtnRT.anchoredPosition = new Vector2(0f, 22f);
        closeBtnRT.sizeDelta = new Vector2(200f, 50f);

        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.42f, 0.235f, 0.098f, 1f);
        closeBtnImg.type = Image.Type.Sliced;
        closeBtnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnImg;
        var cc = closeBtn.colors;
        cc.normalColor = new Color(0.42f, 0.235f, 0.098f, 1f);
        cc.highlightedColor = new Color(0.56f, 0.33f, 0.13f, 1f);
        cc.pressedColor = new Color(0.28f, 0.14f, 0.04f, 1f);
        cc.selectedColor = cc.normalColor;
        closeBtn.colors = cc;

        // Close button label
        var closeLblGO = new GameObject("CloseLabel");
        closeLblGO.transform.SetParent(closeBtnGO.transform, false);

        var closeLblRT = closeLblGO.AddComponent<RectTransform>();
        closeLblRT.anchorMin = Vector2.zero;
        closeLblRT.anchorMax = Vector2.one;
        closeLblRT.offsetMin = Vector2.zero;
        closeLblRT.offsetMax = Vector2.zero;

        var closeLblTxt = closeLblGO.AddComponent<TextMeshProUGUI>();
        closeLblTxt.text = "Close";
        closeLblTxt.fontSize = 28f;
        closeLblTxt.fontStyle = FontStyles.Bold;
        closeLblTxt.alignment = TextAlignmentOptions.Center;
        closeLblTxt.color = new Color(0.945f, 0.882f, 0.773f, 1f);

        // ── 12. Wire up AboutPanelController ─────────────────────────────
        // Find the existing AboutPanelController on the scene's manager GO
        var ctrl = Object.FindFirstObjectByType<AboutPanelController>();
        if (ctrl == null)
        {
            Debug.LogError("[StorybookAbout] AboutPanelController not found in scene. " +
                           "Please attach it to a persistent manager object.");
        }
        else
        {
            Undo.RecordObject(ctrl, "Wire About Controller");

            // Assign aboutPanel via reflection (since it's SerializeField private)
            var so = new SerializedObject(ctrl);
            so.FindProperty("aboutPanel").objectReferenceValue = bookGO;
            so.FindProperty("dimOverlay").objectReferenceValue = dimImg;
            so.ApplyModifiedProperties();

            Debug.Log("[StorybookAbout] Wired AboutPanel and DimOverlay to controller.");
        }

        // Wire close button to CloseAbout
        if (ctrl != null)
        {
            var closeEntry = new UnityEngine.Events.UnityAction(ctrl.CloseAbout);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtn.onClick, closeEntry);
        }

        // ── 13. Mark scene dirty & save ──────────────────────────────────
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("[StorybookAbout] ✅ Storybook About panel created and scene saved!");
        EditorUtility.DisplayDialog("Done!", 
            "Storybook About panel created!\n\n" +
            "• Press 'About' button to see the animated open\n" +
            "• The book slides in from scale 0 with a bounce\n" +
            "• Press 'Close' or Escape to dismiss",
            "Great!");
    }
}

