using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV5LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV5_GreatWallShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 24;
    [SerializeField] private int groundBackDepth = 3;
    [SerializeField] private int groundFrontDepth = 46;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Pixel Art Mode")]
    [SerializeField] private bool use2DPixelArtMode = true;
    [SerializeField] private int artPlaneZ = 24;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 12;
    [SerializeField] private int artPanelHeight = 20;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = true;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;

    [Header("Flying Cloud Platforms (3 Lanes)")]
    [SerializeField] private bool buildFlyingCloudPlatforms = true;
    [SerializeField] private bool keepManualCloudTransform = true;
    [SerializeField] private bool keepManualCloudMotionSettings = true;
    [SerializeField] private string cloudAName = "LV5_Cloud_A";
    [SerializeField] private string cloudBName = "LV5_Cloud_B";
    [SerializeField] private string cloudCName = "LV5_Cloud_C";
    [SerializeField] private int cloudAStartX = -8;
    [SerializeField] private int cloudAStartY = 3;
    [SerializeField] private int cloudAStartZ = 8;
    [SerializeField] private int cloudBStartX = 0;
    [SerializeField] private int cloudBStartY = 3;
    [SerializeField] private int cloudBStartZ = 8;
    [SerializeField] private int cloudCStartX = 8;
    [SerializeField] private int cloudCStartY = 3;
    [SerializeField] private int cloudCStartZ = 8;
    [SerializeField] private float cloudARadiusX = 2.4f;
    [SerializeField] private float cloudARadiusMinor = 1.2f;
    [SerializeField] private float cloudADuration = 6f;
    [SerializeField] private bool cloudAReverse = false;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane cloudAPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private Transform cloudAPassenger;
    [SerializeField] private float cloudBRadiusX = 2.4f;
    [SerializeField] private float cloudBRadiusMinor = 1.2f;
    [SerializeField] private float cloudBDuration = 5.2f;
    [SerializeField] private bool cloudBReverse = true;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane cloudBPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private Transform cloudBPassenger;
    [SerializeField] private float cloudCRadiusX = 1.8f;
    [SerializeField] private float cloudCRadiusMinor = 1f;
    [SerializeField] private float cloudCDuration = 4.4f;
    [SerializeField] private bool cloudCReverse = false;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane cloudCPlane = FigureEightCloudPlatform.FigureEightPlane.SideYZ;
    [SerializeField] private Transform cloudCPassenger;
    [SerializeField] private float cloudBobAmplitude = 0.2f;
    [SerializeField] private float cloudBobFrequency = 1.1f;
    [SerializeField] private bool useFerrisWheelOrbit = true;
    [SerializeField] private int cloudWheelCenterX = 0;
    [SerializeField] private int cloudWheelCenterY = 4;
    [SerializeField] private int cloudWheelCenterZ = 10;
    [SerializeField] private float cloudWheelRadius = 4f;
    [SerializeField] private float cloudWheelDuration = 6f;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane cloudWheelPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private bool cloudWheelReverse = false;
    [SerializeField] private float cloudAPhaseDegrees = 0f;
    [SerializeField] private float cloudBPhaseDegrees = 120f;
    [SerializeField] private float cloudCPhaseDegrees = 240f;

    [Header("Great Wall Landmark")]
    [SerializeField] private int wallStartZ = 7;
    [SerializeField] private int wallEndZ = 38;
    [SerializeField] private int wallHalfWidth = 2;
    [SerializeField] private int wallHeight = 4;
    [SerializeField] private int curveAmplitude = 8;
    [SerializeField] private int curvePeriod = 14;
    [SerializeField] private int watchtowerSpacing = 8;
    [SerializeField] private int watchtowerHalf = 2;
    [SerializeField] private int watchtowerHeight = 8;
    [SerializeField] private int watchtowerRoofHeight = 2;
    [SerializeField] private int sideMountainHeight = 6;
    [SerializeField] private int beaconSpacing = 6;

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

    [ContextMenu("Build LV5 Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV5LandmarkBuilder: Could not resolve any usable block prefab.");
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

        BuildGround();
        BuildEnemyFrontPlatform();
        BuildEntranceRoad();
        BuildFlyingCloudPlatforms();

        if (use2DPixelArtMode)
        {
            Build2DArtPanel();
            BuildEiffelTowerPixelArt();
            Build2DSkyAccents();
        }
        else
        {
            BuildGreatWall();
            BuildGatehouse();
            BuildWatchtowers();
            BuildSignalBeacons();
            BuildMountainBackdrop();
            BuildSunDisc();
        }

        Physics.SyncTransforms();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear LV5 Landmark")]
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
        groundHalfWidth = Mathf.Max(10, groundHalfWidth);
        groundBackDepth = Mathf.Max(0, groundBackDepth);
        groundFrontDepth = Mathf.Max(18, groundFrontDepth);
        artPlaneZ = Mathf.Clamp(artPlaneZ, 8, groundFrontDepth - 2);
        artBaseY = Mathf.Max(1, artBaseY);
        artPanelHalfWidth = Mathf.Clamp(artPanelHalfWidth, 6, Mathf.Max(6, groundHalfWidth - 2));
        artPanelHeight = Mathf.Max(8, artPanelHeight);
        enemyPlatformHalfWidth = Mathf.Max(4, enemyPlatformHalfWidth);
        enemyPlatformForwardLength = Mathf.Max(2, enemyPlatformForwardLength);
        cloudAStartY = Mathf.Max(1, cloudAStartY);
        cloudBStartY = Mathf.Max(1, cloudBStartY);
        cloudCStartY = Mathf.Max(1, cloudCStartY);
        cloudARadiusX = Mathf.Max(0.1f, cloudARadiusX);
        cloudARadiusMinor = Mathf.Max(0.1f, cloudARadiusMinor);
        cloudADuration = Mathf.Max(0.2f, cloudADuration);
        cloudBRadiusX = Mathf.Max(0.1f, cloudBRadiusX);
        cloudBRadiusMinor = Mathf.Max(0.1f, cloudBRadiusMinor);
        cloudBDuration = Mathf.Max(0.2f, cloudBDuration);
        cloudCRadiusX = Mathf.Max(0.1f, cloudCRadiusX);
        cloudCRadiusMinor = Mathf.Max(0.1f, cloudCRadiusMinor);
        cloudCDuration = Mathf.Max(0.2f, cloudCDuration);
        cloudBobAmplitude = Mathf.Max(0f, cloudBobAmplitude);
        cloudBobFrequency = Mathf.Max(0f, cloudBobFrequency);
        cloudWheelCenterY = Mathf.Max(1, cloudWheelCenterY);
        cloudWheelRadius = Mathf.Max(0.1f, cloudWheelRadius);
        cloudWheelDuration = Mathf.Max(0.2f, cloudWheelDuration);
        cloudAPhaseDegrees = Mathf.Repeat(cloudAPhaseDegrees, 360f);
        cloudBPhaseDegrees = Mathf.Repeat(cloudBPhaseDegrees, 360f);
        cloudCPhaseDegrees = Mathf.Repeat(cloudCPhaseDegrees, 360f);
        if (string.IsNullOrWhiteSpace(cloudAName))
        {
            cloudAName = "LV5_Cloud_A";
        }
        if (string.IsNullOrWhiteSpace(cloudBName))
        {
            cloudBName = "LV5_Cloud_B";
        }
        if (string.IsNullOrWhiteSpace(cloudCName))
        {
            cloudCName = "LV5_Cloud_C";
        }
        if (cloudBName == cloudAName)
        {
            cloudBName = cloudAName + "_B";
        }
        if (cloudCName == cloudAName || cloudCName == cloudBName)
        {
            cloudCName = "LV5_Cloud_C";
        }
        wallStartZ = Mathf.Clamp(wallStartZ, 4, groundFrontDepth - 12);
        wallEndZ = Mathf.Clamp(wallEndZ, wallStartZ + 10, groundFrontDepth - 2);
        wallHalfWidth = Mathf.Clamp(wallHalfWidth, 2, 4);
        wallHeight = Mathf.Max(3, wallHeight);
        curveAmplitude = Mathf.Clamp(curveAmplitude, 0, Mathf.Max(2, groundHalfWidth - (wallHalfWidth + 4)));
        curvePeriod = Mathf.Max(6, curvePeriod);
        watchtowerSpacing = Mathf.Max(4, watchtowerSpacing);
        watchtowerHalf = Mathf.Clamp(watchtowerHalf, 2, 4);
        watchtowerHeight = Mathf.Max(5, watchtowerHeight);
        watchtowerRoofHeight = Mathf.Clamp(watchtowerRoofHeight, 1, 4);
        sideMountainHeight = Mathf.Max(3, sideMountainHeight);
        beaconSpacing = Mathf.Max(4, beaconSpacing);
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
        GameObject soilPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject borderPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        int roadTargetZ = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : (wallStartZ + 1);

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                bool border = x == -groundHalfWidth || x == groundHalfWidth || z == -groundBackDepth || z == groundFrontDepth;
                bool entryRoad = Mathf.Abs(x) <= 2 && z >= -groundBackDepth && z <= roadTargetZ;
                GameObject prefab = border ? borderPrefab : (entryRoad ? pathPrefab : soilPrefab);
                SpawnBlock(prefab, x, 0, z, $"Ground_{x}_{z}");
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

        for (int z = minZ + 1; z < maxZ; z += 2)
        {
            SpawnBlock(accentPrefab, enemyPlatformCenterX, enemyPlatformY + 1, z, $"EnemyGuide_{z}");
        }
    }

    private void BuildEntranceRoad()
    {
        GameObject roadPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int roadEnd = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : (wallStartZ + 1);
        for (int z = 0; z <= roadEnd; z++)
        {
            for (int x = -2; x <= 2; x++)
            {
                SpawnBlock(roadPrefab, x, 1, z, $"Road_{x}_{z}");
            }

            if (z % 3 == 0)
            {
                SpawnBlock(accentPrefab, -3, 1, z, $"RoadAccentL_{z}");
                SpawnBlock(accentPrefab, 3, 1, z, $"RoadAccentR_{z}");
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
                if (!border && (x + y) % 2 != 0)
                {
                    continue;
                }

                SpawnBlock(panelPrefab, x, y, z, $"Panel_{x}_{y}");
            }
        }
    }

    private void BuildEiffelTowerPixelArt()
    {
        GameObject bodyPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int z = artPlaneZ;

        string[] rows =
        {
            ".......#.......",
            "......###......",
            "......###......",
            ".....##+##.....",
            ".....##+##.....",
            "....##...##....",
            "....##...##....",
            "...#########...",
            "...##+++++##...",
            "..###.....###..",
            "..###.....###..",
            ".###.......###.",
            ".###.......###.",
            "###.........###",
            "###.........###",
            "###############",
            "######...######",
            "######...######"
        };

        int centerX = 0;
        int topY = artBaseY + artPanelHeight - 2;

        for (int row = 0; row < rows.Length; row++)
        {
            string line = rows[row];
            int gy = topY - row;
            int centerIndex = line.Length / 2;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '.')
                {
                    continue;
                }

                int gx = centerX + (i - centerIndex);
                GameObject prefab = c == '+' ? accentPrefab : bodyPrefab;
                SpawnBlock(prefab, gx, gy, z, $"Eiffel2D_{gx}_{gy}");
            }
        }
    }

    private void Build2DSkyAccents()
    {
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int z = artPlaneZ;
        int sunY = artBaseY + artPanelHeight - 4;

        SpawnBlock(accentPrefab, 8, sunY, z, "SunCore");
        SpawnBlock(accentPrefab, 7, sunY, z, "SunL");
        SpawnBlock(accentPrefab, 9, sunY, z, "SunR");
        SpawnBlock(accentPrefab, 8, sunY + 1, z, "SunTop");
        SpawnBlock(accentPrefab, 8, sunY - 1, z, "SunBottom");
    }

    private void BuildFlyingCloudPlatforms()
    {
        if (!buildFlyingCloudPlatforms)
        {
            ClearStandaloneCloudPlatforms();
            return;
        }

        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);

        if (useFerrisWheelOrbit)
        {
            EnsureFigureEightCloudPlatform(
                objectName: cloudAName,
                startX: cloudWheelCenterX,
                startY: cloudWheelCenterY,
                startZ: cloudWheelCenterZ,
                radiusX: cloudWheelRadius,
                radiusMinor: cloudWheelRadius,
                duration: cloudWheelDuration,
                reverseDirection: cloudWheelReverse,
                plane: cloudWheelPlane,
                assignedPassenger: cloudAPassenger,
                cloudPrefab: cloudPrefab,
                shape: FigureEightCloudPlatform.PathShape.Circle,
                phaseDegrees: cloudAPhaseDegrees);

            EnsureFigureEightCloudPlatform(
                objectName: cloudBName,
                startX: cloudWheelCenterX,
                startY: cloudWheelCenterY,
                startZ: cloudWheelCenterZ,
                radiusX: cloudWheelRadius,
                radiusMinor: cloudWheelRadius,
                duration: cloudWheelDuration,
                reverseDirection: cloudWheelReverse,
                plane: cloudWheelPlane,
                assignedPassenger: cloudBPassenger,
                cloudPrefab: cloudPrefab,
                shape: FigureEightCloudPlatform.PathShape.Circle,
                phaseDegrees: cloudBPhaseDegrees);

            EnsureFigureEightCloudPlatform(
                objectName: cloudCName,
                startX: cloudWheelCenterX,
                startY: cloudWheelCenterY,
                startZ: cloudWheelCenterZ,
                radiusX: cloudWheelRadius,
                radiusMinor: cloudWheelRadius,
                duration: cloudWheelDuration,
                reverseDirection: cloudWheelReverse,
                plane: cloudWheelPlane,
                assignedPassenger: cloudCPassenger,
                cloudPrefab: cloudPrefab,
                shape: FigureEightCloudPlatform.PathShape.Circle,
                phaseDegrees: cloudCPhaseDegrees);
        }
        else
        {
            EnsureFigureEightCloudPlatform(
                objectName: cloudAName,
                startX: cloudAStartX,
                startY: cloudAStartY,
                startZ: cloudAStartZ,
                radiusX: cloudARadiusX,
                radiusMinor: cloudARadiusMinor,
                duration: cloudADuration,
                reverseDirection: cloudAReverse,
                plane: cloudAPlane,
                assignedPassenger: cloudAPassenger,
                cloudPrefab: cloudPrefab,
                shape: FigureEightCloudPlatform.PathShape.Circle);

            EnsureFigureEightCloudPlatform(
                objectName: cloudBName,
                startX: cloudBStartX,
                startY: cloudBStartY,
                startZ: cloudBStartZ,
                radiusX: cloudBRadiusX,
                radiusMinor: cloudBRadiusMinor,
                duration: cloudBDuration,
                reverseDirection: cloudBReverse,
                plane: cloudBPlane,
                assignedPassenger: cloudBPassenger,
                cloudPrefab: cloudPrefab,
                shape: FigureEightCloudPlatform.PathShape.Circle);

            EnsureFigureEightCloudPlatform(
                objectName: cloudCName,
                startX: cloudCStartX,
                startY: cloudCStartY,
                startZ: cloudCStartZ,
                radiusX: cloudCRadiusX,
                radiusMinor: cloudCRadiusMinor,
                duration: cloudCDuration,
                reverseDirection: cloudCReverse,
                plane: cloudCPlane,
                assignedPassenger: cloudCPassenger,
                cloudPrefab: cloudPrefab,
                shape: FigureEightCloudPlatform.PathShape.Circle);
        }
    }

    private void EnsureFigureEightCloudPlatform(
        string objectName,
        int startX,
        int startY,
        int startZ,
        float radiusX,
        float radiusMinor,
        float duration,
        bool reverseDirection,
        FigureEightCloudPlatform.FigureEightPlane plane,
        Transform assignedPassenger,
        GameObject cloudPrefab,
        FigureEightCloudPlatform.PathShape shape = FigureEightCloudPlatform.PathShape.Circle,
        float phaseDegrees = 0f,
        bool forceRelayout = false)
    {
        if (cloudPrefab == null || string.IsNullOrWhiteSpace(objectName))
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

        if (createdNew || forceRelayout || !keepManualCloudTransform)
        {
            AlignBlockToGrid(cloud, startX, startY, startZ);
        }

        EnsureCollider(cloud);

        FigureEightCloudPlatform mover = cloud.GetComponent<FigureEightCloudPlatform>();
        bool createdMover = false;
        if (mover == null)
        {
            mover = cloud.AddComponent<FigureEightCloudPlatform>();
            createdMover = true;
        }

        bool shouldApplyBuilderMotion = createdNew || createdMover || !keepManualCloudMotionSettings;
        if (shouldApplyBuilderMotion)
        {
            mover.Configure(
                radiusX: radiusX,
                radiusZ: radiusMinor,
                cycleDuration: duration,
                bobAmplitude: cloudBobAmplitude,
                bobFrequency: cloudBobFrequency,
                assignedPassenger: assignedPassenger,
                plane: plane,
                reverse: reverseDirection,
                shape: shape,
                phaseDegrees: phaseDegrees);
        }
    }

    private void ClearStandaloneCloudPlatforms()
    {
        ClearStandaloneCloudPlatform(cloudAName);
        ClearStandaloneCloudPlatform(cloudBName);
        ClearStandaloneCloudPlatform(cloudCName);
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
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

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

    private void BuildGreatWall()
    {
        GameObject wallPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject walkwayPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        int gateCenterZ = wallStartZ + 1;

        for (int z = wallStartZ; z <= wallEndZ; z++)
        {
            int centerX = GetWallCenterX(z);
            int outerHalf = wallHalfWidth + 1;

            for (int offset = -outerHalf; offset <= outerHalf; offset++)
            {
                SpawnBlock(wallPrefab, centerX + offset, 1, z, $"WallBase_{centerX + offset}_{z}");
            }

            for (int y = 2; y <= wallHeight; y++)
            {
                for (int offset = -wallHalfWidth; offset <= wallHalfWidth; offset++)
                {
                    if (IsGateOpening(z, gateCenterZ, offset, y))
                    {
                        continue;
                    }

                    SpawnBlock(wallPrefab, centerX + offset, y, z, $"WallCore_{centerX + offset}_{y}_{z}");
                }
            }

            for (int offset = -(wallHalfWidth - 1); offset <= wallHalfWidth - 1; offset++)
            {
                SpawnBlock(walkwayPrefab, centerX + offset, wallHeight + 1, z, $"Walkway_{centerX + offset}_{z}");
            }

            int parapetY = wallHeight + 2;
            SpawnBlock(wallPrefab, centerX - wallHalfWidth, parapetY, z, $"ParapetL_{z}");
            SpawnBlock(wallPrefab, centerX + wallHalfWidth, parapetY, z, $"ParapetR_{z}");

            if ((z & 1) == 0)
            {
                SpawnBlock(wallPrefab, centerX - wallHalfWidth, parapetY + 1, z, $"ParapetTopL_{z}");
                SpawnBlock(wallPrefab, centerX + wallHalfWidth, parapetY + 1, z, $"ParapetTopR_{z}");
            }
        }
    }

    private void BuildGatehouse()
    {
        GameObject wallPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int gateZ = wallStartZ + 1;
        int centerX = GetWallCenterX(gateZ);
        int outerHalf = wallHalfWidth + 3;
        int gateTopY = wallHeight + 6;

        for (int y = 2; y <= gateTopY; y++)
        {
            for (int z = gateZ - 1; z <= gateZ + 1; z++)
            {
                for (int x = centerX - outerHalf; x <= centerX + outerHalf; x++)
                {
                    bool outerFrame = x == centerX - outerHalf || x == centerX + outerHalf || y == gateTopY;
                    bool lowerBand = y <= 3;
                    bool buildCell = outerFrame || lowerBand;
                    bool doorway = y <= 4 && Mathf.Abs(x - centerX) <= 1;
                    if (!buildCell || doorway)
                    {
                        continue;
                    }

                    SpawnBlock(wallPrefab, x, y, z, $"Gate_{x}_{y}_{z}");
                }
            }
        }

        for (int x = centerX - 2; x <= centerX + 2; x++)
        {
            SpawnBlock(accentPrefab, x, gateTopY + 1, gateZ, $"GateRoof_{x}");
        }
    }

    private void BuildWatchtowers()
    {
        int start = wallStartZ + Mathf.Max(2, watchtowerSpacing / 2);
        int index = 0;
        for (int z = start; z <= wallEndZ; z += watchtowerSpacing)
        {
            BuildWatchtower(index, z);
            index++;
        }
    }

    private void BuildWatchtower(int towerIndex, int centerZ)
    {
        GameObject wallPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject roofPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int centerX = GetWallCenterX(centerZ);
        int floorY = wallHeight + 1;
        int topY = floorY + watchtowerHeight;

        for (int y = floorY; y <= topY; y++)
        {
            for (int x = centerX - watchtowerHalf; x <= centerX + watchtowerHalf; x++)
            {
                for (int z = centerZ - watchtowerHalf; z <= centerZ + watchtowerHalf; z++)
                {
                    bool shell = x == centerX - watchtowerHalf || x == centerX + watchtowerHalf || z == centerZ - watchtowerHalf || z == centerZ + watchtowerHalf;
                    bool roofDeck = y == topY;
                    bool doorway = z == centerZ - watchtowerHalf && x == centerX && y <= floorY + 1;
                    bool arrowSlit = y == floorY + 3 && (x == centerX || z == centerZ);

                    if ((!shell && !roofDeck) || doorway || arrowSlit)
                    {
                        continue;
                    }

                    SpawnBlock(wallPrefab, x, y, z, $"Watchtower_{towerIndex}_{x}_{y}_{z}");
                }
            }
        }

        for (int layer = 0; layer < watchtowerRoofHeight; layer++)
        {
            int roofHalf = Mathf.Max(0, watchtowerHalf - layer);
            int gy = topY + 1 + layer;

            for (int x = centerX - roofHalf; x <= centerX + roofHalf; x++)
            {
                for (int z = centerZ - roofHalf; z <= centerZ + roofHalf; z++)
                {
                    bool shell = x == centerX - roofHalf || x == centerX + roofHalf || z == centerZ - roofHalf || z == centerZ + roofHalf;
                    if (!shell && layer < watchtowerRoofHeight - 1)
                    {
                        continue;
                    }

                    SpawnBlock(roofPrefab, x, gy, z, $"WatchRoof_{towerIndex}_{x}_{gy}_{z}");
                }
            }
        }
    }

    private void BuildSignalBeacons()
    {
        GameObject beaconPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        for (int z = wallStartZ + 3; z <= wallEndZ; z += beaconSpacing)
        {
            int centerX = GetWallCenterX(z);
            int y = wallHeight + 3;
            SpawnBlock(beaconPrefab, centerX - wallHalfWidth, y, z, $"BeaconL_{z}");
            SpawnBlock(beaconPrefab, centerX + wallHalfWidth, y, z, $"BeaconR_{z}");
            SpawnBlock(beaconPrefab, centerX - wallHalfWidth, y + 1, z, $"BeaconFlameL_{z}");
            SpawnBlock(beaconPrefab, centerX + wallHalfWidth, y + 1, z, $"BeaconFlameR_{z}");
        }
    }

    private void BuildMountainBackdrop()
    {
        GameObject mountainPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);

        for (int side = -1; side <= 1; side += 2)
        {
            int edgeX = side * (groundHalfWidth - 2);
            for (int z = wallStartZ - 2; z <= groundFrontDepth; z++)
            {
                float wave = Mathf.Abs(Mathf.Sin((z + (side * 3)) * 0.35f));
                int height = 2 + Mathf.RoundToInt(wave * sideMountainHeight);

                for (int y = 1; y <= height; y++)
                {
                    int inwardDepth = Mathf.Min(3, y / 2);
                    for (int step = 0; step <= inwardDepth; step++)
                    {
                        int gx = edgeX - (side * step);
                        SpawnBlock(mountainPrefab, gx, y, z, $"Mountain_{side}_{gx}_{y}_{z}");
                    }
                }
            }
        }
    }

    private void BuildSunDisc()
    {
        GameObject sunPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int coreX = GetWallCenterX(wallEndZ);
        int coreY = wallHeight + watchtowerHeight + 6;
        int coreZ = wallEndZ + 3;

        SpawnBlock(sunPrefab, coreX, coreY, coreZ, "SunCore");
        SpawnBlock(sunPrefab, coreX - 1, coreY, coreZ, "SunLeft");
        SpawnBlock(sunPrefab, coreX + 1, coreY, coreZ, "SunRight");
        SpawnBlock(sunPrefab, coreX, coreY + 1, coreZ, "SunTop");
        SpawnBlock(sunPrefab, coreX, coreY - 1, coreZ, "SunBottom");
    }

    private bool IsGateOpening(int z, int gateCenterZ, int offsetFromCenter, int y)
    {
        bool inZ = Mathf.Abs(z - gateCenterZ) <= 1;
        bool inX = Mathf.Abs(offsetFromCenter) <= 1;
        bool inY = y <= 4;
        return inZ && inX && inY;
    }

    private int GetWallCenterX(int z)
    {
        if (curveAmplitude <= 0)
        {
            return 0;
        }

        float cycles = (z - wallStartZ) / Mathf.Max(1f, curvePeriod);
        float radians = cycles * Mathf.PI * 2f;
        return Mathf.RoundToInt(Mathf.Sin(radians) * curveAmplitude);
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
        box.center = block.transform.InverseTransformPoint(bounds.center);
        box.size = block.transform.InverseTransformVector(bounds.size);
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
