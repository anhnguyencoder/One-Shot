using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PunchUncleLevelBuilder : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField, Min(7)] private int arenaWidth = 11;
    [SerializeField, Min(12)] private int arenaLength = 18;
    [SerializeField, Min(1)] private int floorThickness = 2;
    [SerializeField, Min(1)] private int sideWallHeight = 1;
    [SerializeField, Min(1)] private int obstacleHeight = 1;
    [SerializeField] private float topSurfaceY = 0f;

    [Header("Enemy")]
    [SerializeField] private GameObject angryPrefab;
    [SerializeField, Min(1)] private int angryCount = 3;
    [SerializeField, Min(0.5f)] private float angryScale = 1.8f;

    [Header("Levels")]
    [SerializeField, Range(1, 2)] private int currentLevel = 1;
    [SerializeField] private bool createLevelSwitchUI = true;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject grassBlockPrefab;
    [SerializeField] private GameObject dirtBlockPrefab;
    [SerializeField] private GameObject brickBlockPrefab;
    [SerializeField] private GameObject metalBlockPrefab;
    [SerializeField] private GameObject crateBlockPrefab;

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool buildInEditMode = true;
    [SerializeField] private bool clearOtherRootsOnBuild = true;
    [SerializeField] private bool clearExistingChildrenOnBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("Camera")]
    [SerializeField] private bool autoFrameMainCamera = true;
    [SerializeField, Min(2f)] private float cameraHeight = 3.2f;
    [SerializeField, Min(2f)] private float cameraBackDistance = 3.2f;
    [SerializeField, Min(8f)] private float cameraLookDistance = 14f;
    [SerializeField] private float cameraSideOffset = 0f;
    [SerializeField, Range(35f, 75f)] private float cameraFieldOfView = 40f;

    private const string LevelRootName = "PunchLevelContent";
    private const string UiRootName = "LevelSwitchUI";
    private const string GrassPath = "Assets/Cube World Kit-zip/Grass Block/Block_Grass.fbx";
    private const string DirtPath = "Assets/Cube World Kit-zip/Dirt Block/Block_Dirt.fbx";
    private const string BrickPath = "Assets/Cube World Kit-zip/Brick Block/Block_Brick.fbx";
    private const string MetalPath = "Assets/Cube World Kit-zip/Metal Block/Block_Metal.fbx";
    private const string CratePath = "Assets/Cube World Kit-zip/Crate/Block_Crate.fbx";
    private const string AngryPathPrimary = "Assets/Angry.fbx";
    private const string AngryPathFallback = "Assets/CharactersMixPack1/Prefabs/Angry_01.prefab";

    private Transform levelRoot;
    private float blockSizeX = 1f;
    private float blockSizeY = 1f;
    private float pivotToTop = 0.5f;

    private void Start()
    {
        if (Application.isPlaying && rebuildOnStart)
        {
            BuildLevel();
        }
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying || !buildInEditMode)
        {
            return;
        }

        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            BuildLevel();
        }
    }
