using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD pointer for the quest system (QuestManager + QuestNPCInteraction).
///
/// Shows a pin over whatever the CURRENT quest objective needs, with the distance ("42m") and an
/// optional name. When the target is off-screen (or behind the camera) the pin sticks to the screen
/// edge with an arrow pointing toward it, so the player always knows which way to go.
///
/// Flow (nothing to script):
///   1. The NPC who starts a quest is marked until you talk to them.
///   2. After the talk, the pin moves to the next objective (e.g. the item to pick up).
///   3. After picking it up, the pin moves to where it has to go (the delivery NPC / place).
///   4. Quest finished: the pin moves to the next quest's NPC (once its prerequisites are met).
/// The pins hide while a dialogue (or the cutscene after it) is open, and fade in on the next
/// objective once it closes.
///
/// SETUP: put QuestPointerTarget on each NPC / item / place. This HUD creates itself automatically.
/// Optional: add this component to an empty object yourself to change the look or settings.
/// No sprites needed - it draws its own pin and arrow (you can drag your own in).
/// Needs TextMeshPro (already in the project) for the distance / name text.
/// </summary>
[DefaultExecutionOrder(10000)] // runs after camera scripts, so the pin doesn't shake
public class QuestPointer : MonoBehaviour
{
    public static QuestPointer Instance { get; private set; }

    [Header("Player and camera")]
    [Tooltip("The player, for the distance and 'Complete When Reached'. Empty = the object tagged 'Player'.")]
    public Transform player;
    [Tooltip("The camera the game is seen through. Empty = the Main Camera.")]
    public Camera targetCamera;

    [Header("Look")]
    [Tooltip("Optional pin sprite (its tip at the bottom centre). Empty = a built-in pin.")]
    public Sprite pinSprite;
    [Tooltip("Optional edge arrow sprite (pointing up). Empty = a built-in arrow.")]
    public Sprite arrowSprite;
    [Tooltip("Pin size in pixels at 1920x1080 (scales with the screen).")]
    public Vector2 pinSize = new Vector2(56f, 56f);
    public bool showDistance = true;
    [Tooltip("Show each target's name (its Label, or the NPC Name) above the pin while it's on screen.")]
    public bool showLabels = true;
    [Tooltip("How far from the screen edge the pin stays when its target is off-screen (pixels at 1080p).")]
    public float edgeMargin = 70f;
    [Tooltip("Seconds to fade a pin in or out.")]
    public float fadeTime = 0.35f;
    [Tooltip("Gentle up-and-down bob while the target is on screen.")]
    public bool bob = true;
    [Tooltip("Canvas sorting order. Higher = drawn on top of more of your UI.")]
    public int sortingOrder = 50;

    [Header("Behaviour")]
    [Tooltip("Hide the pins while an NPC dialogue (or the cutscene after it) is open. " +
             "The pin then appears on the next objective once it closes.")]
    public bool hideDuringDialogue = true;
    [Tooltip("Master switch (e.g. for a settings toggle).")]
    public bool showPointer = true;

    [Header("Stability (stops the pin shaking)")]
    [Tooltip("Targets closer than this to the camera lens (metres) are treated as off-screen, " +
             "because their screen position jumps wildly that close.")]
    public float minDepth = 0.6f;
    [Tooltip("Smooths pin movement. Higher = snappier, lower = smoother. 0 = off.")]
    public float positionSmoothing = 25f;

    class Marker
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image pin;
        public RectTransform arrow;
        public Image arrowImage;
        public TextMeshProUGUI distance;
        public TextMeshProUGUI label;

