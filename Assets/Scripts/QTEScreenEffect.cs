using UnityEngine;
using UnityEngine.Rendering;

public class QTEScreenEffect : MonoBehaviour
{
    [SerializeField] private Volume effectVolume;
    [SerializeField] private float fadeInTime = 0.15f;
    [SerializeField] private float fadeOutTime = 0.3f;

    [Header("Heartbeat pulse")]
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.15f;

    private float strength;

    private void Start()
    {
        if (effectVolume != null)
            effectVolume.weight = 0f;
    }

    private void OnDisable()
    {
        if (effectVolume != null)
            effectVolume.weight = 0f;
    }

    private void Update()
    {
        if (effectVolume == null)
            return;

        bool active = QuickTimeEvent.Instance != null &&
                      QuickTimeEvent.Instance.IsActive;

        float target = active ? 1f : 0f;
        float fadeTime = active ? fadeInTime : fadeOutTime;

        // Unscaled time, because Time.timeScale is 0 during the QTE.
        strength = Mathf.MoveTowards(
            strength, target,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));

        float pulse = 1f;
        if (active)
            pulse = 1f - pulseAmount + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;

        effectVolume.weight = Mathf.Clamp01(strength * pulse);
    }
}