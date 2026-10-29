using UnityEngine;
using StarterAssets;

// Add this to the Player (same object as StarterAssetsInputs / ThirdPersonController).
// While any NPC dialogue is open it cancels move, jump and sprint input every frame.
// The ThirdPersonController keeps running, so gravity still works normally.
[DefaultExecutionOrder(-100)] // run before ThirdPersonController reads the input
public class DialogueMovementLock : MonoBehaviour
{
    private StarterAssetsInputs input;

    private void Awake()
    {
        input = GetComponent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (input == null || !QuestNPCInteraction.IsInDialogue)
            return;

        input.move = Vector2.zero;
        input.jump = false;
        input.sprint = false;
    }
}