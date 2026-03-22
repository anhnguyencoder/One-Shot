using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV4LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV4_StonehengeShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 16;
    [SerializeField] private int groundBackDepth = 2;
    [SerializeField] private int groundFrontDepth = 30;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Stonehenge Mode")]
    [SerializeField] private bool use2DPixelArtMode = true;
    [SerializeField] private int artPlaneZ = 18;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 12;
    [SerializeField] private int artPanelHeight = 18;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = true;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;

    [Header("Flying Cloud Platform (Figure-8)")]
    [SerializeField] private bool buildFlyingCloudPlatform = true;
    [SerializeField] private bool buildSecondCloudPlatform = true;
    [SerializeField] private int cloudStartX = 0;
    [SerializeField] private int cloudStartY = 3;
    [SerializeField] private int cloudStartZ = 8;
    [SerializeField] private int cloudSecondStartX = 8;
    [SerializeField] private int cloudSecondStartY = 3;
    [SerializeField] private int cloudSecondStartZ = 8;
    [SerializeField] private string cloudSceneObjectName = "LV4_Figure8CloudPlatform";
    [SerializeField] private string cloudSecondSceneObjectName = "LV4_Figure8CloudPlatform_B";
    [SerializeField] private bool keepManualCloudTransform = true;
    [SerializeField] private float cloudFigure8RadiusX = 2.75f;
    [SerializeField] private float cloudFigure8RadiusZ = 1.5f;
    [SerializeField] private float cloudCycleDuration = 6f;
    [SerializeField] private float cloudBobAmplitude = 0.35f;
    [SerializeField] private float cloudBobFrequency = 1.25f;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane cloudPathPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private Transform cloudPassenger;
    [SerializeField] private Transform cloudSecondPassenger;

    [Header("Giza Landmark")]
    [SerializeField] private int mainPyramidHalf = 8;
    [SerializeField] private int mainPyramidHeight = 8;
    [SerializeField] private int mainPyramidZ = 16;
    [SerializeField] private int sidePyramidHalf = 5;
    [SerializeField] private int sidePyramidHeight = 5;
    [SerializeField] private int sidePyramidOffsetX = 12;
    [SerializeField] private int sidePyramidZ = 20;
    [SerializeField] private int sphinxZ = 8;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject cobblestonePrefab;
    [SerializeField] private GameObject minecraftCubePrefab;
    [SerializeField] private GameObject tntBlockPrefab;

    private const string CobblePath = "Assets/Prefabs/Coblstone.prefab";
    private const string MinecraftCubePath = "Assets/Prefabs/minecraft_cube.prefab";
    private const string TntPath = "Assets/Prefabs/minecraft block TNT.prefab";

    private Transform mapRoot;
    private Vector3 buildCenter;
    private float blockSizeX = 1f;
    private float blockSizeY = 1f;
    private float pivotToTop = 0.5f;

    private readonly Dictionary<GameObject, Vector3> prefabScaleMultiplierCache = new Dictionary<GameObject, Vector3>();
    private readonly HashSet<Vector3Int> occupiedGridCells = new HashSet<Vector3Int>();

#if UNITY_EDITOR
    private static bool s_IsEditorQuitting;
    private static bool s_IsSceneClosing;
    private bool _autoBuildQueued;
#endif

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            return;
        }

#if UNITY_EDITOR
        if (!autoBuildInEditMode)
        {
            return;
        }

        QueueAutoBuild();
