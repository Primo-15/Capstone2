using System.Collections;
using UnityEngine;

public class PlayerSpawnLoader : MonoBehaviour
{
    [SerializeField] private float holdSeconds = 0.5f;

    private IEnumerator Start()
    {
        SaveData data = SaveSystem.PendingSpawn;

        if (data == null)
        {
            Debug.Log("PlayerSpawnLoader: no pending spawn (new game or opened M1 directly).");
            yield break;
        }

        if (!data.hasPosition)
        {
            Debug.LogWarning("PlayerSpawnLoader: save has NO position. It was made before the position code existed, or no 'Player' tag was found when saving.");
            SaveSystem.PendingSpawn = null;
            yield break;
        }

        SaveSystem.PendingSpawn = null;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("PlayerSpawnLoader: no object tagged 'Player' found.");
            yield break;
        }

        Debug.Log($"PlayerSpawnLoader: moving player to {data.playerPosition}");

        // Re-apply for a short time in case another script repositions the player on load
        float t = 0f;
        while (t < holdSeconds)
        {
            Apply(player, data);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Apply(player, data);
    }

    private void Apply(GameObject player, SaveData data)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.SetPositionAndRotation(
            data.playerPosition,
            Quaternion.Euler(0f, data.playerYRotation, 0f)
        );

        if (cc != null) cc.enabled = true;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.position = data.playerPosition;

        Physics.SyncTransforms();
    }
}