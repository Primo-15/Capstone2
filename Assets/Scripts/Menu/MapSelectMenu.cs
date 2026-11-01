using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelectMenu : MonoBehaviour
{
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private Button buttonPrefab;

    [Tooltip("Scene names that should NOT appear as maps.")]
    [SerializeField]
    private string[] excludedScenes =
    {
        "Main Menu",
        "SampleScene"
    };

    private void Start()
    {
        BuildButtons();
    }

    private List<string> GetMapNames()
    {
        List<string> maps = new List<string>();
        int count = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(path);

            if (System.Array.IndexOf(excludedScenes, sceneName) >= 0)
            {
                continue;
            }

            maps.Add(sceneName);
        }

        return maps;
    }

    private void BuildButtons()
    {
        foreach (string mapName in GetMapNames())
        {
            Button button = Instantiate(buttonPrefab, buttonContainer);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = mapName;
            }

            string target = mapName;
            button.onClick.AddListener(() => LoadMap(target));
        }
    }

    public void LoadMap(string mapName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mapName);
    }

    // Optional: call this from a "Next Map" button or when a map is finished.
    public void LoadNextMap()
    {
        List<string> maps = GetMapNames();
        int current = maps.IndexOf(SceneManager.GetActiveScene().name);

        if (current < 0 || current + 1 >= maps.Count)
        {
            return;
        }

        LoadMap(maps[current + 1]);
    }
}