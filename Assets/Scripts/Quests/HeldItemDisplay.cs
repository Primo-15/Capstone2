using UnityEngine;

// Put this on the Player. It shows a model in the player's hand
// while the item is in the inventory, and hides it as soon as the
// item is removed (for example when Pepe takes the knife).
public class HeldItemDisplay : MonoBehaviour
{
    [Tooltip("Must match the item ID in the inventory, e.g. Knife.")]
    [SerializeField] private string itemID = "Knife";

    [Tooltip("The knife model that is a child of the player's hand bone.")]
    [SerializeField] private GameObject heldObject;

    private void Update()
    {
        if (heldObject == null)
            return;

        InventoryManager inventory = InventoryManager.Instance;

        bool hasItem = inventory != null && inventory.HasItem(itemID);

        if (heldObject.activeSelf != hasItem)
            heldObject.SetActive(hasItem);
    }
}