        public float visibility;      // 0..1 fade
        public bool seenThisFrame;
        public Vector2 lastLocal;
        public bool hadLast;
        public int lastDistance = -1;
        public string labelText;
        public float bobOffset;
    }

    readonly Dictionary<QuestPointerTarget, Marker> markers = new Dictionary<QuestPointerTarget, Marker>();
    readonly List<QuestPointerTarget> gone = new List<QuestPointerTarget>();

    Canvas canvas;
    RectTransform canvasRect;
    Camera cam;
    float nextPlayerSearch;

    /// <summary>The player (found by the 'Player' tag if not assigned).</summary>
    public Transform Player
    {
        get
        {
            FindPlayerIfNeeded();
            return player;
        }
    }

    /// <summary>Makes a QuestPointer if the scene doesn't have one yet (QuestPointerTarget calls this).</summary>
    public static void EnsureExists()
    {
        if (Instance == null)
            new GameObject("Quest Pointer (HUD)").AddComponent<QuestPointer>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // one is enough
            return;
        }

        Instance = this;
        BuildCanvas();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- every frame ----------

    void LateUpdate()
    {
        if (canvas == null) return;

        if (cam == null || !cam.isActiveAndEnabled) cam = FindCamera();
        FindPlayerIfNeeded();

        QuestManager manager = QuestManager.Instance;
        bool hideAll = !showPointer || (hideDuringDialogue && QuestNPCInteraction.IsInDialogue);
        float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;

        List<QuestPointerTarget> targets = QuestPointerTarget.All;
        for (int i = 0; i < targets.Count; i++)
        {
            QuestPointerTarget t = targets[i];
            if (t == null) continue;

            bool want = !hideAll && manager != null && t.IsWanted(manager);

            Marker m;
            if (!markers.TryGetValue(t, out m))
            {
                if (!want) continue;
                m = BuildMarker();
                markers.Add(t, m);
            }

            m.seenThisFrame = true;
            m.visibility = Mathf.MoveTowards(m.visibility, want ? 1f : 0f, step);

            if (m.visibility <= 0.001f || cam == null) Hide(m);
            else Draw(t, m);
        }

        // targets that were switched off or destroyed
        gone.Clear();
        foreach (KeyValuePair<QuestPointerTarget, Marker> kv in markers)
        {
            if (kv.Key == null) gone.Add(kv.Key);
            else if (!kv.Value.seenThisFrame) { Hide(kv.Value); kv.Value.visibility = 0f; }
            kv.Value.seenThisFrame = false;
        }
        foreach (QuestPointerTarget t in gone)
        {
            Marker m = markers[t];
            if (m.root != null) Destroy(m.root.gameObject);
            markers.Remove(t);
        }
    }

    void Draw(QuestPointerTarget t, Marker m)
    {
        Vector3 world = t.MarkerPosition;
        Vector3 sp = cam.WorldToScreenPoint(world);

        // very close to the lens (or behind it) a projected point jumps around - treat it as
        // off-screen and use the camera-space direction instead, which is stable
        bool behind = sp.z < minDepth;

        // area the pin may use; extra room at the top for the pin + distance drawn above the point
        float sf = canvas.scaleFactor;
        float side = edgeMargin * sf;
        float top = (edgeMargin + pinSize.y + (showDistance ? 34f : 0f)) * sf;
        Rect safe = Rect.MinMaxRect(side, side,
                                    Mathf.Max(side + 1f, Screen.width - side),
                                    Mathf.Max(side + 1f, Screen.height - top));
        Vector2 centre = safe.center;
        Vector2 point = new Vector2(sp.x, sp.y);

        bool onScreen = !behind && safe.Contains(point);

        Vector2 dir;
        if (behind)
        {
            Vector3 camLocal = cam.transform.InverseTransformPoint(world);
            dir = new Vector2(camLocal.x, camLocal.y);
            dir = dir.sqrMagnitude < 0.000001f ? Vector2.down : dir.normalized;
        }
        else
        {
            dir = point - centre;
        }

        Vector2 screenPos;
        if (onScreen)
        {
            screenPos = point;
        }
        else
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            Vector2 half = safe.size * 0.5f;
            float k = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(dir.x), 0.0001f),
                                half.y / Mathf.Max(Mathf.Abs(dir.y), 0.0001f));
            screenPos = centre + dir * k;
        }

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out local);

        if (onScreen && bob) local.y += Mathf.Sin(Time.unscaledTime * 3f + m.bobOffset) * 5f;

        if (!m.root.gameObject.activeSelf)
        {
            m.root.gameObject.SetActive(true);
            m.hadLast = false;
        }

        // light smoothing against jitter (a big jump, like going on/off screen, snaps instead)
        if (positionSmoothing > 0f && m.hadLast && (local - m.lastLocal).sqrMagnitude < 300f * 300f)
            local = Vector2.Lerp(m.lastLocal, local, 1f - Mathf.Exp(-positionSmoothing * Time.unscaledDeltaTime));
        m.lastLocal = local;
        m.hadLast = true;
        m.root.anchoredPosition = local;

        m.pin.color = t.color;

        // edge arrow, pointing toward the target
        bool showArrow = !onScreen;
        if (m.arrow.gameObject.activeSelf != showArrow) m.arrow.gameObject.SetActive(showArrow);
        if (showArrow)
        {
            Vector2 n = dir.normalized;
            m.arrow.anchoredPosition = new Vector2(0f, pinSize.y * 0.62f) + n * (pinSize.x * 0.85f);
            m.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg - 90f);
            m.arrowImage.color = t.color;
        }

        // distance
        if (m.distance.gameObject.activeSelf != showDistance) m.distance.gameObject.SetActive(showDistance);
        if (showDistance)
        {
            Vector3 from = player != null ? player.position : cam.transform.position;
            int d = Mathf.RoundToInt(Vector3.Distance(from, t.Position));
            if (d != m.lastDistance)
            {
                m.lastDistance = d;
                m.distance.text = d + "m";
            }
        }

        // name, only while on screen (the edge stays clean)
        string label = showLabels && onScreen ? t.Label : null;
        bool showLabel = !string.IsNullOrEmpty(label);
        if (m.label.gameObject.activeSelf != showLabel) m.label.gameObject.SetActive(showLabel);
        if (showLabel)
        {
            m.label.rectTransform.anchoredPosition = new Vector2(0f, pinSize.y + (showDistance ? 46f : 18f));
            if (m.labelText != label)
            {
                m.labelText = label;
                m.label.text = label;
            }
        }

        m.group.alpha = m.visibility;
    }

    static void Hide(Marker m)
    {
        if (m.root.gameObject.activeSelf) m.root.gameObject.SetActive(false);
        m.hadLast = false;
    }

    Camera FindCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (Camera.main != null) return Camera.main;
        return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
    }

    void FindPlayerIfNeeded()
    {
        if (player != null || Time.unscaledTime < nextPlayerSearch) return;
        nextPlayerSearch = Time.unscaledTime + 1f;

        GameObject go = GameObject.FindWithTag("Player");
        if (go != null) player = go.transform;
    }

    // ---------- UI building ----------

    void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("QuestPointer_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvasRect = canvasObj.GetComponent<RectTransform>();

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    Marker BuildMarker()
    {
        Marker m = new Marker();

        GameObject root = new GameObject("Pin", typeof(RectTransform), typeof(CanvasGroup));
        m.root = (RectTransform)root.transform;
        m.root.SetParent(canvasRect, false);
        m.root.sizeDelta = Vector2.zero;
        m.group = root.GetComponent<CanvasGroup>();
        m.group.blocksRaycasts = false;
        m.group.interactable = false;

        // the pin's tip sits exactly on the target point
        m.pin = NewImage("Pin", m.root, pinSprite != null ? pinSprite : QuestPointerArt.Pin);
        RectTransform pr = m.pin.rectTransform;
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = pinSize;
        pr.anchoredPosition = Vector2.zero;

        m.arrowImage = NewImage("Arrow", m.root, arrowSprite != null ? arrowSprite : QuestPointerArt.Arrow);
        m.arrow = m.arrowImage.rectTransform;
        m.arrow.sizeDelta = pinSize * 0.55f;
        m.arrow.gameObject.SetActive(false);

        m.distance = NewText("Distance", m.root, 26f, pinSize.y + 16f);
        m.label = NewText("Label", m.root, 24f, pinSize.y + 46f);
        m.label.gameObject.SetActive(false);

        m.bobOffset = Random.value * 6.28f;

        root.SetActive(false);
        return m;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, float size, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;                       // dark outline so it reads on any background
        t.outlineColor = new Color32(0, 0, 0, 220);

        RectTransform r = t.rectTransform;
        r.sizeDelta = new Vector2(400f, 40f);
        r.anchoredPosition = new Vector2(0f, y);
        return t;
    }
}

