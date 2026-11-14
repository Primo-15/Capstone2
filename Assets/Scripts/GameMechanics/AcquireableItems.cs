using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using StarterAssets;

/// <summary>
/// Collectible world item and foundation for the Scene M5 Hotbar & Item Interaction System.
/// Supports "Rock" (chargeable/throwable with trajectory and guard distraction)
/// and "Key" (usable with Left Mouse Button to unlock gate_GEO).
/// </summary>
public class AcquireableItems : MonoBehaviour
{
    [Header("Item Identification")]
    [Tooltip("Identifies the type of item: \"Rock\" or \"Key\".")]
    public string ItemName;

    [Header("Hotbar Icons (Assets/Prefabs)")]
    [Tooltip("Icon used when acquiring a Rock (Stone_CutOut).")]
    public Texture2D rockIcon;

    [Tooltip("Icon used when acquiring a Key (Key_CutOut).")]
    public Texture2D keyIcon;

    [Header("Key Target Gate")]
    [Tooltip("Reference to gate_GEO for Key usage (auto-found in M5 if left empty).")]
    public GameObject targetGate;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 2.6f;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Header("Left Hand Attachment (Reference: KeyPickup.cs)")]
    [SerializeField] private HumanBodyBones handBone = HumanBodyBones.LeftHand;
    [SerializeField] private Vector3 rockHandPosition = new Vector3(0.015f, 0.08f, 0.03f);
    [SerializeField] private Vector3 rockHandEulerAngles = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 rockHandScale = new Vector3(0.35f, 0.35f, 0.35f);
    [SerializeField] private Vector3 keyHandPosition = new Vector3(-1.355f, -1.305f, -0.71f);
    [SerializeField] private Vector3 keyHandEulerAngles = new Vector3(41.066757f, 241.34666f, 320.36475f);
    [SerializeField] private Vector3 keyHandScale = new Vector3(0.0002459862f, 0.0002459863f, 0.00024598636f);

    private static readonly List<AcquireableItems> ActiveWorldItems = new List<AcquireableItems>();

    private Collider[] cachedColliders;
    private Renderer[] cachedRenderers;
    private float spawnProtectUntil;

    public float PickupRange => pickupRange;
    public KeyCode PickupKey => pickupKey;
    public HumanBodyBones HandBone => handBone;
    public Vector3 RockHandPosition => rockHandPosition;
    public Vector3 RockHandEulerAngles => rockHandEulerAngles;
    public Vector3 RockHandScale => rockHandScale;
    public Vector3 KeyHandPosition => keyHandPosition;
    public Vector3 KeyHandEulerAngles => keyHandEulerAngles;
    public Vector3 KeyHandScale => keyHandScale;
    public bool CanBePickedUpNow => Time.time >= spawnProtectUntil;

    public static IReadOnlyList<AcquireableItems> WorldItems => ActiveWorldItems;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ItemName = InferItemNameFromObject(gameObject.name);
        }

        if (rockIcon == null)
        {
            rockIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Stone_CutOut.png");
        }

        if (keyIcon == null)
        {
            keyIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Key_CutOut.png");
        }
    }
#endif

    private void Awake()
    {
        if (!IsM5Scene())
            return;

        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ItemName = InferItemNameFromObject(gameObject.name);
        }
        else
        {
            ItemName = NormalizeItemName(ItemName);
        }

        // Reference KeyPickup settings if present on the Key object, and disable KeyPickup so AcquireableItems handles it
        KeyPickup existingKeyPickup = GetComponent<KeyPickup>();
        if (existingKeyPickup != null)
        {
            handBone = existingKeyPickup.HandBone;
            keyHandPosition = existingKeyPickup.CarryPosition;
            keyHandEulerAngles = existingKeyPickup.CarryEulerAngles;
            keyHandScale = existingKeyPickup.CarryScale;
            existingKeyPickup.enabled = false;
        }

        EnsureColliderExists();
        RefreshCaches();

        M5HotbarController controller = M5HotbarController.EnsureInstance();
        if (controller != null)
        {
            controller.RegisterItemConfiguration(this);
        }
    }

    private void Start()
    {
        if (!IsM5Scene())
            return;

        M5HotbarController controller = M5HotbarController.EnsureInstance();
        if (controller != null)
        {
            controller.RegisterItemConfiguration(this);
        }
    }

    private void OnEnable()
    {
        if (!ActiveWorldItems.Contains(this))
        {
            ActiveWorldItems.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveWorldItems.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveWorldItems.Remove(this);
    }

    public void SetSpawnPickupDelay(float delaySeconds)
    {
        spawnProtectUntil = Time.time + Mathf.Max(0f, delaySeconds);
    }

    public string GetResolvedItemName()
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ItemName = InferItemNameFromObject(gameObject.name);
        }
        return NormalizeItemName(ItemName);
    }

    public static string NormalizeItemName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string trimmed = rawName.Trim();
        if (trimmed.Equals("Rock", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Stone", StringComparison.OrdinalIgnoreCase))
        {
            return "Rock";
        }

        if (trimmed.Equals("Key", StringComparison.OrdinalIgnoreCase))
        {
            return "Key";
        }

        return trimmed;
    }

    private static string InferItemNameFromObject(string objName)
    {
        if (string.IsNullOrEmpty(objName))
            return string.Empty;

        string lower = objName.ToLowerInvariant();
        if (lower.Contains("key"))
            return "Key";
        if (lower.Contains("stone") || lower.Contains("rock"))
            return "Rock";

        return string.Empty;
    }

    public void EnsureColliderExists()
    {
        Collider existing = GetComponentInChildren<Collider>();
        if (existing != null)
            return;

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds meshBounds = mf.sharedMesh.bounds;
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.center = meshBounds.center;
            float maxExtent = Mathf.Max(meshBounds.extents.x, meshBounds.extents.y, meshBounds.extents.z);
            sphere.radius = Mathf.Max(0.12f, maxExtent);
            sphere.isTrigger = false;
            return;
        }

        MeshFilter childMf = GetComponentInChildren<MeshFilter>();
        if (childMf != null && childMf.sharedMesh != null)
        {
            Bounds meshBounds = childMf.sharedMesh.bounds;
            BoxCollider box = childMf.gameObject.AddComponent<BoxCollider>();
            box.center = meshBounds.center;
            box.size = Vector3.Max(meshBounds.size, new Vector3(0.15f, 0.15f, 0.15f));
            box.isTrigger = false;
            return;
        }

        SphereCollider fallback = gameObject.AddComponent<SphereCollider>();
        fallback.radius = 0.25f;
        fallback.isTrigger = false;
    }

    public void RefreshCaches()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public Vector3 GetWorldInteractionCenter()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            RefreshCaches();

        if (cachedRenderers != null)
        {
            bool hasBounds = false;
            Bounds combined = default;
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer r = cachedRenderers[i];
                if (r != null && r.enabled)
                {
                    if (!hasBounds)
                    {
                        combined = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(r.bounds);
                    }
                }
            }

            if (hasBounds)
                return combined.center;
        }

        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider c = cachedColliders[i];
                if (c != null && c.enabled)
                    return c.bounds.center;
            }
        }

        return transform.position;
    }

    private static bool IsM5Scene()
    {
        Scene active = SceneManager.GetActiveScene();
        return active.IsValid() && active.name == "M5";
    }
}

