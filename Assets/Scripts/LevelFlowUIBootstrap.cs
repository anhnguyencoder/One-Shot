using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LevelFlowUIBootstrap
{
    private const string LevelSelectSceneName = "LevelSelect";
    private const string UiRootName = "OneShotLevelFlowUI";
    private const int FallbackMaxLevel = 20;

    private struct LevelSceneInfo
    {
        public int level;
        public string sceneName;
    }

    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _hooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUiForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        BuildUiForScene(scene);
    }

    private static void BuildUiForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (FindRootByName(scene, UiRootName) != null)
        {
            return;
        }

        if (string.Equals(scene.name, LevelSelectSceneName, StringComparison.OrdinalIgnoreCase))
        {
            BuildLevelSelectUi(scene);
            return;
        }

        if (TryParseLevel(scene.name, out int levelIndex))
        {
            BuildLevelHud(scene, levelIndex);
        }
    }

    private static void BuildLevelSelectUi(Scene scene)
    {
        GameObject uiRoot = CreateUiRoot(scene);
        Font font = GetDefaultFont();
        AttachFpsCounter(uiRoot.transform, font);

        GameObject panel = CreatePanel(
            uiRoot.transform,
            "SelectPanel",
            new Vector2(980f, 760f),
            new Color(0.05f, 0.09f, 0.16f, 0.82f));

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Text title = CreateText(panel.transform, "Title", font, "CHON LEVEL", 52, FontStyle.Bold, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 90f);
        titleRect.anchoredPosition = new Vector2(0f, -26f);
        title.alignment = TextAnchor.MiddleCenter;

        GameObject gridObj = new GameObject("LevelGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObj.transform.SetParent(panel.transform, false);
        RectTransform gridRect = gridObj.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(860f, 560f);
        gridRect.anchoredPosition = new Vector2(0f, -35f);

        GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(190f, 78f);
        grid.spacing = new Vector2(20f, 20f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        List<LevelSceneInfo> levels = GetConfiguredLevelScenes();
        for (int i = 0; i < levels.Count; i++)
        {
            LevelSceneInfo info = levels[i];
            Button button = CreateButton(gridObj.transform, font, $"Level {info.level}");
            string targetScene = info.sceneName;
            button.onClick.AddListener(() => SceneManager.LoadScene(targetScene));
        }

        EnsureEventSystem(scene);
    }

    private static void BuildLevelHud(Scene scene, int levelIndex)
    {
        GameObject uiRoot = CreateUiRoot(scene);
        Font font = GetDefaultFont();
        AttachFpsCounter(uiRoot.transform, font);

        Text title = CreateText(uiRoot.transform, "LevelTitle", font, $"Level {levelIndex}", 48, FontStyle.Bold, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(480f, 70f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        title.alignment = TextAnchor.MiddleCenter;

        string previousScene = ResolvePreviousScene(levelIndex);
        if (!string.IsNullOrEmpty(previousScene))
        {
            Button backButton = CreateButton(uiRoot.transform, font, "Back");
            RectTransform buttonRect = backButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0f, 0f);
            buttonRect.sizeDelta = new Vector2(180f, 64f);
            buttonRect.anchoredPosition = new Vector2(24f, 24f);

            string targetScene = previousScene;
            backButton.onClick.AddListener(() => SceneManager.LoadScene(targetScene));
        }

        string nextScene = ResolveNextScene(levelIndex);
        if (!string.IsNullOrEmpty(nextScene))
        {
            Button nextButton = CreateButton(uiRoot.transform, font, "Next");
            RectTransform buttonRect = nextButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.sizeDelta = new Vector2(180f, 64f);
            buttonRect.anchoredPosition = new Vector2(-24f, 24f);

            string targetScene = nextScene;
            nextButton.onClick.AddListener(() => SceneManager.LoadScene(targetScene));
        }

        EnsureEventSystem(scene);
    }

    private static string ResolvePreviousScene(int currentLevel)
    {
        List<LevelSceneInfo> levels = GetConfiguredLevelScenes();
        if (levels.Count > 0)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].level != currentLevel)
                {
                    continue;
                }

                if (i - 1 >= 0)
                {
                    return levels[i - 1].sceneName;
                }

                return HasSceneInBuild(LevelSelectSceneName) ? LevelSelectSceneName : levels[levels.Count - 1].sceneName;
            }

            for (int i = levels.Count - 1; i >= 0; i--)
            {
                if (levels[i].level < currentLevel)
                {
                    return levels[i].sceneName;
                }
            }

            return HasSceneInBuild(LevelSelectSceneName) ? LevelSelectSceneName : levels[levels.Count - 1].sceneName;
        }

        int fallbackPrevious = currentLevel - 1;
        if (fallbackPrevious >= 1)
        {
            return $"LV{fallbackPrevious}";
        }

        return LevelSelectSceneName;
    }

    private static string ResolveNextScene(int currentLevel)
    {
        List<LevelSceneInfo> levels = GetConfiguredLevelScenes();
        if (levels.Count > 0)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].level != currentLevel)
                {
                    continue;
                }

                if (i + 1 < levels.Count)
                {
                    return levels[i + 1].sceneName;
                }

                return HasSceneInBuild(LevelSelectSceneName) ? LevelSelectSceneName : levels[0].sceneName;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].level > currentLevel)
                {
                    return levels[i].sceneName;
                }
            }

            return HasSceneInBuild(LevelSelectSceneName) ? LevelSelectSceneName : levels[0].sceneName;
        }

        int fallbackNext = currentLevel + 1;
        if (fallbackNext <= FallbackMaxLevel)
        {
            return $"LV{fallbackNext}";
        }

        return LevelSelectSceneName;
    }

    private static List<LevelSceneInfo> GetConfiguredLevelScenes()
    {
        List<LevelSceneInfo> results = new List<LevelSceneInfo>();
        int buildSceneCount = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < buildSceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (!TryParseLevel(sceneName, out int level))
            {
                continue;
            }

            results.Add(new LevelSceneInfo
            {
                level = level,
                sceneName = sceneName
            });
        }

        if (results.Count == 0)
        {
            for (int level = 1; level <= FallbackMaxLevel; level++)
            {
                results.Add(new LevelSceneInfo
                {
                    level = level,
                    sceneName = $"LV{level}"
                });
            }
        }

        results.Sort((a, b) => a.level.CompareTo(b.level));
        return results;
    }

    private static bool HasSceneInBuild(string targetSceneName)
    {
        int buildSceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < buildSceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(sceneName, targetSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseLevel(string sceneName, out int levelIndex)
    {
        levelIndex = 0;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (!sceneName.StartsWith("LV", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = sceneName.Substring(2);
        if (!int.TryParse(suffix, out int parsed) || parsed <= 0)
        {
            return false;
        }

        levelIndex = parsed;
        return true;
    }

    private static GameObject CreateUiRoot(Scene scene)
    {
        GameObject uiRoot = new GameObject(
            UiRootName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        SceneManager.MoveGameObjectToScene(uiRoot, scene);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        return uiRoot;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return panel;
    }

    private static Text CreateText(Transform parent, string name, Font font, string textValue, int fontSize, FontStyle style, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = textValue;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private static void AttachFpsCounter(Transform parent, Font font)
    {
        Text fpsText = CreateText(parent, "FpsCounter", font, "FPS: --", 28, FontStyle.Bold, Color.white);
        RectTransform rect = fpsText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(220f, 56f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        fpsText.alignment = TextAnchor.MiddleRight;
        fpsText.raycastTarget = false;

        fpsText.gameObject.AddComponent<FpsCounterTextUpdater>();
    }

    private static Button CreateButton(Transform parent, Font font, string label)
    {
        GameObject buttonObj = new GameObject(label.Replace(" ", ""), typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        Image image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.12f, 0.45f, 0.76f, 0.95f);

        Button button = buttonObj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.17f, 0.56f, 0.91f, 1f);
        colors.pressedColor = new Color(0.08f, 0.34f, 0.59f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
        button.colors = colors;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObj.transform.SetParent(buttonObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObj.GetComponent<Text>();
        labelText.text = label;
        labelText.font = font;
        labelText.fontSize = 30;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;

        return button;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static GameObject FindRootByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponentInChildren<EventSystem>(true) != null)
            {
                return;
            }
        }

        GameObject eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObj, scene);
    }
}

public class FpsCounterTextUpdater : MonoBehaviour
{
    private Text _text;
    private float _smoothedDeltaTime;

    private void Awake()
    {
        _text = GetComponent<Text>();
        _smoothedDeltaTime = Time.unscaledDeltaTime;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _smoothedDeltaTime += (dt - _smoothedDeltaTime) * 0.1f;
        float fps = _smoothedDeltaTime > 0f ? (1f / _smoothedDeltaTime) : 0f;
        if (_text != null)
        {
            _text.text = $"FPS: {Mathf.RoundToInt(fps)}";
        }
    }
}
