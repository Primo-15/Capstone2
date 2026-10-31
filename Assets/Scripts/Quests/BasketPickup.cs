using UnityEngine;

public class BasketPickup : MonoBehaviour
{
    [Header("Quest Requirement")]
    [SerializeField] private string questID = "main_quest_02";
    [Tooltip("Objective ID of 'Hanapin ang basket'. Pickup only works while this is the current objective.")]
    [SerializeField] private string requiredObjectiveID;

    [Header("Inventory")]
    [Tooltip("Same ID that Rosa's 'Required Item ID' uses. Leave empty if the basket isn't an inventory item.")]
    [SerializeField] private string itemID;

    [Header("Pickup")]
    [SerializeField] private Transform basketVisual;     // the Basket object (the model)
    [SerializeField] private float pickupRange = 2.5f;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private GameObject promptUI;        // optional "Press E" text

    [Header("Carry Position (relative to the hand)")]
    [SerializeField] private HumanBodyBones handBone = HumanBodyBones.RightHand;
    [SerializeField] private Vector3 carryPosition = Vector3.zero;
    [SerializeField] private Vector3 carryEulerAngles = new Vector3(-90f, 0f, 0f);
    [SerializeField] private Vector3 carryScale = new Vector3(13f, 13f, 13f);

    private Transform player;
    private bool pickedUp;

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;

        if (basketVisual == null)
            basketVisual = transform.parent != null ? transform.parent : transform;

        if (promptUI != null) promptUI.SetActive(false);
    }

    private void Update()
    {
        if (pickedUp || player == null)
            return;

        bool canPick = IsObjectiveActive() &&
                       !QuestNPCInteraction.IsInDialogue &&
                       Vector3.Distance(player.position, basketVisual.position) <= pickupRange;

        if (promptUI != null) promptUI.SetActive(canPick);

        if (canPick && Input.GetKeyDown(pickupKey))
            PickUp();
    }

    private bool IsObjectiveActive()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || manager.ActiveQuest == null) return false;
        if (manager.ActiveQuest.questID != questID) return false;
        if (manager.CurrentObjectiveIndex >= manager.ActiveQuest.objectives.Length) return false;

        return manager.ActiveQuest.objectives[manager.CurrentObjectiveIndex].objectiveID
               == requiredObjectiveID;
    }

    private void PickUp()
    {
        pickedUp = true;
        if (promptUI != null) promptUI.SetActive(false);

        // Find the hand bone on the player's humanoid rig
        Transform hand = null;
        Animator anim = player.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
            hand = anim.GetBoneTransform(handBone);

        if (hand == null)
        {
            Debug.LogWarning("BasketPickup: hand bone not found, attaching to the player instead.");
            hand = player;
        }

        // Attach to the hand
        basketVisual.SetParent(hand, false);
        basketVisual.localPosition = carryPosition;
        basketVisual.localRotation = Quaternion.Euler(carryEulerAngles);
        basketVisual.localScale = carryScale;

        // The basket must not block or trigger anything while carried
        foreach (Collider c in basketVisual.GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Give the item, then complete the objective
        if (!string.IsNullOrWhiteSpace(itemID) && InventoryManager.Instance != null)
            InventoryManager.Instance.AddItem(itemID);

        QuestManager.Instance.CompleteCurrentObjective(requiredObjectiveID);

        Debug.Log("Basket picked up.");
        enabled = false;
    }
}