/// <summary>
/// Manages the 3-slot Hotbar UI, Left-Hand equipped item display, E pickup, G drop,
/// Rock charging/throwing with trajectory prediction & guard distraction, and Key usage on gate_GEO
/// exclusively in Scene M5.
/// </summary>
public class M5HotbarController : MonoBehaviour
{
    public const int SlotCount = 3;

    private static M5HotbarController instance;
    public static M5HotbarController Instance => instance;

    [Header("Hotbar Icons")]
    public Texture2D rockIcon;
    public Texture2D keyIcon;

    [Header("Gate Target (gate_GEO)")]
    public GameObject gateGeoObject;
    public GateController gateController;
    public bool consumeKeyOnGateUnlock = false;

    [Header("Rock Throw Settings")]
    public float minThrowSpeed = 5.0f;
    public float maxThrowSpeed = 16.5f;
    public float maxChargeTime = 1.4f;
    public float throwUpwardBias = 0.22f;
    public float guardDistractionRadius = 4.5f;
    public float guardInvestigateSeconds = 2.0f;

    [Header("Interaction Ranges")]
    public float itemPickupRange = 2.6f;
    public float gateInteractRange = 4.2f;

    // Hotbar state: 3 non-stacking slots
    private readonly string[] slotItems = new string[SlotCount];
    private int equippedSlotIndex = -1;

    // Player & Camera references
    private Transform playerTransform;
    private CharacterController playerCharController;
    private Camera mainCam;
    private Transform leftHandBone;
    private HumanBodyBones preferredHandBone = HumanBodyBones.LeftHand;

    // Left handtransform settings (copied from AcquireableItems / KeyPickup)
    private Vector3 rockHandPos = new Vector3(0.015f, 0.08f, 0.03f);
    private Vector3 rockHandEuler = new Vector3(0f, 0f, 0f);
    private Vector3 rockHandScale = new Vector3(0.35f, 0.35f, 0.35f);
    private Vector3 keyHandPos = new Vector3(-1.355f, -1.305f, -0.71f);
    private Vector3 keyHandEuler = new Vector3(41.066757f, 241.34666f, 320.36475f);
    private Vector3 keyHandScale = new Vector3(0.0002459862f, 0.0002459863f, 0.00024598636f);

    // Templates & Left-hand visual objects
    private Transform templateContainer;
    private readonly Dictionary<string, GameObject> worldItemTemplates = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> handVisualObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    // Throw charging & trajectory state
    private bool isChargingRock;
    private float currentChargeTime;
    private LineRenderer trajectoryLine;
    private LineRenderer landingRingLine;
    private Material trajectoryMaterial;

    // Hotbar UI elements
    private Canvas hotbarCanvas;
    private bool hiddenByExternalSystem;
    private RectTransform[] slotRects = new RectTransform[SlotCount];
    private Image[] slotBackgrounds = new Image[SlotCount];
    private GameObject[] slotSelectionOutlines = new GameObject[SlotCount];
    private RawImage[] slotIconImages = new RawImage[SlotCount];
    private Text equippedLabelText;
    private Text promptLabelText;
    private Text statusToastText;
    private float statusToastTimer;

