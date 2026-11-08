using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AboutPanelController : MonoBehaviour
{
    [SerializeField] private GameObject aboutPanel;

    [Header("Animation")]
    [Tooltip("Duration of the open/close scale animation in seconds")]
    [SerializeField] private float animDuration = 0.45f;
    [Tooltip("Overshoot scale during open (bounce effect)")]
    [SerializeField] private float overshootScale = 1.08f;

    // Optional dim overlay behind the book panel
    [SerializeField] private Image dimOverlay;

    private Coroutine _animCoroutine;

    private void Awake()
    {
        // Start hidden with scale 0 so animation starts from scratch each time
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(false);
            aboutPanel.transform.localScale = Vector3.zero;
        }

        if (dimOverlay != null)
        {
            var c = dimOverlay.color;
            c.a = 0f;
            dimOverlay.color = c;
            dimOverlay.gameObject.SetActive(false);
        }
    }

    public void OpenAbout()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);

        aboutPanel.SetActive(true);
        if (dimOverlay != null) dimOverlay.gameObject.SetActive(true);

        _animCoroutine = StartCoroutine(AnimateOpen());
    }

    public void CloseAbout()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateClose());
    }

    private IEnumerator AnimateOpen()
    {
        float elapsed = 0f;
        float half = animDuration * 0.6f;
        float secondHalf = animDuration * 0.4f;

        // Phase 1: scale 0 → overshoot
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float scale = Mathf.Lerp(0f, overshootScale, EaseOutCubic(t));
            aboutPanel.transform.localScale = Vector3.one * scale;

            if (dimOverlay != null)
            {
                var c = dimOverlay.color;
                c.a = Mathf.Lerp(0f, 0.55f, t);
                dimOverlay.color = c;
            }

            yield return null;
        }

        // Phase 2: overshoot → 1
        elapsed = 0f;
        while (elapsed < secondHalf)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / secondHalf);
            float scale = Mathf.Lerp(overshootScale, 1f, EaseOutCubic(t));
            aboutPanel.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        aboutPanel.transform.localScale = Vector3.one;
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        Vector3 startScale = aboutPanel.transform.localScale;
        float startAlpha = dimOverlay != null ? dimOverlay.color.a : 0f;

        while (elapsed < animDuration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (animDuration * 0.5f));
            aboutPanel.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, EaseInCubic(t));

            if (dimOverlay != null)
            {
                var c = dimOverlay.color;
                c.a = Mathf.Lerp(startAlpha, 0f, t);
                dimOverlay.color = c;
            }

            yield return null;
        }

        aboutPanel.SetActive(false);
        aboutPanel.transform.localScale = Vector3.zero;
        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;

    private void Update()
    {
        // Esc also closes the panel
        if (aboutPanel != null && aboutPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseAbout();
    }
}
