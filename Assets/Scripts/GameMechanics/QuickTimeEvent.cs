using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;
using UnityEngine.SceneManagement;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QuickTimeEvent : MonoBehaviour
{
    public VideoPlayer storyVideoPlayer;
    public GameObject storyVideoCanvas;
    public static QuickTimeEvent Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text messageText;   // optional
    [SerializeField] private Image timerBar;         // optional, Image Type = Filled, Radial 360

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
        if (storyVideoCanvas != null)
            storyVideoCanvas.SetActive(false);
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

    // Radial pie: 1 = full circle, 0 = empty.
    private void SetTimerFill(float t)
    {
        if (timerBar != null)
            timerBar.fillAmount = Mathf.Clamp01(t);
    }

    // Returns false if an event is already running or the player has no stamina.
    public bool StartEvent(Action onSuccess, Action onFail)
    {
        if (IsActive) return false;
        if (!PlayerStats.UseStamina()) return false;   // each QTE costs 1 stamina

        StartCoroutine(Run(onSuccess, onFail));
        return true;
    }
    private void OnVideoFinished(VideoPlayer vp)
    {
        storyVideoPlayer.loopPointReached -= OnVideoFinished;

    }
    private IEnumerator Run(Action onSuccess, Action onFail)
    {
        IsActive = true;
        SceneMusic.Current?.BeginQTE();

        int target = UnityEngine.Random.Range(0, keys.Length);
        storyVideoPlayer.Stop();
        storyVideoPlayer.loopPointReached -= OnVideoFinished;
        storyVideoPlayer.loopPointReached += OnVideoFinished;

        storyVideoPlayer.Play();
        if (storyVideoCanvas != null)
            storyVideoCanvas.SetActive(true);

        qtePanel.SetActive(true);
        keyText.text = labels[target];
        if (messageText != null) messageText.text = "Press the key to dodge!";

        if (timerBar != null)
            timerBar.rectTransform.localScale = Vector3.one;   // undo the old shrinking effect
        SetTimerFill(1f);

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

            // The pie drains as time runs out.
            SetTimerFill(timer / timeLimit);

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

        // Exactly one sound per attempt (right key, wrong key or timeout)
        if (success) SceneMusic.Current?.PlayHitPass();
        else SceneMusic.Current?.PlayHitFail();

        if (messageText != null)
            messageText.text = success ? "DODGED!" : "CAUGHT!";

        yield return new WaitForSecondsRealtime(resultDisplayTime);

        qtePanel.SetActive(false);
        if (storyVideoCanvas != null)
            storyVideoCanvas.SetActive(false);
        if (storyVideoPlayer != null)
            storyVideoPlayer.Stop();
        IsActive = false;
        SceneMusic.Current?.EndQTE(success);

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