#endif

    [ContextMenu("Build Punch Level")]
    public void BuildLevel()
    {
        currentLevel = Mathf.Clamp(currentLevel, 1, 2);

        if (!TryResolveAssets())
        {
            Debug.LogError("PunchUncleLevelBuilder: Missing required prefabs.");
            return;
        }

        CaptureBlockMetrics();
        if (clearOtherRootsOnBuild)
        {
            ClearOtherRoots();
        }

        PrepareLevelRoot();
        if (clearExistingChildrenOnBuild)
        {
            ClearDirectChildrenExceptLevelRoot();
        }
        ClearLevelRootChildren();

        if (currentLevel == 1)
        {
            BuildLevelOne();
        }
        else
        {
            BuildLevelTwo();
        }

        Physics.SyncTransforms();
        EnsureLevelSwitchUI();
    }

    [ContextMenu("Build Level 1")]
    public void SetLevel1()
    {
        SetLevel(1);
    }

    [ContextMenu("Build Level 2")]
    public void SetLevel2()
    {
        SetLevel(2);
    }

    public void SetLevel(int levelIndex)
    {
        currentLevel = Mathf.Clamp(levelIndex, 1, 2);
        BuildLevel();
    }

    [ContextMenu("Clear Punch Level")]
    public void ClearLevel()
    {
        PrepareLevelRoot();
        ClearLevelRootChildren();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        TryAutoAssignAssets();
    }

    private void OnValidate()
    {
        arenaWidth = Mathf.Max(7, arenaWidth);
        if (arenaWidth % 2 == 0) arenaWidth += 1;
        arenaLength = Mathf.Max(12, arenaLength);
        floorThickness = Mathf.Max(1, floorThickness);
        sideWallHeight = Mathf.Max(1, sideWallHeight);
        obstacleHeight = Mathf.Max(1, obstacleHeight);
        angryCount = Mathf.Max(1, angryCount);
        angryScale = Mathf.Max(0.5f, angryScale);
        currentLevel = Mathf.Clamp(currentLevel, 1, 2);
        cameraHeight = Mathf.Max(2f, cameraHeight);
        cameraBackDistance = Mathf.Max(2f, cameraBackDistance);
        cameraLookDistance = Mathf.Max(8f, cameraLookDistance);
        cameraSideOffset = Mathf.Clamp(cameraSideOffset, -3f, 3f);
        TryAutoAssignAssets();
    }

    private bool TryAutoAssignAssets()
    {
        grassBlockPrefab = grassBlockPrefab != null ? grassBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(GrassPath);
        dirtBlockPrefab = dirtBlockPrefab != null ? dirtBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DirtPath);
        brickBlockPrefab = brickBlockPrefab != null ? brickBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(BrickPath);
        metalBlockPrefab = metalBlockPrefab != null ? metalBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(MetalPath);
        crateBlockPrefab = crateBlockPrefab != null ? crateBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(CratePath);
        angryPrefab = angryPrefab != null ? angryPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(AngryPathPrimary);
        angryPrefab = angryPrefab != null ? angryPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(AngryPathFallback);

        return grassBlockPrefab != null
            && dirtBlockPrefab != null
            && brickBlockPrefab != null
            && metalBlockPrefab != null
            && crateBlockPrefab != null
            && angryPrefab != null;
    }
