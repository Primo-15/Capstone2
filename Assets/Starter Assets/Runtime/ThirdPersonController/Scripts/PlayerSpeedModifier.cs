using System.Collections.Generic;
using UnityEngine;

public class PlayerSpeedModifier : MonoBehaviour
{
    private readonly Dictionary<Object, float> slows = new Dictionary<Object, float>();

    // 1 = normal speed. ThirdPersonController multiplies its speed by this.
    public float Multiplier { get; private set; } = 1f;

    public void AddSlow(Object source, float multiplier)
    {
        slows[source] = Mathf.Clamp(multiplier, 0.05f, 1f);
        Recalculate();
    }

    public void RemoveSlow(Object source)
    {
        if (slows.Remove(source))
            Recalculate();
    }

    private void Recalculate()
    {
        // The strongest slow wins when the player overlaps several mud piles.
        float result = 1f;
        foreach (var slow in slows)
            result = Mathf.Min(result, slow.Value);

        Multiplier = result;
    }
}