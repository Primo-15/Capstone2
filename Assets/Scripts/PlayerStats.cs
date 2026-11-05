using System;
using UnityEngine;

// Static, so hearts and stamina survive scene reloads (Try Again).
public static class PlayerStats
{
    public const int MaxHearts = 3;
    public const int MaxStamina = 3;

    public static int Hearts { get; private set; } = MaxHearts;
    public static int Stamina { get; private set; } = MaxStamina;

    public static bool HasHearts => Hearts > 0;
    public static bool HasStamina => Stamina > 0;

    public static event Action OnChanged;

    // Makes sure a new Play session in the editor always starts fresh.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        Hearts = MaxHearts;
        Stamina = MaxStamina;
        OnChanged = null;
    }

    public static bool UseStamina()
    {
        if (Stamina <= 0)
            return false;

        Stamina--;
        OnChanged?.Invoke();
        return true;
    }

    // Losing a heart also refills stamina.
    public static void LoseHeart()
    {
        if (Hearts > 0)
            Hearts--;

        Stamina = MaxStamina;
        OnChanged?.Invoke();
    }

    public static void ResetAll()
    {
        Hearts = MaxHearts;
        Stamina = MaxStamina;
        OnChanged?.Invoke();
    }
}