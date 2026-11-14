using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        // Persist M3 hearts so the player cannot exploit Main Menu -> Continue to refill hearts.
        // If all 3 hearts are lost in M3, force the saved checkpoint back to M2.
        if (SceneManager.GetActiveScene().name == "M3")
        {
            if (Hearts > 0)
            {
                SaveSystem.SaveM3Hearts(Hearts);
            }
            else
            {
                SaveSystem.SaveMapCheckpoint("M2");
            }
        }
    }

    public static void SetHearts(int hearts)
    {
        Hearts = Mathf.Clamp(hearts, 0, MaxHearts);
        Stamina = MaxStamina;
        OnChanged?.Invoke();
    }

    public static void RefillStamina()
    {
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
