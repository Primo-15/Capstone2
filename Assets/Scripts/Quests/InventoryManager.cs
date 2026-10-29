
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // The single item currently carried by the player.
    private string currentItemID;

    public bool HasItemInInventory =>
        !string.IsNullOrWhiteSpace(currentItemID);

    public string CurrentItemID => currentItemID;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool HasItem(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID))
            return false;

        return currentItemID == itemID;
    }

    public bool AddItem(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID))
        {
            Debug.LogWarning("Cannot add an item with an empty ID.");
            return false;
        }

        if (HasItemInInventory)
        {
            Debug.Log(
                $"Inventory is occupied by {currentItemID}. " +
                $"Cannot add {itemID}."
            );

            return false;
        }

        currentItemID = itemID;

        Debug.Log($"Item added to inventory: {currentItemID}");

        return true;
    }

    public bool RemoveItem(string itemID)
    {
        if (!HasItem(itemID))
        {
            Debug.LogWarning(
                $"Cannot remove {itemID}. " +
                "The item is not in the inventory."
            );

            return false;
        }

        Debug.Log($"Item removed from inventory: {currentItemID}");

        currentItemID = null;

        return true;
    }

    public void ClearInventory()
    {
        if (HasItemInInventory)
        {
            Debug.Log($"Inventory cleared: {currentItemID}");
        }

        currentItemID = null;
    }
}
