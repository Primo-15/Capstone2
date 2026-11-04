using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QuickTimeEvent : MonoBehaviour
{
    public static QuickTimeEvent Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text messageText;   // optional
    [SerializeField] private Image timerBar;         // optional, Image Type = Simple, shrinks to center

    [Header("Settings")]
    [SerializeField] private float timeLimit = 1.5f;
    [SerializeField] private float inputDelay = 0.15f;       // ignores keys held at the moment of the catch
    [SerializeField] private float resultDisplayTime = 0.6f;
    [SerializeField] private string followCameraName = "PlayerFollowCamera";
    [SerializeField] private Behaviour[] disableDuringEvent;

    private static readonly string[] labels = { "E", "F", "R", "G", "T" };
#if ENABLE_INPUT_SYSTEM
    private static readonly Key[] keys = { Key.E, Key.F, Key.R, Key.G, Key.T };
#else
    private static readonly KeyCode[] keys =
        { KeyCode.E, KeyCode.F, KeyCode.R, KeyCode.G, KeyCode.T };
#endif

    public bool IsActive { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        qtePanel.SetActive(false);
        AutoFindGameplayInput();
    }

    private void AutoFindGameplayInput()
    {
        var list = new List<Behaviour>();
        if (disableDuringEvent != null)
            list.AddRange(disableDuringEvent);

        GameObject cam = GameObject.Find(followCameraName);
        if (cam != null)
        {
            foreach (Behaviour b in cam.GetComponents<Behaviour>())
            {
                if (b != null &&
                    b.GetType().Name == "CinemachineCamera" &&
                    !list.Contains(b))
                {
                    list.Add(b);
                }
            }
        }

        disableDuringEvent = list.ToArray();
    }

    private void SetGameplayInput(bool enabled)
    {
        foreach (var b in disableDuringEvent)
            if (b != null) b.enabled = enabled;
    }

    private bool WasPressed(int index)
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current[keys[index]].wasPressedThisFrame;
#else
        return Input.GetKeyDown(keys[index]);
#endif
    }

    private void SetTimerScale(float t)
    {
        if (timerBar != null)
            timerBar.rectTransform.localScale = new Vector3(t, t, 1f);
    }

    // Returns false if an event is already running.
    public bool StartEvent(Action onSuccess, Action onFail)
    {
        if (IsActive) return false;
        StartCoroutine(Run(onSuccess, onFail));
        return true;
    }

    private IEnumerator Run(Action onSuccess, Action onFail)
    {
        IsActive = true;

        int target = UnityEngine.Random.Range(0, keys.Length);

        qtePanel.SetActive(true);
        keyText.text = labels[target];
        if (messageText != null) messageText.text = "Press the key to dodge!";
        SetTimerScale(1f);

        Time.timeScale = 0f;
        SetGameplayInput(false);

        float timer = timeLimit;
        float elapsed = 0f;
        bool resolved = false;
        bool success = false;

        while (!resolved && timer > 0f)
        {
            // Unscaled time, because the game is frozen.
            timer -= Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;

            // Circle shrinks from the outside toward the center.
            SetTimerScale(Mathf.Clamp01(timer / timeLimit));

            if (elapsed >= inputDelay)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!WasPressed(i)) continue;

                    success = (i == target);
                    resolved = true;
                    break;
                }
            }

            yield return null;
        }

        if (messageText != null)
            messageText.text = success ? "DODGED!" : "CAUGHT!";

        yield return new WaitForSecondsRealtime(resultDisplayTime);

        qtePanel.SetActive(false);
        IsActive = false;

        if (success)
        {
            Time.timeScale = 1f;
            SetGameplayInput(true);
            onSuccess?.Invoke();
        }
        else
        {
            // Stay frozen; the game over screen takes over from here.
            onFail?.Invoke();
        }
    }
}