#endif
    }

    private void Start()
    {
        if (Application.isPlaying && rebuildOnPlay)
        {
            BuildShowcase();
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorLifecycleHooks()
    {
        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.quitting += OnEditorQuitting;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        EditorSceneManager.sceneClosed -= OnSceneClosed;
        EditorSceneManager.sceneClosed += OnSceneClosed;
    }

    private static void OnEditorQuitting()
    {
        s_IsEditorQuitting = true;
    }

    private static void OnSceneClosing(Scene _, bool __)
    {
        s_IsSceneClosing = true;
    }

    private static void OnSceneClosed(Scene _)
    {
        s_IsSceneClosing = false;
    }

    private void OnDisable()
    {
        CancelQueuedAutoBuild();
    }

    private bool CanAutoBuildInEditor()
    {
        if (!autoBuildInEditMode)
        {
            return false;
        }

        if (s_IsEditorQuitting || s_IsSceneClosing)
        {
            return false;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return false;
        }

        if (EditorSceneManager.IsPreviewSceneObject(gameObject))
        {
            return false;
        }

        return gameObject.activeInHierarchy;
    }

    private void QueueAutoBuild()
    {
        if (_autoBuildQueued)
        {
            return;
        }

        _autoBuildQueued = true;
        EditorApplication.update -= TryRunQueuedAutoBuild;
        EditorApplication.update += TryRunQueuedAutoBuild;
    }

    private void CancelQueuedAutoBuild()
    {
        if (!_autoBuildQueued)
        {
            return;
        }

        _autoBuildQueued = false;
        EditorApplication.update -= TryRunQueuedAutoBuild;
    }

    private void TryRunQueuedAutoBuild()
    {
        if (this == null)
        {
            CancelQueuedAutoBuild();
            return;
        }

        if (!CanAutoBuildInEditor())
        {
            return;
        }

        CancelQueuedAutoBuild();
        BuildShowcase();
    }
#endif

    [ContextMenu("Build LV4 Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV4LandmarkBuilder: Could not resolve any usable block prefab.");
            return;
        }

        prefabScaleMultiplierCache.Clear();
        occupiedGridCells.Clear();

        CaptureBlockMetrics();
        ResolveBuildCenter();
        PrepareMapRoot();

        if (clearBeforeBuild)
        {
            ClearMapRootChildren();
        }
        BuildEnemyFrontPlatform();
        BuildCeremonialPath();
        if (use2DPixelArtMode)
        {
            Build2DArtPanel();
            BuildStonehengePixelArt();
            BuildHillsAndMoon();
        }
        else
        {
            BuildPyramids();
            BuildSphinx();
            BuildObelisks();
            BuildSunDisc();
        }

        BuildFlyingCloudPlatform();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear LV4 Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
        ClearStandaloneCloudPlatforms();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

#if UNITY_EDITOR
    private void Reset()
    {
        focusGroup = transform;
        TryAutoAssignPrefabs();
    }

    private void OnValidate()
    {
        groundHalfWidth = Mathf.Max(8, groundHalfWidth);
        groundBackDepth = Mathf.Max(0, groundBackDepth);
        groundFrontDepth = Mathf.Max(10, groundFrontDepth);
        artPlaneZ = Mathf.Clamp(artPlaneZ, 8, groundFrontDepth - 2);
        artBaseY = Mathf.Max(1, artBaseY);
        artPanelHalfWidth = Mathf.Clamp(artPanelHalfWidth, 6, Mathf.Max(6, groundHalfWidth - 2));
        artPanelHeight = Mathf.Max(10, artPanelHeight);
        enemyPlatformHalfWidth = Mathf.Max(4, enemyPlatformHalfWidth);
        enemyPlatformForwardLength = Mathf.Max(2, enemyPlatformForwardLength);
        cloudStartY = Mathf.Max(1, cloudStartY);
        cloudSecondStartY = Mathf.Max(1, cloudSecondStartY);
        cloudFigure8RadiusX = Mathf.Max(0.1f, cloudFigure8RadiusX);
        cloudFigure8RadiusZ = Mathf.Max(0.1f, cloudFigure8RadiusZ);
        cloudCycleDuration = Mathf.Max(0.2f, cloudCycleDuration);
        cloudBobAmplitude = Mathf.Max(0f, cloudBobAmplitude);
        cloudBobFrequency = Mathf.Max(0f, cloudBobFrequency);
        if (string.IsNullOrWhiteSpace(cloudSceneObjectName))
        {
            cloudSceneObjectName = "LV4_Figure8CloudPlatform";
        }
        if (string.IsNullOrWhiteSpace(cloudSecondSceneObjectName))
        {
            cloudSecondSceneObjectName = "LV4_Figure8CloudPlatform_B";
        }
        if (cloudSecondSceneObjectName == cloudSceneObjectName)
        {
            cloudSecondSceneObjectName = cloudSceneObjectName + "_B";
        }
        mainPyramidHalf = Mathf.Max(4, mainPyramidHalf);
        mainPyramidHeight = Mathf.Max(4, mainPyramidHeight);
        mainPyramidZ = Mathf.Max(8, mainPyramidZ);
        sidePyramidHalf = Mathf.Max(3, sidePyramidHalf);
        sidePyramidHeight = Mathf.Max(3, sidePyramidHeight);
        sidePyramidOffsetX = Mathf.Max(8, sidePyramidOffsetX);
        sidePyramidZ = Mathf.Max(mainPyramidZ + 2, sidePyramidZ);
        sphinxZ = Mathf.Clamp(sphinxZ, 2, mainPyramidZ - 3);
        TryAutoAssignPrefabs();
    }

    private bool TryAutoAssignPrefabs()
    {
        cobblestonePrefab = cobblestonePrefab != null ? cobblestonePrefab : AssetDatabase.LoadAssetAtPath<GameObject>(CobblePath);
        minecraftCubePrefab = minecraftCubePrefab != null ? minecraftCubePrefab : AssetDatabase.LoadAssetAtPath<GameObject>(MinecraftCubePath);
        tntBlockPrefab = tntBlockPrefab != null ? tntBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(TntPath);
        return ResolveAnyPrefab() != null;
    }
#endif

    private bool TryResolvePrefabs()
    {
#if UNITY_EDITOR
        TryAutoAssignPrefabs();
#endif
        return ResolveAnyPrefab() != null;
    }

    private GameObject ResolveAnyPrefab()
    {
        return FirstNonNull(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
    }

    private static GameObject FirstNonNull(params GameObject[] prefabs)
    {
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                return prefabs[i];
            }
        }

        return null;
    }

    private GameObject PickPrefab(params GameObject[] candidates)
    {
        GameObject picked = FirstNonNull(candidates);
        return picked != null ? picked : ResolveAnyPrefab();
    }

    private void ResolveBuildCenter()
    {
        Transform anchor = focusGroup != null ? focusGroup : transform;
        buildCenter = anchor.position;
        buildCenter.y = 0f;
    }

    private void PrepareMapRoot()
    {
        if (mapRoot != null && mapRoot.gameObject.scene == gameObject.scene)
        {
            return;
        }

        mapRoot = null;

        Transform existing = FindMapRootInCurrentScene();
        if (existing != null)
        {
            mapRoot = existing;
            return;
        }

        GameObject root = new GameObject(mapRootName);
        if (gameObject.scene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        }

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        mapRoot = root.transform;
    }

    private Transform FindMapRootInCurrentScene()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == mapRootName)
            {
                return roots[i].transform;
            }
        }

        return null;
    }

    private void ClearMapRootChildren()
    {
        if (mapRoot == null)
        {
            return;
        }

        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = mapRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void CaptureBlockMetrics()
    {
        GameObject probePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        if (probePrefab == null)
        {
            blockSizeX = 1f;
            blockSizeY = 1f;
            pivotToTop = 0.5f;
            return;
        }

        GameObject probe = Instantiate(probePrefab);
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

        if (Application.isPlaying)
        {
            Destroy(probe);
        }
        else
        {
            DestroyImmediate(probe);
        }
    }

    private void BuildGround()
    {
        GameObject sandPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject edgePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        int roadTargetZ = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : Mathf.Max(8, mainPyramidZ - 2);

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                bool border = x == -groundHalfWidth || x == groundHalfWidth || z == -groundBackDepth || z == groundFrontDepth;
                bool entryRoad = Mathf.Abs(x) <= 2 && z >= -groundBackDepth && z <= roadTargetZ;
                SpawnBlock(border ? edgePrefab : (entryRoad ? pathPrefab : sandPrefab), x, 0, z, $"Ground_{x}_{z}");
            }
        }
    }

    private void BuildEnemyFrontPlatform()
    {
        if (!buildEnemyFrontPlatform)
        {
            return;
        }

        GameObject basePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject edgePrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int minX = enemyPlatformCenterX - enemyPlatformHalfWidth;
        int maxX = enemyPlatformCenterX + enemyPlatformHalfWidth;
        int maxZ = -groundBackDepth - 1;
        int minZ = maxZ - enemyPlatformForwardLength;

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool border = x == minX || x == maxX || z == minZ || z == maxZ;
                SpawnBlock(border ? edgePrefab : basePrefab, x, enemyPlatformY, z, $"EnemyStage_{x}_{z}");
            }
        }

        // Center stripe helps quickly align enemies in front of the camera.
        for (int z = minZ + 1; z < maxZ; z += 2)
        {
            SpawnBlock(accentPrefab, enemyPlatformCenterX, enemyPlatformY + 1, z, $"EnemyStageGuide_{z}");
        }
    }

    private void BuildCeremonialPath()
    {
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int pathEnd = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : Mathf.Max(8, mainPyramidZ - 2);
        for (int z = 0; z <= pathEnd; z++)
        {
            for (int x = -2; x <= 2; x++)
            {
                SpawnBlock(pathPrefab, x, 1, z, $"Path_{x}_{z}");
            }

            if (z % 4 == 0)
            {
                SpawnBlock(accentPrefab, -4, 1, z, $"PathAccent_L_{z}");
                SpawnBlock(accentPrefab, 4, 1, z, $"PathAccent_R_{z}");
            }
        }
    }

    private void Build2DArtPanel()
    {
        if (!buildPanelBehindArt)
        {
            return;
        }

        GameObject panelPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        int z = artPlaneZ + 1;
        int minY = artBaseY;
        int maxY = artBaseY + artPanelHeight;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = -artPanelHalfWidth; x <= artPanelHalfWidth; x++)
            {
                bool border = x == -artPanelHalfWidth || x == artPanelHalfWidth || y == minY || y == maxY;
                if (!border && ((x + y) & 1) == 0)
                {
                    continue;
                }

                SpawnBlock(panelPrefab, x, y, z, $"Panel_{x}_{y}");
            }
        }
    }

    private void BuildStonehengePixelArt()
    {
        GameObject stonePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject runePrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int z = artPlaneZ;
        int baseY = artBaseY + 2;

        BuildStonePillar(-10, baseY, 6, z, stonePrefab, "OuterL1");
        BuildStonePillar(-7, baseY, 6, z, stonePrefab, "OuterL2");
        BuildStonePillar(-4, baseY, 6, z, stonePrefab, "OuterL3");
        BuildStonePillar(4, baseY, 6, z, stonePrefab, "OuterR1");
        BuildStonePillar(7, baseY, 6, z, stonePrefab, "OuterR2");
        BuildStonePillar(10, baseY, 6, z, stonePrefab, "OuterR3");

        BuildStonePillar(-2, baseY, 8, z, accentPrefab, "InnerL");
        BuildStonePillar(2, baseY, 8, z, accentPrefab, "InnerR");
        BuildStonePillar(0, baseY, 5, z, accentPrefab, "Center");

        BuildLintel(-10, -7, baseY + 6, z, accentPrefab, "LintelL1");
        BuildLintel(-7, -4, baseY + 6, z, accentPrefab, "LintelL2");
        BuildLintel(4, 7, baseY + 6, z, accentPrefab, "LintelR1");
        BuildLintel(7, 10, baseY + 6, z, accentPrefab, "LintelR2");
        BuildLintel(-2, 2, baseY + 8, z, accentPrefab, "LintelCenter");

        SpawnBlock(runePrefab, -2, baseY + 7, z, "RuneL");
        SpawnBlock(runePrefab, 2, baseY + 7, z, "RuneR");
        SpawnBlock(runePrefab, 0, baseY + 9, z, "RuneTop");

        for (int x = -11; x <= 11; x++)
        {
            if ((x & 1) != 0)
            {
                continue;
            }

            SpawnBlock(stonePrefab, x, baseY - 1, z, $"FootStone_{x}");
        }
    }

    private void BuildStonePillar(int x, int baseY, int height, int z, GameObject prefab, string prefix)
    {
        for (int y = baseY; y < baseY + height; y++)
        {
            SpawnBlock(prefab, x, y, z, $"{prefix}_{x}_{y}");
        }
    }

    private void BuildLintel(int xMin, int xMax, int y, int z, GameObject prefab, string prefix)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            SpawnBlock(prefab, x, y, z, $"{prefix}_{x}_{y}");
        }
    }

    private void BuildHillsAndMoon()
    {
        GameObject hillPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject moonPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);

        int z = artPlaneZ;
        int hillBaseY = artBaseY + 1;

        for (int x = -12; x <= -2; x++)
        {
            int height = Mathf.Clamp(4 - Mathf.Abs(x + 7), 1, 4);
            for (int y = hillBaseY; y < hillBaseY + height; y++)
            {
                SpawnBlock(hillPrefab, x, y, z, $"HillL_{x}_{y}");
            }
        }

        for (int x = 2; x <= 12; x++)
        {
            int height = Mathf.Clamp(4 - Mathf.Abs(x - 7), 1, 4);
            for (int y = hillBaseY; y < hillBaseY + height; y++)
            {
                SpawnBlock(hillPrefab, x, y, z, $"HillR_{x}_{y}");
            }
        }

        int moonX = 8;
        int moonY = artBaseY + artPanelHeight - 3;
        SpawnBlock(moonPrefab, moonX, moonY, z, "MoonCore");
        SpawnBlock(moonPrefab, moonX - 1, moonY, z, "MoonL");
        SpawnBlock(moonPrefab, moonX, moonY + 1, z, "MoonTop");
        SpawnBlock(moonPrefab, moonX + 1, moonY - 1, z, "MoonCurve");
    }

    private void BuildFlyingCloudPlatform()
    {
        if (!buildFlyingCloudPlatform)
        {
            ClearStandaloneCloudPlatforms();
            return;
        }

        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        EnsureFigureEightCloudPlatform(
            objectName: cloudSceneObjectName,
            startX: cloudStartX,
            startY: cloudStartY,
            startZ: cloudStartZ,
            assignedPassenger: cloudPassenger,
            reverseDirection: false,
            cloudPrefab: cloudPrefab);

        if (buildSecondCloudPlatform)
        {
            EnsureFigureEightCloudPlatform(
                objectName: cloudSecondSceneObjectName,
                startX: cloudSecondStartX,
                startY: cloudSecondStartY,
                startZ: cloudSecondStartZ,
                assignedPassenger: cloudSecondPassenger,
                reverseDirection: true,
                cloudPrefab: cloudPrefab);
        }
        else
        {
            ClearStandaloneCloudPlatform(cloudSecondSceneObjectName);
        }
    }

    private void EnsureFigureEightCloudPlatform(string objectName, int startX, int startY, int startZ, Transform assignedPassenger, bool reverseDirection, GameObject cloudPrefab)
    {
        if (cloudPrefab == null)
        {
            return;
        }

        Transform existing = FindStandaloneCloudPlatformInCurrentScene(objectName);
        GameObject cloud;
        bool createdNew = false;

        if (existing != null)
        {
            cloud = existing.gameObject;
        }
        else
        {
            createdNew = true;
            cloud = Instantiate(cloudPrefab);
            cloud.transform.position = Vector3.zero;

            if (normalizePrefabScaleToCell)
            {
                Vector3 scaleMultiplier = GetScaleMultiplier(cloudPrefab, cloud);
                cloud.transform.localScale = Vector3.Scale(cloud.transform.localScale, scaleMultiplier);
            }

            cloud.name = objectName;
        }

        if (createdNew || !keepManualCloudTransform)
        {
            AlignBlockToGrid(cloud, startX, startY, startZ);
        }

        EnsureCollider(cloud);

        FigureEightCloudPlatform mover = cloud.GetComponent<FigureEightCloudPlatform>();
        if (mover == null)
        {
            mover = cloud.AddComponent<FigureEightCloudPlatform>();
        }

        mover.Configure(
            radiusX: cloudFigure8RadiusX,
            radiusZ: cloudFigure8RadiusZ,
            cycleDuration: cloudCycleDuration,
            bobAmplitude: cloudBobAmplitude,
            bobFrequency: cloudBobFrequency,
            assignedPassenger: assignedPassenger,
            plane: cloudPathPlane,
            reverse: reverseDirection);
    }

    private void ClearStandaloneCloudPlatforms()
    {
        ClearStandaloneCloudPlatform(cloudSceneObjectName);
        if (!string.IsNullOrWhiteSpace(cloudSecondSceneObjectName) && cloudSecondSceneObjectName != cloudSceneObjectName)
        {
            ClearStandaloneCloudPlatform(cloudSecondSceneObjectName);
        }
    }

    private void ClearStandaloneCloudPlatform(string objectName)
    {
        Transform existing = FindStandaloneCloudPlatformInCurrentScene(objectName);
        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }
    }

    private Transform FindStandaloneCloudPlatformInCurrentScene(string objectName)
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i].transform;
            }
        }

        return null;
    }

    private void BuildPyramids()
    {
        GameObject stonePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject capPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        BuildStepPyramid(0, mainPyramidZ, mainPyramidHalf, mainPyramidHeight, stonePrefab, "MainPyramid");
        SpawnBlock(capPrefab, 0, mainPyramidHeight + 1, mainPyramidZ, "MainPyramid_Cap");

        BuildStepPyramid(-sidePyramidOffsetX, sidePyramidZ, sidePyramidHalf, sidePyramidHeight, stonePrefab, "SidePyramid_L");
        SpawnBlock(capPrefab, -sidePyramidOffsetX, sidePyramidHeight + 1, sidePyramidZ, "SidePyramid_L_Cap");

        BuildStepPyramid(sidePyramidOffsetX, sidePyramidZ, sidePyramidHalf, sidePyramidHeight, stonePrefab, "SidePyramid_R");
        SpawnBlock(capPrefab, sidePyramidOffsetX, sidePyramidHeight + 1, sidePyramidZ, "SidePyramid_R_Cap");
    }

    private void BuildStepPyramid(int cx, int cz, int baseHalf, int height, GameObject prefab, string namePrefix)
    {
        for (int layer = 0; layer < height; layer++)
        {
            int half = baseHalf - layer;
            if (half < 0)
            {
                break;
            }

            int gy = 1 + layer;
            for (int x = cx - half; x <= cx + half; x++)
            {
                for (int z = cz - half; z <= cz + half; z++)
                {
                    bool shell = x == cx - half || x == cx + half || z == cz - half || z == cz + half || layer == height - 1;
                    if (!shell)
                    {
                        continue;
                    }

                    SpawnBlock(prefab, x, gy, z, $"{namePrefix}_{x}_{gy}_{z}");
                }
            }
        }
    }

    private void BuildSphinx()
    {
        GameObject bodyPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject headPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        for (int x = -4; x <= 4; x++)
        {
            for (int z = sphinxZ - 2; z <= sphinxZ + 2; z++)
            {
                SpawnBlock(bodyPrefab, x, 1, z, $"SphinxBody_{x}_{z}");
            }
        }

        for (int x = -3; x <= 3; x++)
        {
            for (int z = sphinxZ - 1; z <= sphinxZ + 1; z++)
            {
                SpawnBlock(bodyPrefab, x, 2, z, $"SphinxBack_{x}_{z}");
            }
        }

        for (int z = sphinxZ - 4; z <= sphinxZ - 3; z++)
        {
            SpawnBlock(bodyPrefab, -2, 1, z, $"SphinxPawL_{z}");
            SpawnBlock(bodyPrefab, -1, 1, z, $"SphinxPawL2_{z}");
            SpawnBlock(bodyPrefab, 1, 1, z, $"SphinxPawR_{z}");
            SpawnBlock(bodyPrefab, 2, 1, z, $"SphinxPawR2_{z}");
        }

        for (int y = 2; y <= 4; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                SpawnBlock(headPrefab, x, y, sphinxZ + 2, $"SphinxHead_{x}_{y}");
            }
        }
    }

    private void BuildObelisks()
    {
        GameObject stonePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject capPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int leftX = -7;
        int rightX = 7;
        int z = mainPyramidZ - 6;
        for (int y = 1; y <= 6; y++)
        {
            SpawnBlock(stonePrefab, leftX, y, z, $"ObeliskL_{y}");
            SpawnBlock(stonePrefab, rightX, y, z, $"ObeliskR_{y}");
        }

        SpawnBlock(capPrefab, leftX, 7, z, "ObeliskL_Cap");
        SpawnBlock(capPrefab, rightX, 7, z, "ObeliskR_Cap");
    }

    private void BuildSunDisc()
    {
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int y = mainPyramidHeight + 5;
        int z = mainPyramidZ + 3;
        SpawnBlock(accentPrefab, 0, y, z, "SunCore");
        SpawnBlock(accentPrefab, -1, y, z, "SunL");
        SpawnBlock(accentPrefab, 1, y, z, "SunR");
        SpawnBlock(accentPrefab, 0, y + 1, z, "SunTop");
        SpawnBlock(accentPrefab, 0, y - 1, z, "SunBottom");
    }

    private void SpawnBlock(GameObject prefab, int gx, int gy, int gz, string objectName)
    {
        if (prefab == null || mapRoot == null)
        {
            return;
        }

        if (preventCellOverlap)
        {
            Vector3Int cell = new Vector3Int(gx, gy, gz);
            if (!occupiedGridCells.Add(cell))
            {
                return;
            }
        }

        GameObject block = Instantiate(prefab, mapRoot);
        block.transform.position = Vector3.zero;

        if (normalizePrefabScaleToCell)
        {
            Vector3 scaleMultiplier = GetScaleMultiplier(prefab, block);
            block.transform.localScale = Vector3.Scale(block.transform.localScale, scaleMultiplier);
        }

        AlignBlockToGrid(block, gx, gy, gz);
        block.name = objectName;

        EnsureCollider(block);
    }

    private void AlignBlockToGrid(GameObject block, int gx, int gy, int gz)
    {
        if (!TryGetWorldBounds(block, out Bounds bounds))
        {
            block.transform.position = GridToWorld(gx, gy, gz);
            return;
        }

        Vector3 mapOffset = GetMapRootWorldOffset();
        float targetCenterX = buildCenter.x + mapOffset.x + (gx * blockSizeX);
        float targetCenterZ = buildCenter.z + mapOffset.z + (gz * blockSizeX);
        float targetTopY = topSurfaceY + mapOffset.y + (gy * blockSizeY);

        Vector3 delta = new Vector3(
            targetCenterX - bounds.center.x,
            targetTopY - bounds.max.y,
            targetCenterZ - bounds.center.z);

        block.transform.position += delta;
    }

    private Vector3 GetScaleMultiplier(GameObject prefab, GameObject instance)
    {
        if (prefabScaleMultiplierCache.TryGetValue(prefab, out Vector3 cached))
        {
            return cached;
        }

        if (!TryGetWorldBounds(instance, out Bounds bounds))
        {
            prefabScaleMultiplierCache[prefab] = Vector3.one;
            return Vector3.one;
        }

        const float epsilon = 0.0001f;
        float mx = bounds.size.x > epsilon ? blockSizeX / bounds.size.x : 1f;
        float my = bounds.size.y > epsilon ? blockSizeY / bounds.size.y : 1f;
        float mz = bounds.size.z > epsilon ? blockSizeX / bounds.size.z : 1f;

        Vector3 multiplier = new Vector3(mx, my, mz);
        prefabScaleMultiplierCache[prefab] = multiplier;
        return multiplier;
    }

    private Vector3 GridToWorld(int gx, int gy, int gz)
    {
        Vector3 mapOffset = GetMapRootWorldOffset();
        float worldX = buildCenter.x + mapOffset.x + (gx * blockSizeX);
        float worldY = (topSurfaceY - pivotToTop) + mapOffset.y + (gy * blockSizeY);
        float worldZ = buildCenter.z + mapOffset.z + (gz * blockSizeX);
        return new Vector3(worldX, worldY, worldZ);
    }

    private Vector3 GetMapRootWorldOffset()
    {
        return mapRoot != null ? mapRoot.position : Vector3.zero;
    }

    private void EnsureCollider(GameObject block)
    {
        if (!addBoxColliderIfMissing || block.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        if (!TryGetWorldBounds(block, out Bounds bounds))
        {
            return;
        }

        BoxCollider box = block.AddComponent<BoxCollider>();
        Vector3 center = block.transform.InverseTransformPoint(bounds.center);
        Vector3 size = bounds.size;

        Vector3 lossyScale = block.transform.lossyScale;
        if (!Mathf.Approximately(lossyScale.x, 0f)) size.x /= Mathf.Abs(lossyScale.x);
        if (!Mathf.Approximately(lossyScale.y, 0f)) size.y /= Mathf.Abs(lossyScale.y);
        if (!Mathf.Approximately(lossyScale.z, 0f)) size.z /= Mathf.Abs(lossyScale.z);

        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
        size.z = Mathf.Max(0.01f, size.z);

        box.center = center;
        box.size = size;
    }

    private static bool TryGetWorldBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
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