    // Current hovered item
    private AcquireableItems hoveredItem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeForM5()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.name != "M5")
            return;

        EnsureInstance();
    }

    public static M5HotbarController EnsureInstance()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.name != "M5")
            return null;

        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<M5HotbarController>();
        if (instance != null)
            return instance;

        GameObject host = GameObject.Find("AcquireableItems");
        if (host == null)
        {
            host = new GameObject("M5HotbarSystem");
        }

        instance = host.AddComponent<M5HotbarController>();
        return instance;
    }

    private void Awake()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.name != "M5")
        {
            enabled = false;
            return;
        }

        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        EnsureTemplateContainer();
        LoadDefaultIconsIfNeeded();
        BuildHotbarUI();
        BuildTrajectoryVisuals();
        RefreshHotbarUI();
    }

    private void Start()
    {
        FindPlayerAndHand();
        FindGateGeoIfNeeded();
        ConfigureGateInteractionCompatibility();
        ScanAllSceneAcquireableItems();
        RefreshHotbarUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (trajectoryMaterial != null)
            Destroy(trajectoryMaterial);
    }

    private void EnsureTemplateContainer()
    {
        if (templateContainer != null)
            return;

        GameObject containerObj = new GameObject("M5_ItemTemplates");
        containerObj.transform.SetParent(transform, false);
        containerObj.SetActive(false);
        templateContainer = containerObj.transform;
    }

    private void LoadDefaultIconsIfNeeded()
    {
#if UNITY_EDITOR
        if (rockIcon == null)
            rockIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Stone_CutOut.png");
        if (keyIcon == null)
            keyIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Key_CutOut.png");
#endif
    }

    private void ScanAllSceneAcquireableItems()
    {
        AcquireableItems[] items = FindObjectsByType<AcquireableItems>(FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                RegisterItemConfiguration(items[i]);
        }
    }

    public void RegisterItemConfiguration(AcquireableItems item)
    {
        if (item == null)
            return;

        if (rockIcon == null && item.rockIcon != null)
            rockIcon = item.rockIcon;

        if (keyIcon == null && item.keyIcon != null)
            keyIcon = item.keyIcon;

        if (gateGeoObject == null && item.targetGate != null)
        {
            gateGeoObject = item.targetGate;
            gateController = gateGeoObject.GetComponentInChildren<GateController>();
        }

        preferredHandBone = item.HandBone;
        itemPickupRange = Mathf.Max(2.2f, item.PickupRange);

        string itemName = item.GetResolvedItemName();
        if (string.Equals(itemName, "Rock", StringComparison.OrdinalIgnoreCase))
        {
            rockHandPos = item.RockHandPosition;
            rockHandEuler = item.RockHandEulerAngles;
            rockHandScale = item.RockHandScale;
        }
        else if (string.Equals(itemName, "Key", StringComparison.OrdinalIgnoreCase))
        {
            keyHandPos = item.KeyHandPosition;
            keyHandEuler = item.KeyHandEulerAngles;
            keyHandScale = item.KeyHandScale;
        }

        if (!string.IsNullOrEmpty(itemName) && !worldItemTemplates.ContainsKey(itemName))
        {
            CacheItemTemplate(itemName, item.gameObject);
        }
    }

    private void CacheItemTemplate(string itemName, GameObject sourceObject)
    {
        if (string.IsNullOrEmpty(itemName) || sourceObject == null)
            return;

        EnsureTemplateContainer();

        if (worldItemTemplates.ContainsKey(itemName) && worldItemTemplates[itemName] != null)
            return;

        GameObject clone = Instantiate(sourceObject, templateContainer);
        clone.name = itemName + "_Template";
        clone.transform.localRotation = sourceObject.transform.rotation;
        clone.transform.localScale = sourceObject.transform.lossyScale;

        // Clean up runtime physics/throw scripts on the template
        ThrownRockImpact[] impacts = clone.GetComponentsInChildren<ThrownRockImpact>(true);
        for (int i = 0; i < impacts.Length; i++)
        {
            if (impacts[i] != null)
                Destroy(impacts[i]);
        }

        Rigidbody[] rbs = clone.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] != null)
                Destroy(rbs[i]);
        }

        KeyPickup[] kps = clone.GetComponentsInChildren<KeyPickup>(true);
        for (int i = 0; i < kps.Length; i++)
        {
            if (kps[i] != null)
                Destroy(kps[i]);
        }

        AcquireableItems acq = clone.GetComponent<AcquireableItems>();
        if (acq == null)
            acq = clone.AddComponent<AcquireableItems>();

        acq.ItemName = itemName;
        acq.rockIcon = rockIcon;
        acq.keyIcon = keyIcon;
        acq.targetGate = gateGeoObject;

        clone.SetActive(false);
        worldItemTemplates[itemName] = clone;
    }

    private void FindPlayerAndHand()
    {
        if (mainCam == null)
            mainCam = Camera.main;

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                ThirdPersonController tpc = FindFirstObjectByType<ThirdPersonController>();
                if (tpc != null)
                    playerTransform = tpc.transform;
            }
        }

        if (playerTransform != null)
        {
            if (playerCharController == null)
                playerCharController = playerTransform.GetComponent<CharacterController>();

            if (leftHandBone == null)
                leftHandBone = FindLeftHandBone(playerTransform);
        }
    }

    private Transform FindLeftHandBone(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;

        Animator animator = playerRoot.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(preferredHandBone);
            if (bone != null)
                return bone;

            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (leftHand != null)
                return leftHand;
        }

        string[] handBoneNames =
        {
            "Left_Hand", "LeftHand", "hand_l", "Hand_L", "L_Hand", "mixamorig:LeftHand"
        };

        for (int i = 0; i < handBoneNames.Length; i++)
        {
            Transform found = FindChildRecursive(playerRoot, handBoneNames[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void FindGateGeoIfNeeded()
    {
        if (gateGeoObject == null)
        {
            gateGeoObject = GameObject.Find("gate_GEO");
            if (gateGeoObject == null)
            {
                GateController gc = FindFirstObjectByType<GateController>();
                if (gc != null)
                    gateGeoObject = gc.gameObject;
            }
        }

        if (gateGeoObject != null && gateController == null)
        {
            gateController = gateGeoObject.GetComponentInChildren<GateController>();
            if (gateController == null)
                gateController = gateGeoObject.GetComponentInParent<GateController>();
        }
    }

    private void ConfigureGateInteractionCompatibility()
    {
        FindGateGeoIfNeeded();
        if (gateGeoObject == null)
            return;

        // Ensure E key on gate_GEO doesn't bypass Left Mouse Button Key usage
        QuestNPCInteraction gateNpc = gateGeoObject.GetComponent<QuestNPCInteraction>();
        if (gateNpc != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RemoveItem("Key");
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "M5")
            return;

        UpdateHotbarVisibility();

        if (MapVideoController.IsVideoPlaying)
        {
            CancelRockCharge();
            UpdatePromptLabel(string.Empty);
            return;
        }

        if (playerTransform == null || mainCam == null || leftHandBone == null)
            FindPlayerAndHand();

        if (gateGeoObject == null)
            FindGateGeoIfNeeded();

        UpdateStatusToast();

        // Pause / Dialogue / Cutscene / Transition check
        if (Time.timeScale <= 0.0001f || ShouldHideHotbarUI())
        {
            CancelRockCharge();
            UpdatePromptLabel(string.Empty);
            return;
        }

        // 1. Hotbar Slot Equip / Unequip Keys (1, 2, 3)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            ToggleEquipSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            ToggleEquipSlot(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            ToggleEquipSlot(2);
        }

        // 2. Drop Currently Equipped Item (G)
        if (Input.GetKeyDown(KeyCode.G))
        {
            DropEquippedItem();
        }

        // 3. Detect Hovered World AcquireableItem & Handle Pickup (E)
        hoveredItem = FindBestCandidateItem();
        if (hoveredItem != null && Input.GetKeyDown(hoveredItem.PickupKey))
        {
            TryPickUpItem(hoveredItem);
        }

        // 4. Handle Equipped Item Actions (Rock vs Key)
        string equippedItemName = GetEquippedItemName();
        if (string.Equals(equippedItemName, "Rock", StringComparison.OrdinalIgnoreCase))
        {
            HandleRockEquippedMechanics();
        }
        else
        {
            // Ensure throwing mechanics and trajectory are disabled when Rock is not equipped
            if (isChargingRock)
                CancelRockCharge();

            if (string.Equals(equippedItemName, "Key", StringComparison.OrdinalIgnoreCase))
            {
                HandleKeyEquippedMechanics();
            }
        }

        // 5. Update Context Prompt UI
        UpdateInteractionPromptUI(equippedItemName);
    }

    // =========================================================================
    // HOTBAR & EQUIP LOGIC
    // =========================================================================

    public string GetEquippedItemName()
    {
        if (equippedSlotIndex < 0 || equippedSlotIndex >= SlotCount)
            return string.Empty;

        return slotItems[equippedSlotIndex] ?? string.Empty;
    }

    public int FindFirstAvailableSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (string.IsNullOrEmpty(slotItems[i]))
                return i;
        }
        return -1;
    }

    public bool IsInventoryFull()
    {
        return FindFirstAvailableSlot() < 0;
    }

    public void ToggleEquipSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return;

        // Empty slots cannot be equipped
        if (string.IsNullOrEmpty(slotItems[slotIndex]))
        {
            return;
        }

        // Cancel any ongoing Rock charge before changing equipped state
        CancelRockCharge();

        // Pressing the same number key again unequips the currently equipped item
        if (equippedSlotIndex == slotIndex)
        {
            equippedSlotIndex = -1;
        }
        else
        {
            // Equipping another slot automatically unequips the previously equipped item
            equippedSlotIndex = slotIndex;
        }

        UpdateLeftHandVisual();
        RefreshHotbarUI();
    }

    private static bool IsItemAllowedByQuestProgression(string itemName)
    {
        if (!string.Equals(itemName, "Key", StringComparison.OrdinalIgnoreCase))
            return true;

        // In M5 (main_quest_06), the Key may only be acquired after completing:
        // 0: talk_to_Joselina -> 1: scout_the_vilage -> 2: look_for_key (reaching index >= 3: get_key)
        if (QuestManager.Instance == null || QuestManager.Instance.ActiveQuest == null)
            return false;

        if (QuestManager.Instance.ActiveQuest.questID != "main_quest_06")
            return true;

        return QuestManager.Instance.CurrentObjectiveIndex >= 3;
    }

    public bool TryPickUpItem(AcquireableItems item)
    {
        if (item == null || !item.CanBePickedUpNow)
            return false;

        string itemName = item.GetResolvedItemName();
        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning("[M5Hotbar] AcquireableItems has an empty ItemName on " + item.name, item);
            return false;
        }

        if (!IsItemAllowedByQuestProgression(itemName))
            return false;

        // Check first available slot (no stacking allowed)
        int targetSlot = FindFirstAvailableSlot();
        if (targetSlot < 0)
        {
            ShowStatusToast("Inventory Full (3/3)");
            return false;
        }

        // Cache template and icons before destroying the world object
        RegisterItemConfiguration(item);
        CacheItemTemplate(itemName, item.gameObject);

        // Assign item to its own individual hotbar slot
        slotItems[targetSlot] = itemName;

        // Quest progression hook if picking up the Key in M5
        if (string.Equals(itemName, "Key", StringComparison.OrdinalIgnoreCase))
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.CompleteCurrentObjective("get_key");
            }
        }

        // Remove/destroy original item GameObject from the game world after successful pickup
        if (hoveredItem == item)
            hoveredItem = null;

        Destroy(item.gameObject);

        RefreshHotbarUI();
        UpdateLeftHandVisual();
        return true;
    }

    public void DropEquippedItem()
    {
        if (equippedSlotIndex < 0 || equippedSlotIndex >= SlotCount)
            return;

        string itemName = slotItems[equippedSlotIndex];
        if (string.IsNullOrEmpty(itemName))
            return;

        CancelRockCharge();

        // Clear slot and unequip immediately
        slotItems[equippedSlotIndex] = null;
        equippedSlotIndex = -1;

        UpdateLeftHandVisual();
        RefreshHotbarUI();

        // Spawn dropped item in the game world without triggering guard distraction
        SpawnDroppedWorldItem(itemName);
    }

    private void UpdateLeftHandVisual()
    {
        if (leftHandBone == null)
            FindPlayerAndHand();

        // Hide all hand visuals first
        foreach (KeyValuePair<string, GameObject> kvp in handVisualObjects)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }

        string equippedItem = GetEquippedItemName();
        if (string.IsNullOrEmpty(equippedItem) || leftHandBone == null)
            return;

        GameObject handVisual = GetOrCreateHandVisual(equippedItem);
        if (handVisual != null)
        {
            ApplyHandTransform(handVisual.transform, equippedItem);
            handVisual.SetActive(true);
        }
    }

    private GameObject GetOrCreateHandVisual(string itemName)
    {
        if (handVisualObjects.TryGetValue(itemName, out GameObject existing) && existing != null)
            return existing;

        if (!worldItemTemplates.TryGetValue(itemName, out GameObject template) || template == null)
            return null;

        if (leftHandBone == null)
            FindPlayerAndHand();

        if (leftHandBone == null)
            return null;

        GameObject visual = Instantiate(template, leftHandBone, false);
        visual.name = "LeftHand_" + itemName;

        // Strip interactive/physics components so the held visual never interferes with physics or pickup
        MonoBehaviour[] scripts = visual.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scripts.Length; i++)
        {
            if (scripts[i] != null)
                Destroy(scripts[i]);
        }

        Rigidbody[] rbs = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] != null)
                Destroy(rbs[i]);
        }

        Collider[] cols = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                Destroy(cols[i]);
        }

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        ApplyHandTransform(visual.transform, itemName);
        visual.SetActive(false);

        handVisualObjects[itemName] = visual;
        return visual;
    }

    private void ApplyHandTransform(Transform visualTransform, string itemName)
    {
        if (visualTransform == null)
            return;

        if (string.Equals(itemName, "Key", StringComparison.OrdinalIgnoreCase))
        {
            visualTransform.localPosition = keyHandPos;
            visualTransform.localRotation = Quaternion.Euler(keyHandEuler);
            visualTransform.localScale = keyHandScale;
        }
        else if (string.Equals(itemName, "Rock", StringComparison.OrdinalIgnoreCase))
        {
            Quaternion rot = Quaternion.Euler(rockHandEuler);
            Vector3 scale = rockHandScale;
            Vector3 localPos = rockHandPos;

            MeshFilter mf = visualTransform.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 meshCenter = mf.sharedMesh.bounds.center;
                if (mf.transform == visualTransform)
                {
                    localPos = rockHandPos - (rot * Vector3.Scale(meshCenter, scale));
                }
            }

            visualTransform.localPosition = localPos;
            visualTransform.localRotation = rot;
            visualTransform.localScale = scale;
        }
        else
        {
            visualTransform.localPosition = rockHandPos;
            visualTransform.localRotation = Quaternion.Euler(rockHandEuler);
            visualTransform.localScale = rockHandScale;
        }
    }

    // =========================================================================
    // WORLD ITEM PICKUP CANDIDATE DETECTION
    // =========================================================================

    private AcquireableItems FindBestCandidateItem()
    {
        if (playerTransform == null)
            return null;

        IReadOnlyList<AcquireableItems> items = AcquireableItems.WorldItems;
        if (items == null || items.Count == 0)
            return null;

        Vector3 playerPos = playerTransform.position;
        Vector3 playerEye = playerPos + Vector3.up * 1.35f;

        // 1. Check direct camera raycast/spherecast first
        if (mainCam != null)
        {
            Ray camRay = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float maxRayDist = Vector3.Distance(mainCam.transform.position, playerPos) + itemPickupRange + 2.0f;
            RaycastHit[] hits = Physics.SphereCastAll(camRay, 0.28f, maxRayDist, ~0, QueryTriggerInteraction.Collide);

            AcquireableItems bestRayItem = null;
            float bestRayDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null)
                    continue;

                AcquireableItems item = col.GetComponentInParent<AcquireableItems>();
                if (item == null || !item.isActiveAndEnabled || !item.CanBePickedUpNow)
                    continue;

                if (!IsItemAllowedByQuestProgression(item.GetResolvedItemName()))
                    continue;

                Vector3 itemCenter = item.GetWorldInteractionCenter();
                float flatDist = Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.z),
                    new Vector2(itemCenter.x, itemCenter.z));
                float dist3D = Vector3.Distance(playerEye, itemCenter);

                if (flatDist <= item.PickupRange || dist3D <= item.PickupRange + 0.4f)
                {
                    if (hits[i].distance < bestRayDist)
                    {
                        bestRayDist = hits[i].distance;
                        bestRayItem = item;
                    }
                }
            }

            if (bestRayItem != null)
                return bestRayItem;
        }

        // 2. Fallback: Check closest item in range within camera/player view cone
        AcquireableItems bestItem = null;
        float bestScore = float.MaxValue;

        Vector3 lookForward = mainCam != null ? mainCam.transform.forward : playerTransform.forward;
        Vector3 flatLookForward = new Vector3(lookForward.x, 0f, lookForward.z).normalized;
        if (flatLookForward.sqrMagnitude < 0.001f)
            flatLookForward = playerTransform.forward;

        for (int i = 0; i < items.Count; i++)
        {
            AcquireableItems item = items[i];
            if (item == null || !item.isActiveAndEnabled || !item.CanBePickedUpNow)
                continue;

            if (!IsItemAllowedByQuestProgression(item.GetResolvedItemName()))
                continue;

            Vector3 center = item.GetWorldInteractionCenter();
            float flatDist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(center.x, center.z));
            float heightDiff = Mathf.Abs(center.y - (playerPos.y + 0.8f));

            if (flatDist > item.PickupRange || heightDiff > 2.6f)
                continue;

            Vector3 toItemFlat = new Vector3(center.x - playerPos.x, 0f, center.z - playerPos.z);
            float flatAngle = toItemFlat.sqrMagnitude > 0.001f
                ? Vector3.Angle(flatLookForward, toItemFlat.normalized)
                : 0f;

            // Allow pickup if looking generally toward the item or standing right on top of it
            if (flatAngle <= 68f || flatDist <= 1.35f)
            {
                float score = flatDist + (flatAngle * 0.02f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestItem = item;
                }
            }
        }

        return bestItem;
    }

    // =========================================================================
    // ROCK CHARGING, TRAJECTORY PREDICTION & THROWING MECHANICS
    // =========================================================================

    private void HandleRockEquippedMechanics()
    {
        if (IsPointerOverInteractiveUI())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            isChargingRock = true;
            currentChargeTime = 0f;
        }

        if (isChargingRock && Input.GetMouseButton(0))
        {
            currentChargeTime = Mathf.Min(currentChargeTime + Time.deltaTime, maxChargeTime);
            float chargeNormalized = GetChargeNormalized();
            float launchSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, chargeNormalized);

            Vector3 origin = GetRockThrowOrigin();
            Vector3 velocity = GetRockThrowDirection() * launchSpeed;

            UpdateTrajectoryIndicator(origin, velocity);
        }
        else if (isChargingRock && Input.GetMouseButtonUp(0))
        {
            float chargeNormalized = GetChargeNormalized();
            float launchSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, chargeNormalized);

            Vector3 origin = GetRockThrowOrigin();
            Vector3 velocity = GetRockThrowDirection() * launchSpeed;

            CancelRockCharge();
            ThrowEquippedRock(origin, velocity);
        }
        else if (!Input.GetMouseButton(0) && isChargingRock)
        {
            CancelRockCharge();
        }
    }

    private float GetChargeNormalized()
    {
        if (maxChargeTime <= 0.01f)
            return 1f;
        return Mathf.Clamp01(currentChargeTime / maxChargeTime);
    }

    private Vector3 GetRockThrowDirection()
    {
        Vector3 forward = mainCam != null ? mainCam.transform.forward : (playerTransform != null ? playerTransform.forward : Vector3.forward);
        Vector3 biased = (forward + Vector3.up * throwUpwardBias).normalized;
        return biased.sqrMagnitude > 0.001f ? biased : Vector3.forward;
    }

    private Vector3 GetRockThrowOrigin()
    {
        if (playerTransform == null)
            return transform.position + Vector3.up * 1.3f;

        Vector3 aimDir = GetRockThrowDirection();
        Vector3 flatForward = new Vector3(aimDir.x, 0f, aimDir.z);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = playerTransform.forward;
        else
            flatForward.Normalize();

        return playerTransform.position + Vector3.up * 1.35f + flatForward * 0.65f;
    }

    private void UpdateTrajectoryIndicator(Vector3 startPos, Vector3 initialVelocity)
    {
        if (trajectoryLine == null || landingRingLine == null)
            BuildTrajectoryVisuals();

        const int maxSteps = 45;
        const float timeStep = 0.045f;
        Vector3 gravity = Physics.gravity;

        trajectoryLine.enabled = true;
        trajectoryLine.positionCount = maxSteps;

        Vector3 prevPoint = startPos;
        trajectoryLine.SetPosition(0, startPos);

        int actualCount = maxSteps;
        Vector3 landingPoint = startPos;
        Vector3 landingNormal = Vector3.up;
        bool hitSurface = false;

        for (int i = 1; i < maxSteps; i++)
        {
            float t = i * timeStep;
            Vector3 nextPoint = startPos + initialVelocity * t + 0.5f * gravity * (t * t);
            Vector3 seg = nextPoint - prevPoint;
            float segLen = seg.magnitude;

            if (segLen > 0.0001f)
            {
                RaycastHit[] hits = Physics.RaycastAll(prevPoint, seg / segLen, segLen, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit closestHit = default;
                float closestDist = float.MaxValue;
                bool foundHit = false;

                for (int h = 0; h < hits.Length; h++)
                {
                    Collider c = hits[h].collider;
                    if (c == null)
                        continue;

                    if (playerTransform != null && c.transform.IsChildOf(playerTransform))
                        continue;

                    if (hits[h].distance < closestDist)
                    {
                        closestDist = hits[h].distance;
                        closestHit = hits[h];
                        foundHit = true;
                    }
                }

                if (foundHit)
                {
                    landingPoint = closestHit.point;
                    landingNormal = closestHit.normal;
                    trajectoryLine.SetPosition(i, landingPoint);
                    actualCount = i + 1;
                    hitSurface = true;
                    break;
                }
            }

            trajectoryLine.SetPosition(i, nextPoint);
            prevPoint = nextPoint;
            landingPoint = nextPoint;
        }

        trajectoryLine.positionCount = actualCount;
        UpdateLandingRing(landingPoint, landingNormal, hitSurface || actualCount > 2);
    }

    private void UpdateLandingRing(Vector3 point, Vector3 normal, bool visible)
    {
        if (landingRingLine == null)
            return;

        landingRingLine.enabled = visible;
        if (!visible)
            return;

        const int segments = 24;
        const float radius = 0.32f;
        landingRingLine.positionCount = segments;

        Vector3 up = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Quaternion ringRot = Quaternion.FromToRotation(Vector3.up, up);
        Vector3 center = point + up * 0.03f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            landingRingLine.SetPosition(i, center + ringRot * localOffset);
        }
    }

    private void CancelRockCharge()
    {
        isChargingRock = false;
        currentChargeTime = 0f;

        if (trajectoryLine != null)
            trajectoryLine.enabled = false;

        if (landingRingLine != null)
            landingRingLine.enabled = false;
    }

    private void ThrowEquippedRock(Vector3 origin, Vector3 velocity)
    {
        if (equippedSlotIndex < 0 || equippedSlotIndex >= SlotCount)
            return;

        // Remove Rock from hotbar slot and unequip
        slotItems[equippedSlotIndex] = null;
        equippedSlotIndex = -1;

        UpdateLeftHandVisual();
        RefreshHotbarUI();

        // Spawn physical thrown Rock in the world & Unity Hierarchy
        GameObject rockObj = CreateWorldItemObject("Rock", origin, Quaternion.identity);
        if (rockObj == null)
            return;

        rockObj.name = "Rock";

        AcquireableItems acq = rockObj.GetComponent<AcquireableItems>();
        if (acq != null)
        {
            acq.ItemName = "Rock";
            acq.SetSpawnPickupDelay(0.25f);
        }

        // Ignore collision with player CharacterController so throw starts cleanly
        Collider[] rockColliders = rockObj.GetComponentsInChildren<Collider>();
        for (int i = 0; i < rockColliders.Length; i++)
        {
            rockColliders[i].isTrigger = false;
            if (playerCharController != null)
            {
                Physics.IgnoreCollision(rockColliders[i], playerCharController, true);
            }
        }

        Rigidbody rb = rockObj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = rockObj.AddComponent<Rigidbody>();

        rb.mass = 1.0f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearVelocity = velocity;
        rb.angularVelocity = new Vector3(4f, 2f, 3f);

        // Attach ThrownRockImpact so ONLY thrown rocks distract nearby guards within 4.5 units
        ThrownRockImpact impact = rockObj.GetComponent<ThrownRockImpact>();
        if (impact == null)
            impact = rockObj.AddComponent<ThrownRockImpact>();

        impact.Initialize(playerTransform, guardDistractionRadius, guardInvestigateSeconds);
    }

    private void SpawnDroppedWorldItem(string itemName)
    {
        Vector3 dropPos = GetDropWorldPosition(out Quaternion dropRot);
        GameObject droppedObj = CreateWorldItemObject(itemName, dropPos, dropRot);
        if (droppedObj == null)
            return;

        droppedObj.name = itemName;

        AcquireableItems acq = droppedObj.GetComponent<AcquireableItems>();
        if (acq != null)
        {
            acq.ItemName = itemName;
            acq.SetSpawnPickupDelay(0.15f);
            acq.RefreshCaches();
        }

        if (string.Equals(itemName, "Rock", StringComparison.OrdinalIgnoreCase))
        {
            Collider[] cols = droppedObj.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].isTrigger = false;
                if (playerCharController != null)
                    Physics.IgnoreCollision(cols[i], playerCharController, true);
            }

            Rigidbody rb = droppedObj.GetComponent<Rigidbody>();
            if (rb == null)
                rb = droppedObj.AddComponent<Rigidbody>();

            rb.mass = 1.0f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearDamping = 3.0f;
            rb.angularDamping = 4.0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = Vector3.down * 0.5f;
        }
        else
        {
            // Align visual mesh center of Key or non-Rock item cleanly onto the ground drop point
            AlignVisualCenterToWorldPoint(droppedObj, dropPos + Vector3.up * 0.06f);
        }
    }

    private Vector3 GetDropWorldPosition(out Quaternion dropRotation)
    {
        if (playerTransform == null)
        {
            dropRotation = Quaternion.identity;
            return transform.position;
        }

        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        dropRotation = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 checkOrigin = playerTransform.position + Vector3.up * 1.1f + forward * 1.0f;

        RaycastHit[] hits = Physics.RaycastAll(checkOrigin, Vector3.down, 4.0f, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        Vector3 groundPoint = playerTransform.position + forward * 1.0f + Vector3.up * 0.15f;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null)
                continue;
            if (c.transform.IsChildOf(playerTransform))
                continue;

            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                groundPoint = hits[i].point + Vector3.up * 0.12f;
            }
        }

        return groundPoint;
    }

    private void AlignVisualCenterToWorldPoint(GameObject obj, Vector3 targetWorldPoint)
    {
        if (obj == null)
            return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combined = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
                if (!hasBounds)
                {
                    combined = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderers[i].bounds);
                }
            }
        }

        if (hasBounds)
        {
            Vector3 offset = combined.center - obj.transform.position;
            obj.transform.position = targetWorldPoint - offset + Vector3.up * Mathf.Max(0.03f, combined.extents.y);
        }
    }

    private GameObject CreateWorldItemObject(string itemName, Vector3 position, Quaternion rotation)
    {
        GameObject spawned = null;

        if (worldItemTemplates.TryGetValue(itemName, out GameObject template) && template != null)
        {
            Quaternion spawnRot = string.Equals(itemName, "Key", StringComparison.OrdinalIgnoreCase)
                ? template.transform.localRotation
                : rotation;
            spawned = Instantiate(template, position, spawnRot);
            spawned.transform.SetParent(null, true);
            spawned.transform.localScale = template.transform.localScale;
            spawned.SetActive(true);
        }
        else
        {
            // Fallback primitive if template was not registered
            spawned = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spawned.transform.SetPositionAndRotation(position, rotation);
            spawned.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        }

        AcquireableItems acq = spawned.GetComponent<AcquireableItems>();
        if (acq == null)
            acq = spawned.AddComponent<AcquireableItems>();

        acq.ItemName = itemName;
        acq.rockIcon = rockIcon;
        acq.keyIcon = keyIcon;
        acq.targetGate = gateGeoObject;
        acq.EnsureColliderExists();
        acq.RefreshCaches();

        return spawned;
    }

    // =========================================================================
    // KEY MECHANICS (GATE_GEO INTERACTION)
    // =========================================================================

    private void HandleKeyEquippedMechanics()
    {
        if (IsPointerOverInteractiveUI())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsTargetingGateGeo(out GateController targetGateController))
            {
                if (targetGateController != null)
                {
                    targetGateController.Open();
                }

                // Update quest state if active
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.CompleteCurrentObjective("get_key");
                    QuestManager.Instance.CompleteCurrentObjective("open_prison_door");
                }

                if (gateGeoObject != null)
                {
                    QuestNPCInteraction gateNpc = gateGeoObject.GetComponent<QuestNPCInteraction>();
                    if (gateNpc != null)
                        gateNpc.enabled = false;
                }

                if (consumeKeyOnGateUnlock && equippedSlotIndex >= 0 && equippedSlotIndex < SlotCount)
                {
                    slotItems[equippedSlotIndex] = null;
                    equippedSlotIndex = -1;
                    UpdateLeftHandVisual();
                    RefreshHotbarUI();
                }

                ShowStatusToast("Unlocked gate_GEO!");
            }
        }
    }

    private bool IsTargetingGateGeo(out GateController resolvedGateController)
    {
        resolvedGateController = null;
        FindGateGeoIfNeeded();

        if (gateGeoObject == null || playerTransform == null)
            return false;

        resolvedGateController = gateController;

        Vector3 playerPos = playerTransform.position;
        Vector3 gateCenter = GetGateWorldCenter(gateGeoObject, out Collider gateCol);

        float distToGate = gateCol != null
            ? Vector3.Distance(playerPos + Vector3.up * 1.0f, gateCol.ClosestPoint(playerPos + Vector3.up * 1.0f))
            : Vector3.Distance(playerPos, gateCenter);

        if (distToGate > gateInteractRange)
            return false;

        // 1. Check camera raycast / spherecast toward gate_GEO
        if (mainCam != null)
        {
            Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float maxRayDist = Vector3.Distance(mainCam.transform.position, playerPos) + gateInteractRange + 3.0f;
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.35f, maxRayDist, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null)
                    continue;

                if (c.gameObject == gateGeoObject ||
                    c.transform.IsChildOf(gateGeoObject.transform) ||
                    c.name.IndexOf("gate_GEO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.GetComponentInParent<GateController>() != null)
                {
                    if (resolvedGateController == null)
                        resolvedGateController = c.GetComponentInParent<GateController>();
                    return true;
                }
            }
        }

        // 2. Check look direction angle toward gate_GEO bounds center
        Vector3 lookDir = mainCam != null ? mainCam.transform.forward : playerTransform.forward;
        Vector3 toGate = gateCenter - (mainCam != null ? mainCam.transform.position : (playerPos + Vector3.up * 1.4f));
        float angle = Vector3.Angle(lookDir, toGate);

        Vector3 flatLook = new Vector3(lookDir.x, 0f, lookDir.z).normalized;
        Vector3 flatToGate = new Vector3(gateCenter.x - playerPos.x, 0f, gateCenter.z - playerPos.z).normalized;
        float flatAngle = Vector3.Angle(flatLook, flatToGate);

        return angle <= 55f || flatAngle <= 55f;
    }

    private static Vector3 GetGateWorldCenter(GameObject gateObj, out Collider gateCollider)
    {
        gateCollider = gateObj.GetComponentInChildren<Collider>();
        if (gateCollider != null)
            return gateCollider.bounds.center;

        Renderer r = gateObj.GetComponentInChildren<Renderer>();
        if (r != null)
            return r.bounds.center;

        return gateObj.transform.position;
    }

    private static bool IsPointerOverInteractiveUI()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
            return false;

        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    // =========================================================================
    // TRAJECTORY VISUALS & HOTBAR UI CONSTRUCTION
    // =========================================================================

    private void BuildTrajectoryVisuals()
    {
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null)
            lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null)
            lineShader = Shader.Find("Unlit/Color");

        trajectoryMaterial = new Material(lineShader);

        GameObject arcObj = new GameObject("RockTrajectoryArc");
        arcObj.transform.SetParent(transform, false);
        trajectoryLine = arcObj.AddComponent<LineRenderer>();
        trajectoryLine.material = trajectoryMaterial;
        trajectoryLine.useWorldSpace = true;
        trajectoryLine.startWidth = 0.055f;
        trajectoryLine.endWidth = 0.025f;
        trajectoryLine.numCapVertices = 4;
        trajectoryLine.numCornerVertices = 4;
        trajectoryLine.startColor = new Color(1f, 0.92f, 0.25f, 0.92f);
        trajectoryLine.endColor = new Color(1f, 0.35f, 0.15f, 0.92f);
        trajectoryLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trajectoryLine.receiveShadows = false;
        trajectoryLine.enabled = false;

        GameObject ringObj = new GameObject("RockLandingIndicator");
        ringObj.transform.SetParent(transform, false);
        landingRingLine = ringObj.AddComponent<LineRenderer>();
        landingRingLine.material = trajectoryMaterial;
        landingRingLine.useWorldSpace = true;
        landingRingLine.loop = true;
        landingRingLine.startWidth = 0.045f;
        landingRingLine.endWidth = 0.045f;
        landingRingLine.startColor = new Color(1f, 0.3f, 0.1f, 0.95f);
        landingRingLine.endColor = new Color(1f, 0.3f, 0.1f, 0.95f);
        landingRingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        landingRingLine.receiveShadows = false;
        landingRingLine.enabled = false;
    }

    // Hides/shows only the hotbar visuals (and its raycasts); inventory data is untouched.
    public void SetHotbarUIHidden(bool hidden)
    {
        hiddenByExternalSystem = hidden;
        UpdateHotbarVisibility();
    }

    private bool ShouldHideHotbarUI()
    {
        if (hiddenByExternalSystem)
            return true;

        if (QuestNPCInteraction.IsInDialogue)
            return true;

        if (MapVideoController.IsVideoPlaying)
            return true;

        if (PlayerStatsHUD.IsTooltipOpen)
            return true;

        if (QuestSceneTransition.Instance != null && QuestSceneTransition.Instance.IsTransitioning)
            return true;

        if (GameOverScreen.Instance != null && GameOverScreen.Instance.IsGameOver)
            return true;

        return false;
    }

    private void UpdateHotbarVisibility()
    {
        if (hotbarCanvas != null)
        {
            bool shouldBeVisible = !ShouldHideHotbarUI();
            if (hotbarCanvas.enabled != shouldBeVisible)
                hotbarCanvas.enabled = shouldBeVisible;
        }
    }

    private void BuildHotbarUI()
    {
        if (hotbarCanvas != null)
            return;

        GameObject canvasObj = new GameObject("M5_HotbarCanvas");
        canvasObj.transform.SetParent(transform, false);

        hotbarCanvas = canvasObj.AddComponent<Canvas>();
        hotbarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hotbarCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Hotbar Container at bottom-center
        GameObject barContainer = new GameObject("HotbarSlotsContainer");
        barContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform barRect = barContainer.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 28f);
        barRect.sizeDelta = new Vector2(310f, 96f);

        const float slotSize = 78f;
        const float slotSpacing = 98f;

        for (int i = 0; i < SlotCount; i++)
        {
            float xOffset = (i - 1) * slotSpacing;

            // Root slot object
            GameObject slotObj = new GameObject("HotbarSlot_" + (i + 1));
            slotObj.transform.SetParent(barContainer.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2(xOffset, 0f);
            slotRect.sizeDelta = new Vector2(slotSize, slotSize);
            slotRects[i] = slotRect;

            // Equipped Selection Box (larger box with prominent BLACK outline)
            GameObject selectionBoxObj = new GameObject("SelectionOutlineBox");
            selectionBoxObj.transform.SetParent(slotObj.transform, false);
            RectTransform selRect = selectionBoxObj.AddComponent<RectTransform>();
            selRect.anchorMin = Vector2.zero;
            selRect.anchorMax = Vector2.one;
            selRect.offsetMin = new Vector2(-6f, -6f);
            selRect.offsetMax = new Vector2(6f, 6f);

            Image selBlackBorder = selectionBoxObj.AddComponent<Image>();
            selBlackBorder.color = Color.black;
            selBlackBorder.raycastTarget = false;

            Outline extraBlackOutline = selectionBoxObj.AddComponent<Outline>();
            extraBlackOutline.effectColor = Color.black;
            extraBlackOutline.effectDistance = new Vector2(2f, -2f);

            slotSelectionOutlines[i] = selectionBoxObj;

            // Slot inner background
            GameObject bgObj = new GameObject("SlotBackground");
            bgObj.transform.SetParent(slotObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.14f, 0.14f, 0.17f, 0.82f);
            bgImage.raycastTarget = false;
            slotBackgrounds[i] = bgImage;

            // Subtle default border
            Outline subtleBorder = bgObj.AddComponent<Outline>();
            subtleBorder.effectColor = new Color(0.45f, 0.45f, 0.5f, 0.65f);
            subtleBorder.effectDistance = new Vector2(1.5f, -1.5f);

            // Item Icon (RawImage for Stone_CutOut / Key_CutOut)
            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(8f, 8f);
            iconRect.offsetMax = new Vector2(-8f, -8f);

            RawImage rawIcon = iconObj.AddComponent<RawImage>();
            rawIcon.raycastTarget = false;
            rawIcon.enabled = false;
            slotIconImages[i] = rawIcon;

            // Slot Key Number Label (1, 2, 3)
            GameObject keyNumObj = new GameObject("KeybindNumber");
            keyNumObj.transform.SetParent(slotObj.transform, false);
            RectTransform numRect = keyNumObj.AddComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0f, 1f);
            numRect.anchorMax = new Vector2(0f, 1f);
            numRect.pivot = new Vector2(0f, 1f);
            numRect.anchoredPosition = new Vector2(5f, -3f);
            numRect.sizeDelta = new Vector2(26f, 24f);

            Text numText = keyNumObj.AddComponent<Text>();
            numText.font = defaultFont;
            numText.text = (i + 1).ToString();
            numText.fontSize = 15;
            numText.fontStyle = FontStyle.Bold;
            numText.color = new Color(0.92f, 0.92f, 0.92f, 0.95f);
            numText.alignment = TextAnchor.UpperLeft;
            numText.raycastTarget = false;

            Shadow numShadow = keyNumObj.AddComponent<Shadow>();
            numShadow.effectColor = Color.black;
            numShadow.effectDistance = new Vector2(1f, -1f);
        }

        // Equipped Item & Controls Hint Label above hotbar
        GameObject equippedLabelObj = new GameObject("EquippedItemHint");
        equippedLabelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform eqRect = equippedLabelObj.AddComponent<RectTransform>();
        eqRect.anchorMin = new Vector2(0.5f, 0f);
        eqRect.anchorMax = new Vector2(0.5f, 0f);
        eqRect.pivot = new Vector2(0.5f, 0f);
        eqRect.anchoredPosition = new Vector2(0f, 130f);
        eqRect.sizeDelta = new Vector2(700f, 34f);

        equippedLabelText = equippedLabelObj.AddComponent<Text>();
        equippedLabelText.font = defaultFont;
        equippedLabelText.fontSize = 17;
        equippedLabelText.fontStyle = FontStyle.Bold;
        equippedLabelText.alignment = TextAnchor.MiddleCenter;
        equippedLabelText.color = Color.white;
        equippedLabelText.raycastTarget = false;

        Outline eqOutline = equippedLabelObj.AddComponent<Outline>();
        eqOutline.effectColor = Color.black;
        eqOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // World Interaction Prompt Label
        GameObject promptObj = new GameObject("InteractionPromptLabel");
        promptObj.transform.SetParent(canvasObj.transform, false);
        RectTransform promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 168f);
        promptRect.sizeDelta = new Vector2(700f, 36f);

        promptLabelText = promptObj.AddComponent<Text>();
        promptLabelText.font = defaultFont;
        promptLabelText.fontSize = 19;
        promptLabelText.fontStyle = FontStyle.Bold;
        promptLabelText.alignment = TextAnchor.MiddleCenter;
        promptLabelText.color = new Color(1f, 0.95f, 0.65f, 1f);
        promptLabelText.raycastTarget = false;

        Outline promptOutline = promptObj.AddComponent<Outline>();
        promptOutline.effectColor = Color.black;
        promptOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Status Toast Label
        GameObject toastObj = new GameObject("StatusToastLabel");
        toastObj.transform.SetParent(canvasObj.transform, false);
        RectTransform toastRect = toastObj.AddComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 0f);
        toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0f, 206f);
        toastRect.sizeDelta = new Vector2(700f, 34f);

        statusToastText = toastObj.AddComponent<Text>();
        statusToastText.font = defaultFont;
        statusToastText.fontSize = 18;
        statusToastText.fontStyle = FontStyle.Bold;
        statusToastText.alignment = TextAnchor.MiddleCenter;
        statusToastText.color = new Color(1f, 0.55f, 0.45f, 1f);
        statusToastText.raycastTarget = false;
        statusToastText.text = string.Empty;

        Outline toastOutline = toastObj.AddComponent<Outline>();
        toastOutline.effectColor = Color.black;
        toastOutline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    public void RefreshHotbarUI()
    {
        if (hotbarCanvas == null)
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            string itemName = slotItems[i];
            bool hasItem = !string.IsNullOrEmpty(itemName);
            bool isSelected = (equippedSlotIndex == i) && hasItem;

            // Update selection box (black outline + larger box when selected, removed when unequipped)
            if (slotSelectionOutlines[i] != null)
            {
                slotSelectionOutlines[i].SetActive(isSelected);
            }

            if (slotRects[i] != null)
            {
                slotRects[i].localScale = isSelected ? new Vector3(1.16f, 1.16f, 1f) : Vector3.one;
            }

            if (slotBackgrounds[i] != null)
            {
                slotBackgrounds[i].color = isSelected
                    ? new Color(0.28f, 0.28f, 0.33f, 0.94f)
                    : new Color(0.14f, 0.14f, 0.17f, 0.80f);
            }

            // Update slot icon (Stone_CutOut or Key_CutOut)
            if (slotIconImages[i] != null)
            {
                Texture2D iconTex = GetIconForItem(itemName);
                slotIconImages[i].texture = iconTex;
                slotIconImages[i].enabled = hasItem && iconTex != null;
            }
        }

        UpdateEquippedHintText();
    }

    private Texture2D GetIconForItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
            return null;

        if (string.Equals(itemName, "Rock", StringComparison.OrdinalIgnoreCase))
            return rockIcon;

        if (string.Equals(itemName, "Key", StringComparison.OrdinalIgnoreCase))
            return keyIcon;

        return null;
    }

    private void UpdateEquippedHintText()
    {
        if (equippedLabelText == null)
            return;

        string equipped = GetEquippedItemName();
        if (string.IsNullOrEmpty(equipped))
        {
            equippedLabelText.text = string.Empty;
            return;
        }

        if (string.Equals(equipped, "Rock", StringComparison.OrdinalIgnoreCase))
        {
            if (isChargingRock)
            {
                int pct = Mathf.RoundToInt(GetChargeNormalized() * 100f);
                equippedLabelText.text = $"Charging Rock Throw: {pct}%  |  [Release LMB] Throw";
            }
            else
            {
                equippedLabelText.text = "Equipped: Rock  |  [Hold LMB] Charge & Throw  |  [G] Drop";
            }
        }
        else if (string.Equals(equipped, "Key", StringComparison.OrdinalIgnoreCase))
        {
            equippedLabelText.text = "Equipped: Key  |  [LMB] Unlock Gate  |  [G] Drop";
        }
        else
        {
            equippedLabelText.text = $"Equipped: {equipped}  |  [G] Drop";
        }
    }

    private void UpdateInteractionPromptUI(string equippedItemName)
    {
        UpdateEquippedHintText();

        if (hoveredItem != null)
        {
            string itemName = hoveredItem.GetResolvedItemName();
            if (IsInventoryFull())
            {
                UpdatePromptLabel($"Inventory Full (3/3) - Cannot pick up {itemName}");
            }
            else
            {
                UpdatePromptLabel($"Press [{hoveredItem.PickupKey}] to pick up {itemName}");
            }
            return;
        }

        if (string.Equals(equippedItemName, "Key", StringComparison.OrdinalIgnoreCase))
        {
            if (IsTargetingGateGeo(out _))
            {
                UpdatePromptLabel("Press [Left Mouse Button] to Use Key on Gate");
                return;
            }
        }

        UpdatePromptLabel(string.Empty);
    }

    private void UpdatePromptLabel(string message)
    {
        if (promptLabelText != null)
            promptLabelText.text = message ?? string.Empty;
    }

    private void ShowStatusToast(string message, float duration = 2.0f)
    {
        if (statusToastText != null)
        {
            statusToastText.text = message;
            statusToastTimer = duration;
        }
    }

    private void UpdateStatusToast()
    {
        if (statusToastTimer > 0f)
        {
            statusToastTimer -= Time.deltaTime;
            if (statusToastTimer <= 0f && statusToastText != null)
            {
                statusToastText.text = string.Empty;
            }
        }
    }
}