#endif

    private bool TryResolveAssets()
    {
#if UNITY_EDITOR
        TryAutoAssignAssets();
#endif
        return grassBlockPrefab != null
            && dirtBlockPrefab != null
            && brickBlockPrefab != null
            && metalBlockPrefab != null
            && crateBlockPrefab != null
            && angryPrefab != null;
    }

    private void BuildLevelOne()
    {
        int length = arenaLength;
        int stageHalfWidth = Mathf.Max(2, arenaWidth / 3);

        BuildFloor(length);
        BuildSideWalls(length, sideWallHeight, 4);
        BuildLevel1Obstacles(length, obstacleHeight);
        BuildFinalStage(length, 2, stageHalfWidth, 2);
        Physics.SyncTransforms();
        SpawnAngryLine(length, angryCount, angryScale);
        FrameMainCamera(length, cameraHeight, cameraBackDistance, cameraLookDistance, cameraSideOffset, cameraFieldOfView);
    }

    private void BuildLevelTwo()
    {
        int length = arenaLength + 6;
        int wallHeight = sideWallHeight + 1;
        int rowHeight = obstacleHeight + 1;
        int targetCount = Mathf.Max(5, angryCount + 2);
        float targetScale = angryScale * 1.15f;
        int stageHalfWidth = Mathf.Max(3, (arenaWidth / 2) - 1);

        BuildFloor(length);
        BuildSideWalls(length, wallHeight, 3);
        BuildLevel2Obstacles(length, rowHeight);
        BuildFinalStage(length, 3, stageHalfWidth, 3);
        Physics.SyncTransforms();
        SpawnAngryVFormation(length, targetCount, targetScale);
        FrameMainCamera(
            length,
            Mathf.Max(2f, cameraHeight - 0.1f),
            cameraBackDistance + 0.8f,
            cameraLookDistance + 4f,
            cameraSideOffset,
            Mathf.Clamp(cameraFieldOfView + 2f, 35f, 75f));
    }

    private void CaptureBlockMetrics()
    {
        GameObject probe = Instantiate(grassBlockPrefab);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;
        probe.transform.localScale = Vector3.one;

        if (TryGetWorldBounds(probe, out Bounds bounds))
        {
            blockSizeX = Mathf.Max(0.01f, bounds.size.x);
            blockSizeY = Mathf.Max(0.01f, bounds.size.y);
            pivotToTop = bounds.max.y - probe.transform.position.y;
            if (pivotToTop <= 0f)
            {
                pivotToTop = blockSizeY * 0.5f;
            }
        }
        else
        {
            blockSizeX = 1f;
            blockSizeY = 1f;
            pivotToTop = 0.5f;
        }

        if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);
    }

    private void ClearOtherRoots()
    {
        Scene scene = gameObject.scene;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == gameObject)
            {
                continue;
            }

            bool keepRoot = root.GetComponentInChildren<Camera>() != null
                || root.GetComponentInChildren<Light>() != null;

            if (keepRoot)
            {
                continue;
            }

            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
    }

    private void PrepareLevelRoot()
    {
        if (levelRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(LevelRootName);
        if (existing != null)
        {
            levelRoot = existing;
            return;
        }

        GameObject root = new GameObject(LevelRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        levelRoot = root.transform;
    }

    private void ClearLevelRootChildren()
    {
        if (levelRoot == null)
        {
            return;
        }

        for (int i = levelRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = levelRoot.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
    }

    private void ClearDirectChildrenExceptLevelRoot()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == levelRoot)
            {
                continue;
            }

            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
    }

    private void BuildFloor(int length)
    {
        int halfWidth = arenaWidth / 2;
        for (int z = 0; z < length; z++)
        {
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                for (int y = -floorThickness + 1; y <= 0; y++)
                {
                    GameObject prefab = y == 0 ? grassBlockPrefab : dirtBlockPrefab;
                    SpawnBlock(prefab, x, y, z, $"Floor_{x}_{y}_{z}");
                }
            }
        }
    }

    private void BuildSideWalls(int length, int wallHeight, int pillarSpacing)
    {
        int halfWidth = arenaWidth / 2;
        int wallX = halfWidth + 1;
        int wallStartZ = 1;
        int wallEndZ = length - 4;

        for (int z = wallStartZ; z <= wallEndZ; z++)
        {
            for (int y = 1; y <= wallHeight; y++)
            {
                SpawnBlock(metalBlockPrefab, -wallX, y, z, $"Wall_L_{y}_{z}");
                SpawnBlock(metalBlockPrefab, wallX, y, z, $"Wall_R_{y}_{z}");
            }

            if (pillarSpacing > 0 && z % pillarSpacing == 0)
            {
                SpawnBlock(brickBlockPrefab, -wallX, wallHeight + 1, z, $"Pillar_L_{z}");
                SpawnBlock(brickBlockPrefab, wallX, wallHeight + 1, z, $"Pillar_R_{z}");
            }
        }
    }

    private void BuildLevel1Obstacles(int length, int rowHeight)
    {
        int rowA = Mathf.Clamp(Mathf.RoundToInt(length * 0.33f), 4, length - 6);
        int rowB = Mathf.Clamp(Mathf.RoundToInt(length * 0.55f), rowA + 2, length - 4);
        int rowC = Mathf.Clamp(Mathf.RoundToInt(length * 0.75f), rowB + 2, length - 2);
        int[] rows = { rowA, rowB, rowC };

        for (int i = 0; i < rows.Length; i++)
        {
            int openSide = i % 2 == 0 ? -1 : 1;
            BuildObstacleRow(rows[i], openSide, rowHeight, 1, 1);
        }
    }

    private void BuildLevel2Obstacles(int length, int rowHeight)
    {
        int rowA = Mathf.Clamp(Mathf.RoundToInt(length * 0.24f), 4, length - 10);
        int rowB = Mathf.Clamp(Mathf.RoundToInt(length * 0.42f), rowA + 2, length - 8);
        int rowC = Mathf.Clamp(Mathf.RoundToInt(length * 0.58f), rowB + 2, length - 6);
        int rowD = Mathf.Clamp(Mathf.RoundToInt(length * 0.74f), rowC + 2, length - 4);
        int rowE = Mathf.Clamp(Mathf.RoundToInt(length * 0.86f), rowD + 2, length - 2);

        BuildObstacleRow(rowA, -1, rowHeight, 1, 1);
        BuildObstacleRow(rowB, 1, rowHeight, 0, 2);
        BuildObstacleRow(rowC, -1, rowHeight, 1, 1);
        BuildObstacleRow(rowD, 1, rowHeight, 0, 2);
        BuildObstacleRow(rowE, 0, rowHeight, 1, 0);
    }

    private void BuildObstacleRow(int z, int openSide, int rowHeight, int centerGapHalfWidth, int sideGapWidth)
    {
        int halfWidth = arenaWidth / 2;
        int sideGapCenter = openSide * (halfWidth - 1);
        int sideGapMin = sideGapCenter - Mathf.Max(0, sideGapWidth - 1);
        int sideGapMax = sideGapCenter;
        if (sideGapMin > sideGapMax)
        {
            int swap = sideGapMin;
            sideGapMin = sideGapMax;
            sideGapMax = swap;
        }

        for (int x = -halfWidth + 1; x <= halfWidth - 1; x++)
        {
            bool inCenterGap = Mathf.Abs(x) <= centerGapHalfWidth;
            bool inSideGap = sideGapWidth > 0 && x >= sideGapMin && x <= sideGapMax;
            if (inCenterGap || inSideGap)
            {
                continue;
            }

            for (int y = 1; y <= rowHeight; y++)
            {
                GameObject prefab = y == rowHeight ? crateBlockPrefab : brickBlockPrefab;
                SpawnBlock(prefab, x, y, z, $"Obstacle_{x}_{y}_{z}");
            }
        }
    }

    private void BuildFinalStage(int length, int stageHeight, int halfStageWidth, int stairCount)
    {
        int stageStart = length - 2;
        int stageEnd = length + 2;

        for (int z = stageStart; z <= stageEnd; z++)
        {
            for (int x = -halfStageWidth; x <= halfStageWidth; x++)
            {
                for (int y = 1; y <= stageHeight; y++)
                {
                    GameObject prefab = y == stageHeight ? metalBlockPrefab : brickBlockPrefab;
                    SpawnBlock(prefab, x, y, z, $"Stage_{x}_{y}_{z}");
                }
            }
        }

        for (int step = 0; step < stairCount; step++)
        {
            int z = stageStart - 1 - step;
            int y = Mathf.Max(1, stageHeight - step);
            for (int x = -2; x <= 2; x++)
            {
                SpawnBlock(brickBlockPrefab, x, y, z, $"Stair_{x}_{step}_{z}");
            }
        }
    }

    private void SpawnAngryLine(int length, int count, float scale)
    {
        int[] laneX = { 0, -2, 2, -4, 4 };
        int spawnZ = length + 1;

        for (int i = 0; i < count; i++)
        {
            int row = i / laneX.Length;
            int col = i % laneX.Length;
            SpawnSingleAngry(i + 1, laneX[col], spawnZ + (row * 2), scale);
        }
    }

    private void SpawnAngryVFormation(int length, int count, float scale)
    {
        Vector2Int[] formation = new Vector2Int[]
        {
            new Vector2Int(0, length + 2),
            new Vector2Int(-2, length + 3),
            new Vector2Int(2, length + 3),
            new Vector2Int(-4, length + 4),
            new Vector2Int(4, length + 4),
            new Vector2Int(0, length + 5)
        };

        int spawnCount = Mathf.Min(count, formation.Length);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnSingleAngry(i + 1, formation[i].x, formation[i].y, scale);
        }
    }

    private void SpawnSingleAngry(int index, int gx, int gz, float scale)
    {
        if (angryPrefab == null)
        {
            return;
        }

        GameObject angry = Instantiate(angryPrefab, levelRoot);
        angry.name = $"Angry_Target_{index}";
        angry.transform.localPosition = GridToLocal(gx, 3, gz);
        angry.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        angry.transform.localScale = Vector3.one * scale;
        SnapActorFeetToGround(angry.transform);
    }

    private void FrameMainCamera(int length, float height, float backDistance, float lookDistance, float sideOffset, float fov)
    {
        if (!autoFrameMainCamera)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = FindObjectOfType<Camera>();
        }

        if (cam == null)
        {
            return;
        }

        float laneStartZ = -1f * blockSizeX;
        float laneFocusZ = Mathf.Min((length - 1) * blockSizeX, lookDistance * blockSizeX);

        Vector3 cameraLocal = new Vector3(
            sideOffset * blockSizeX,
            topSurfaceY + height,
            laneStartZ - (backDistance * blockSizeX));

        Vector3 focusLocal = new Vector3(0f, topSurfaceY + 1.6f, laneFocusZ);

        Transform camTransform = cam.transform;
        Vector3 worldPos = transform.TransformPoint(cameraLocal);
        Vector3 worldTarget = transform.TransformPoint(focusLocal);

        camTransform.position = worldPos;
        camTransform.rotation = Quaternion.LookRotation((worldTarget - worldPos).normalized, Vector3.up);
        cam.fieldOfView = fov;
    }

    private void EnsureLevelSwitchUI()
    {
        if (!createLevelSwitchUI)
        {
            return;
        }

        Transform existing = transform.Find(UiRootName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }

        GameObject uiRoot = new GameObject(UiRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 1f;

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(420f, 140f);
        panelRect.anchoredPosition = new Vector2(0f, -30f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.34f);

        Text title = CreateText(panel.transform, "Title", defaultFont, $"LEVEL {currentLevel}", 34, FontStyle.Bold, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(340f, 44f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);

        Button level1Button = CreateLevelButton(panel.transform, defaultFont, "Level 1", new Vector2(-95f, -72f));
        Button level2Button = CreateLevelButton(panel.transform, defaultFont, "Level 2", new Vector2(95f, -72f));
        level1Button.onClick.AddListener(SetLevel1);
        level2Button.onClick.AddListener(SetLevel2);

        Color activeColor = new Color(0.09f, 0.56f, 0.96f, 0.95f);
        Color idleColor = new Color(0f, 0f, 0f, 0.6f);
        level1Button.GetComponent<Image>().color = currentLevel == 1 ? activeColor : idleColor;
        level2Button.GetComponent<Image>().color = currentLevel == 2 ? activeColor : idleColor;

        EnsureEventSystem();
    }

    private static Button CreateLevelButton(Transform parent, Font font, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObj = new GameObject(label.Replace(" ", ""), typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.sizeDelta = new Vector2(170f, 52f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image image = buttonObj.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.6f);

        Button button = buttonObj.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObj.transform, "Label", font, label, 26, FontStyle.Bold, Color.white);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private static Text CreateText(Transform parent, string objectName, Font font, string textValue, int fontSize, FontStyle style, Color color)
    {
        GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);
        Text text = textObj.GetComponent<Text>();
        text.font = font;
        text.text = textValue;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObj.transform.position = Vector3.zero;
    }

    private void SpawnBlock(GameObject prefab, int gx, int gy, int gz, string objectName)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject block = Instantiate(prefab, levelRoot);
        block.name = objectName;
        block.transform.localPosition = GridToLocal(gx, gy, gz);
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = Vector3.one;
        EnsureCollider(block);
    }

    private Vector3 GridToLocal(int gx, int gy, int gz)
    {
        float localX = gx * blockSizeX;
        float localY = (topSurfaceY - pivotToTop) + (gy * blockSizeY);
        float localZ = gz * blockSizeX;
        return new Vector3(localX, localY, localZ);
    }

    private void SnapActorFeetToGround(Transform actorTransform)
    {
        if (!TryGetWorldBounds(actorTransform.gameObject, out Bounds actorBounds))
        {
            return;
        }

        float feetOffset = actorTransform.position.y - actorBounds.min.y;
        Vector3 rayOrigin = actorTransform.position + (Vector3.up * 25f);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = actorTransform.position;
            p.y = hit.point.y + feetOffset;
            actorTransform.position = p;
        }
    }

    private void EnsureCollider(GameObject block)
    {
        if (!addBoxColliderIfMissing || block.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        if (!TryGetWorldBounds(block, out Bounds worldBounds))
        {
            return;
        }

        BoxCollider box = block.AddComponent<BoxCollider>();
        Vector3 center = block.transform.InverseTransformPoint(worldBounds.center);
        Vector3 size = worldBounds.size;

        Vector3 lossyScale = block.transform.lossyScale;
        if (!Mathf.Approximately(lossyScale.x, 0f)) size.x /= Mathf.Abs(lossyScale.x);
        if (!Mathf.Approximately(lossyScale.y, 0f)) size.y /= Mathf.Abs(lossyScale.y);
        if (!Mathf.Approximately(lossyScale.z, 0f)) size.z /= Mathf.Abs(lossyScale.z);

        box.center = center;
        box.size = new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));
    }

    private static bool TryGetWorldBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }
}
