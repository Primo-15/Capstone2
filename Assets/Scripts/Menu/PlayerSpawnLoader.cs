
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
            Debug.Log(
                "PlayerSpawnLoader: no pending spawn."
            );
            yield break;
        }

        // Continue and map checkpoints always start
        // at the map's normal spawn point.
        if (!data.hasPosition)
        {
            Debug.Log(
                "PlayerSpawnLoader: using default map spawn."
            );

            SaveSystem.PendingSpawn = null;
            yield break;
        }

        SaveSystem.PendingSpawn = null;

        GameObject player =
            GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning(
                "PlayerSpawnLoader: Player not found."
            );
            yield break;
        }

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
        CharacterController cc =
            player.GetComponent<CharacterController>();

        if (cc != null)
            cc.enabled = false;

        player.transform.SetPositionAndRotation(
            data.playerPosition,
            Quaternion.Euler(
                0f,
                data.playerYRotation,
                0f
            )
        );

        if (cc != null)
            cc.enabled = true;

        Rigidbody rb =
            player.GetComponent<Rigidbody>();

        if (rb != null)
            rb.position = data.playerPosition;

        Physics.SyncTransforms();
    }
}