/// <summary>
/// Attached ONLY to thrown Rocks so that landing within 4.5 units of a guard triggers
/// the 2-second guard distraction/investigation behavior without affecting dropped Rocks.
/// </summary>
public class ThrownRockImpact : MonoBehaviour
{
    private Transform throwerTransform;
    private float distractionRadius = 4.5f;
    private float investigateDuration = 2.0f;
    private bool hasTriggeredDistraction;
    private float launchTime;
    private Rigidbody rb;

    public void Initialize(Transform thrower, float radius, float duration)
    {
        throwerTransform = thrower;
        distractionRadius = radius > 0f ? radius : 4.5f;
        investigateDuration = duration > 0f ? duration : 2.0f;
        hasTriggeredDistraction = false;
        launchTime = Time.time;
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (hasTriggeredDistraction)
            return;

        // Fallback landing check if rock settles on a surface without triggering OnCollisionEnter
        if (Time.time - launchTime > 0.25f && rb != null && rb.linearVelocity.sqrMagnitude < 0.25f)
        {
            TriggerLandingDistraction(transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null && collision.collider != null && throwerTransform != null)
        {
            if (collision.collider.transform.IsChildOf(throwerTransform))
                return;
        }

        // Increase damping after landing so the rock stays near its landing spot and remains easy to collect
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearDamping = 3.5f;
            rb.angularDamping = 4.5f;
        }

        if (!hasTriggeredDistraction)
        {
            Vector3 impactPoint = (collision != null && collision.contactCount > 0)
                ? collision.GetContact(0).point
                : transform.position;
            TriggerLandingDistraction(impactPoint);
        }
    }

    private void TriggerLandingDistraction(Vector3 landingPosition)
    {
        if (hasTriggeredDistraction)
            return;

        hasTriggeredDistraction = true;
        GuardPatrol.NotifyThrownRockLanded(landingPosition, distractionRadius, investigateDuration);
    }
}
