using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsHUD : MonoBehaviour
{
    [SerializeField] private Image[] heartIcons;
    [SerializeField] private Image[] staminaIcons;
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.2f;

    private Color[] heartBase;
    private Color[] staminaBase;

    private void Awake()
    {
        heartBase = CaptureColors(heartIcons);
        staminaBase = CaptureColors(staminaIcons);
    }

    private void OnEnable()
    {
        PlayerStats.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerStats.OnChanged -= Refresh;
    }

    private Color[] CaptureColors(Image[] icons)
    {
        var colors = new Color[icons.Length];
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) colors[i] = icons[i].color;
        return colors;
    }

    private void Refresh()
    {
        Apply(heartIcons, heartBase, PlayerStats.Hearts);
        Apply(staminaIcons, staminaBase, PlayerStats.Stamina);
    }

    private void Apply(Image[] icons, Color[] baseColors, int value)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null) continue;

            Color c = baseColors[i];
            if (i >= value) c.a = emptyAlpha;   // spent icon fades out
            icons[i].color = c;
        }
    }
}