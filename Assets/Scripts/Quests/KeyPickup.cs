using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

public class KeyPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Distance from the player within which the key can be picked up. Height is ignored.")]
    [SerializeField] private float pickupRange = 2.5f;

    [Tooltip("Key to press to pick up.")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Header("Inventory & Quest")]
    [Tooltip("Item ID to add to InventoryManager (e.g. 'Key').")]
    [SerializeField] private string itemID = "Key";

    [Tooltip("ID of the quest (e.g. 'main_quest_06').")]
    [SerializeField] private string questID = "main_quest_06";

    [Tooltip("Objective ID in QuestManager to complete upon pickup (e.g. 'get_key').")]
    [SerializeField] private string objectiveID = "get_key";

    [Tooltip("If true, pickup only works while the get_key objective is active. If false, can be picked up anytime.")]
    [SerializeField] private bool requireObjectiveActive = false;

    [Header("Carry In Hand Settings")]
    [SerializeField] private HumanBodyBones handBone = HumanBodyBones.RightHand;
    [SerializeField] private string fallbackHandBoneName = "Right_Hand";
    [SerializeField] private Vector3 carryPosition = new Vector3(-0.06f, 0.05f, 0.01f);
    [SerializeField] private Vector3 carryEulerAngles = new Vector3(-90f, 0f, 90f);
    [SerializeField] private Vector3 carryScale = new Vector3(0.0008f, 0.0008f, 0.0008f);
    [SerializeField] private bool preserveInitialWorldScale = true;

    [Header("Prompt UI")]
    [Tooltip("Optional custom UI GameObject to activate/deactivate when in pickup range.")]
    [SerializeField] private GameObject promptUI;

    [Tooltip("If true and promptUI is null, automatically displays an on-screen prompt.")]
    [SerializeField] private bool showOnScreenPrompt = true;

    [SerializeField] private string promptText = "Press [E] to pick up Key";

    [Header("Debug")]
    [Tooltip("Prints the player name, distance and dialogue lock once per second. Turn off when done.")]
    [SerializeField] private bool debugLog = false;

    private Transform player;
    private bool isPickedUp;
    private bool inRange;
    private Vector3 initialWorldScale;

    public bool IsPickedUp => isPickedUp;
    public HumanBodyBones HandBone => handBone;
    public Vector3 CarryPosition => carryPosition;
    public Vector3 CarryEulerAngles => carryEulerAngles;
    public Vector3 CarryScale => carryScale;
    public string QuestID => questID;
    public string ObjectiveID => objectiveID;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSetupInScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "M5")
            return;

        GameObject keyObj = GameObject.Find("key");
        if (keyObj == null)
        {
            var npcs = GameObject.Find("NPCs");
            if (npcs != null)
            {
                var t = npcs.transform.Find("key");
                if (t != null) keyObj = t.gameObject;
            }
        }

        if (keyObj != null)
        {
            // Remove QuestNPCInteraction if it was mistakenly attached
            var npcInteraction = keyObj.GetComponent<QuestNPCInteraction>();
            if (npcInteraction != null)
            {
                Destroy(npcInteraction);
            }

            // If AcquireableItems is managing this object in M5, yield to AcquireableItems
            if (keyObj.GetComponent<AcquireableItems>() != null)
            {
                var kp = keyObj.GetComponent<KeyPickup>();
                if (kp != null)
                    kp.enabled = false;
                return;
            }

            if (keyObj.GetComponent<KeyPickup>() == null)
            {
                keyObj.AddComponent<KeyPickup>();
                Debug.Log("[KeyPickup] Auto-attached KeyPickup component to key in M5.");
            }
        }
    }

    private void Awake()
    {
        initialWorldScale = transform.lossyScale;

        // If QuestNPCInteraction is present, remove/disable it to avoid dialogue conflicts
        var oldInteraction = GetComponent<QuestNPCInteraction>();
        if (oldInteraction != null)
        {
            oldInteraction.enabled = false;
            Destroy(oldInteraction);
        }

        // If AcquireableItems is attached on this GameObject in Scene M5, let AcquireableItems handle pickup and hotbar
        if (SceneManager.GetActiveScene().name == "M5" && GetComponent<AcquireableItems>() != null)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        FindPlayer();

        if (promptUI != null)
            promptUI.SetActive(false);
    }

    private void FindPlayer()
    {
        if (player != null) return;

        GameObject pObj = GameObject.FindWithTag("Player");
        if (pObj != null)
        {
            player = pObj.transform;
            return;
        }

        var tpc = FindFirstObjectByType<ThirdPersonController>();
        if (tpc != null)
        {
            player = tpc.transform;
            return;
        }

        var anim = FindFirstObjectByType<Animator>();
        if (anim != null && anim.gameObject.name.ToLower().Contains("player"))
        {
            player = anim.transform;
        }
    }

    private void Update()
    {
        if (isPickedUp)
        {
            // If the item was later removed from inventory (e.g. used to unlock a door), hide it
            if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem(itemID))
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // Measure distance on the ground plane only, so the key's height
        // (or an off-center pivot) doesn't push the player out of range.
        Vector3 playerPos = player.position;
        Vector3 keyPos = transform.position;
        playerPos.y = 0f;
        keyPos.y = 0f;

        float distance = Vector3.Distance(playerPos, keyPos);
        bool canPick = (distance <= pickupRange) && !QuestNPCInteraction.IsInDialogue;

        if (debugLog && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[KeyPickup] player={player.name} dist={distance:F2} dialogue={QuestNPCInteraction.IsInDialogue}");
        }

        if (canPick && requireObjectiveActive)
        {
            canPick = IsObjectiveActive();
        }

        inRange = canPick;

        if (promptUI != null && promptUI.activeSelf != inRange)
        {
            promptUI.SetActive(inRange);
        }

        if (inRange && Input.GetKeyDown(pickupKey))
        {
            PickUp();
        }
    }

    private bool IsObjectiveActive()
    {
        QuestManager qm = QuestManager.Instance;
        if (qm == null || qm.ActiveQuest == null) return false;
        if (qm.ActiveQuest.questID != questID) return false;
        if (qm.CurrentObjectiveIndex < 0 || qm.CurrentObjectiveIndex >= qm.ActiveQuest.objectives.Length) return false;

        return qm.ActiveQuest.objectives[qm.CurrentObjectiveIndex].objectiveID == objectiveID;
    }

    public void PickUp()
    {
        if (isPickedUp) return;

        isPickedUp = true;
        inRange = false;

        if (promptUI != null)
            promptUI.SetActive(false);

        // 1. Locate player's right hand bone
        Transform rightHand = FindRightHand(player);

        if (rightHand == null)
        {
            Debug.LogWarning("[KeyPickup] Right hand bone not found on Player! Attaching to player root instead.");
            rightHand = player;
        }

        // 2. Transfer object to player's right hand
        transform.SetParent(rightHand, false);
        transform.localPosition = carryPosition;
        transform.localRotation = Quaternion.Euler(carryEulerAngles);

        if (preserveInitialWorldScale && rightHand != null)
        {
            Vector3 handScale = rightHand.lossyScale;
            float sx = handScale.x != 0 ? initialWorldScale.x / handScale.x : carryScale.x;
            float sy = handScale.y != 0 ? initialWorldScale.y / handScale.y : carryScale.y;
            float sz = handScale.z != 0 ? initialWorldScale.z / handScale.z : carryScale.z;
            transform.localScale = new Vector3(sx, sy, sz);
        }
        else
        {
            transform.localScale = carryScale;
        }

        // 3. Disable all colliders so carried key won't collide with player or environment
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        // 4. Add to Inventory
        if (!string.IsNullOrWhiteSpace(itemID) && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(itemID);
        }

        // 5. Complete quest objective if applicable
        if (!string.IsNullOrWhiteSpace(objectiveID) && QuestManager.Instance != null)
        {
            QuestManager.Instance.CompleteCurrentObjective(objectiveID);
        }

        Debug.Log($"[KeyPickup] Key successfully transferred to player's right hand ({rightHand.name}).");
    }

    private Transform FindRightHand(Transform p)
    {
        if (p == null) return null;

        // Try Humanoid bone
        Animator anim = p.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            Transform bone = anim.GetBoneTransform(handBone);
            if (bone != null) return bone;
        }

        // Search recursively for known right hand bone names
        Transform[] allChildren = p.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            if (t.name.Equals(fallbackHandBoneName, System.StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("Right_Hand", System.StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("RightHand", System.StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("hand.R", System.StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("mixamorig:RightHand", System.StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private void OnGUI()
    {
        if (isPickedUp || !inRange || !showOnScreenPrompt || promptUI != null)
            return;

        // Draw styled prompt at bottom center of screen
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        float width = 280f;
        float height = 44f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - 120f;

        // Dark background pill
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(new Rect(x - 2, y - 2, width + 4, height + 4), GUIContent.none);

        GUI.color = Color.white;
        GUI.Box(new Rect(x, y, width, height), promptText, style);
    }
}