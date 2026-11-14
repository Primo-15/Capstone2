using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MudZone : MonoBehaviour
{
    [SerializeField, Range(0.05f, 1f)] private float speedMultiplier = 0.4f;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return; // guards and everything else are ignored

        PlayerSpeedModifier modifier =
            other.GetComponentInParent<PlayerSpeedModifier>();

        if (modifier != null)
            modifier.AddSlow(this, speedMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        PlayerSpeedModifier modifier =
            other.GetComponentInParent<PlayerSpeedModifier>();

        if (modifier != null)
            modifier.RemoveSlow(this);
    }
}