/// <summary>Draws the built-in pin and arrow in code, so no image files have to be imported.</summary>
static class QuestPointerArt
{
    const int N = 128;
    static Sprite pin, arrow;

    public static Sprite Pin
    {
        get
        {
            if (pin == null) pin = Make(PinPixel, new Vector2(0.5f, 0f));
            return pin;
        }
    }

    public static Sprite Arrow
    {
        get
        {
            if (arrow == null) arrow = Make(ArrowPixel, new Vector2(0.5f, 0.5f));
            return arrow;
        }
    }

    delegate Color PixelFn(Vector2 uv);

    static Sprite Make(PixelFn fn, Vector2 pivot)
    {
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset; // survive scene changes

        Color[] px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                px[y * N + x] = fn(new Vector2((x + 0.5f) / N, (y + 0.5f) / N));

        tex.SetPixels(px);
        tex.Apply(false, true);

        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, N, N), pivot, 100f);
        s.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return s;
    }

    // map pin: round head + point. White body (tinted by the target's colour), dark outline, dark centre dot.
    static Color PinPixel(Vector2 p)
    {
        Vector2 c = new Vector2(0.5f, 0.64f);
        float head = SdCircle(p, c, 0.29f);
        float tip = SdTriangle(p, new Vector2(0.255f, 0.55f), new Vector2(0.745f, 0.55f), new Vector2(0.5f, 0.06f));
        float shape = Mathf.Min(head, tip);
        float dot = SdCircle(p, c, 0.11f);

        Color col = new Color(0f, 0f, 0f, 0f);
        col = Over(col, new Color(0f, 0f, 0f, 0.6f), Cover(shape - 0.04f));
        col = Over(col, Color.white, Cover(shape));
        col = Over(col, new Color(0.15f, 0.15f, 0.15f, 1f), Cover(dot));
        return col;
    }

    // triangle pointing up, white (tinted) with a dark outline
    static Color ArrowPixel(Vector2 p)
    {
        float tri = SdTriangle(p, new Vector2(0.5f, 0.9f), new Vector2(0.14f, 0.22f), new Vector2(0.86f, 0.22f));

        Color col = new Color(0f, 0f, 0f, 0f);
        col = Over(col, new Color(0f, 0f, 0f, 0.6f), Cover(tri - 0.05f));
        col = Over(col, Color.white, Cover(tri));
        return col;
    }

    // 1-pixel anti-aliased coverage of a signed distance (negative = inside)
    static float Cover(float d) => Mathf.Clamp01(0.5f - d * N);

    static Color Over(Color dst, Color src, float amount)
    {
        float sa = src.a * amount;
        float outA = sa + dst.a * (1f - sa);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);

        Color o = (src * sa + dst * (dst.a * (1f - sa))) / outA;
        o.a = outA;
        return o;
    }

    static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    // signed distance to a triangle (Inigo Quilez)
    static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
        Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        Vector2 d = Vector2.Min(Vector2.Min(
                        new Vector2(pq0.sqrMagnitude, s * (v0.x * e0.y - v0.y * e0.x)),
                        new Vector2(pq1.sqrMagnitude, s * (v1.x * e1.y - v1.y * e1.x))),
                        new Vector2(pq2.sqrMagnitude, s * (v2.x * e2.y - v2.y * e2.x)));
        return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
    }
}
