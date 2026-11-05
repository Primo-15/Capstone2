using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public class QTECanvasSetup
{
    static QTECanvasSetup()
    {
        EditorApplication.delayCall += ApplySetupQuietly;
    }

    private static void ApplySetupQuietly()
    {
        ApplySetup(false);
    }

    [MenuItem("Tools/Apply QTE Key Button Setup")]
    public static void ApplySetupMenuItem()
    {
        ApplySetup(true);
    }

    public static void ApplySetup(bool logVerbose)
    {
        GameObject qteCanvas = GameObject.Find("QTECanvas");
        if (qteCanvas == null)
        {
            if (logVerbose)
                Debug.LogWarning("[QTECanvasSetup] QTECanvas not found in active scene.");
            return;
        }

        Transform panel = qteCanvas.transform.Find("QTEPanel");
        if (panel == null)
        {
            if (logVerbose)
                Debug.LogWarning("[QTECanvasSetup] QTEPanel not found under QTECanvas.");
            return;
        }

        // Find KEYButtonBG (or RawImage)
        Transform keyButtonBG = panel.Find("KEYButtonBG");
        if (keyButtonBG == null)
            keyButtonBG = panel.Find("RawImage");

        Transform keyText = panel.Find("KeyText");

        if (keyButtonBG == null)
        {
            if (logVerbose)
                Debug.LogWarning("[QTECanvasSetup] Could not find KEYButtonBG or RawImage under QTEPanel.");
            return;
        }

        bool changed = false;

        // 1. Rename to KEYButtonBG if needed
        if (keyButtonBG.name != "KEYButtonBG")
        {
            Undo.RecordObject(keyButtonBG.gameObject, "Rename to KEYButtonBG");
            keyButtonBG.name = "KEYButtonBG";
            changed = true;
        }

        // 2. Ensure GameObject is active
        if (!keyButtonBG.gameObject.activeSelf)
        {
            Undo.RecordObject(keyButtonBG.gameObject, "Activate KEYButtonBG");
            keyButtonBG.gameObject.SetActive(true);
            changed = true;
        }

        // 3. Assign Transparent Black Key Material
        RawImage rawImage = keyButtonBG.GetComponent<RawImage>();
        if (rawImage != null)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KEYButtonBG_Material.mat");
            if (mat != null && rawImage.material != mat)
            {
                Undo.RecordObject(rawImage, "Assign KEYButtonBG Material");
                rawImage.material = mat;
                changed = true;
                if (logVerbose)
                    Debug.Log("[QTECanvasSetup] Assigned KEYButtonBG_Material to RawImage.");
            }
        }

        // 4. Ensure KeyText appears IN FRONT OF KEYButtonBG
        // In Unity UI, elements with higher sibling index render on top (in front).
        if (keyText != null)
        {
            int textIndex = keyText.GetSiblingIndex();
            int bgIndex = keyButtonBG.GetSiblingIndex();

            if (bgIndex > textIndex)
            {
                Undo.SetTransformParent(keyButtonBG, panel, "Reorder KEYButtonBG behind KeyText");
                // Place KEYButtonBG right before KeyText
                keyButtonBG.SetSiblingIndex(textIndex);
                changed = true;
                if (logVerbose)
                    Debug.Log($"[QTECanvasSetup] Moved KEYButtonBG before KeyText. New KEYButtonBG index={keyButtonBG.GetSiblingIndex()}, KeyText index={keyText.GetSiblingIndex()}");
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(qteCanvas);
            EditorSceneManager.MarkSceneDirty(qteCanvas.scene);
            if (logVerbose)
                Debug.Log("[QTECanvasSetup] Successfully configured QTECanvas! KeyText is now in front of KEYButtonBG, and KEYButtonBG has the transparent background material.");
        }
        else
        {
            if (logVerbose)
                Debug.Log("[QTECanvasSetup] QTECanvas is already properly configured.");
        }
    }
}

