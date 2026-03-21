using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV10LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV10_PetraShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 24;
    [SerializeField] private int groundBackDepth = 3;
    [SerializeField] private int groundFrontDepth = 36;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Petra Mode")]
    [SerializeField] private int artPlaneZ = 25;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 15;
    [SerializeField] private int artPanelHeight = 22;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = true;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;
    [Header("Clock Orbit Platforms (LV10)")]
    [SerializeField] private bool buildClockOrbitPlatforms = true;
    [SerializeField] private bool keepManualClockOrbitTransform = false;
    [SerializeField] private bool keepManualClockOrbitMotionSettings = true;
    [SerializeField] private int clockOrbitCenterX = 0;
    [SerializeField] private int clockOrbitCenterY = 5;
    [SerializeField] private int clockOrbitCenterZ = 12;
    [SerializeField] private float clockOrbitRadius = 3f;
    [SerializeField] private float clockOrbitCycleDuration = 9f;
    [SerializeField] private float clockOrbitDepthBobAmplitude = 0.08f;
    [SerializeField] private float clockOrbitDepthBobFrequency = 1f;
    [SerializeField] private float clockOrbitPhaseOffsetDegrees = -90f;
    [SerializeField] private bool reverseLastThreeBlocks = true;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane clockOrbitPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private string clockOrbitBlockAName = "LV10_ClockOrbit_A";
    [SerializeField] private string clockOrbitBlockBName = "LV10_ClockOrbit_B";
    [SerializeField] private string clockOrbitBlockCName = "LV10_ClockOrbit_C";
    [SerializeField] private string clockOrbitBlockDName = "LV10_ClockOrbit_D";
    [SerializeField] private string clockOrbitBlockEName = "LV10_ClockOrbit_E";
    [SerializeField] private string clockOrbitBlockFName = "LV10_ClockOrbit_F";
    [SerializeField] private Transform clockOrbitPassengerA;
    [SerializeField] private Transform clockOrbitPassengerB;
    [SerializeField] private Transform clockOrbitPassengerC;
    [SerializeField] private Transform clockOrbitPassengerD;
    [SerializeField] private Transform clockOrbitPassengerE;
    [SerializeField] private Transform clockOrbitPassengerF;

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

    [ContextMenu("Build LV10 Petra Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV10LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildApproachPath();
        BuildClockOrbitPlatforms();
        Build2DArtPanel();
        BuildPetraFacadePixelArt();
        BuildDesertCanyonAndSun();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear LV10 Petra Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
        ClearStandaloneClockOrbitPlatforms();
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
        groundFrontDepth = Mathf.Max(14, groundFrontDepth);
        artPlaneZ = Mathf.Clamp(artPlaneZ, 8, groundFrontDepth - 2);
        artBaseY = Mathf.Max(1, artBaseY);
        artPanelHalfWidth = Mathf.Clamp(artPanelHalfWidth, 6, Mathf.Max(6, groundHalfWidth - 2));
        artPanelHeight = Mathf.Max(12, artPanelHeight);
        enemyPlatformHalfWidth = Mathf.Max(4, enemyPlatformHalfWidth);
        enemyPlatformForwardLength = Mathf.Max(2, enemyPlatformForwardLength);
        clockOrbitCenterY = Mathf.Max(1, clockOrbitCenterY);
        clockOrbitCenterZ = Mathf.Clamp(clockOrbitCenterZ, 2, groundFrontDepth - 2);
        clockOrbitRadius = Mathf.Max(0.2f, clockOrbitRadius);
        clockOrbitCycleDuration = Mathf.Max(0.2f, clockOrbitCycleDuration);
        clockOrbitDepthBobAmplitude = Mathf.Max(0f, clockOrbitDepthBobAmplitude);
        clockOrbitDepthBobFrequency = Mathf.Max(0f, clockOrbitDepthBobFrequency);
        clockOrbitPhaseOffsetDegrees = Mathf.Repeat(clockOrbitPhaseOffsetDegrees, 360f);
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
        GameObject groundPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject borderPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        int roadTargetZ = Mathf.Max(8, artPlaneZ - 2);

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                bool border = x == -groundHalfWidth || x == groundHalfWidth || z == -groundBackDepth || z == groundFrontDepth;
                bool road = Mathf.Abs(x) <= 2 && z >= -groundBackDepth && z <= roadTargetZ;
                SpawnBlock(border ? borderPrefab : (road ? pathPrefab : groundPrefab), x, 0, z, $"Ground_{x}_{z}");
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

    private void BuildApproachPath()
    {
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int pathEnd = Mathf.Max(8, artPlaneZ - 2);

        for (int z = 0; z <= pathEnd; z++)
        {
            for (int x = -2; x <= 2; x++)
            {
                SpawnBlock(pathPrefab, x, 1, z, $"Path_{x}_{z}");
            }

            if (z % 3 == 0)
            {
                SpawnBlock(accentPrefab, -4, 1, z, $"PathAccentL_{z}");
                SpawnBlock(accentPrefab, 4, 1, z, $"PathAccentR_{z}");
            }
        }
    }

    private void BuildClockOrbitPlatforms()
    {
        if (!buildClockOrbitPlatforms)
        {
            ClearStandaloneClockOrbitPlatforms();
            return;
        }

        GameObject platformPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        if (platformPrefab == null)
        {
            return;
        }

        EnsureClockOrbitPlatform(clockOrbitBlockAName, 0, clockOrbitPassengerA, platformPrefab);
        EnsureClockOrbitPlatform(clockOrbitBlockBName, 1, clockOrbitPassengerB, platformPrefab);
        EnsureClockOrbitPlatform(clockOrbitBlockCName, 2, clockOrbitPassengerC, platformPrefab);
        EnsureClockOrbitPlatform(clockOrbitBlockDName, 3, clockOrbitPassengerD, platformPrefab);
        EnsureClockOrbitPlatform(clockOrbitBlockEName, 4, clockOrbitPassengerE, platformPrefab);
        EnsureClockOrbitPlatform(clockOrbitBlockFName, 5, clockOrbitPassengerF, platformPrefab);
    }

    private void EnsureClockOrbitPlatform(
        string objectName,
        int index,
        Transform assignedPassenger,
        GameObject platformPrefab)
    {
        if (platformPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneClockOrbitObjectInCurrentScene(objectName);
        GameObject platform;
        bool createdNew = false;

        if (existing != null)
        {
            platform = existing.gameObject;
        }
        else
        {
            createdNew = true;
            platform = Instantiate(platformPrefab);
            platform.transform.position = Vector3.zero;

            if (normalizePrefabScaleToCell)
            {
                Vector3 scaleMultiplier = GetScaleMultiplier(platformPrefab, platform);
                platform.transform.localScale = Vector3.Scale(platform.transform.localScale, scaleMultiplier);
            }

            platform.name = objectName;
        }

        if (createdNew || !keepManualClockOrbitTransform)
        {
            AlignBlockToGrid(platform, clockOrbitCenterX, clockOrbitCenterY, clockOrbitCenterZ);
        }

        EnsureCollider(platform);
        RemoveComponentIfPresent<RadialShuttleCloudPlatform>(platform);
        RemoveComponentIfPresent<VerticalWaveCloudPlatform>(platform);

        FigureEightCloudPlatform mover = platform.GetComponent<FigureEightCloudPlatform>();
        bool createdMover = false;
        if (mover == null)
        {
            mover = platform.AddComponent<FigureEightCloudPlatform>();
            createdMover = true;
        }

        bool shouldApplyBuilderMotion = createdNew || createdMover || !keepManualClockOrbitMotionSettings;
        if (shouldApplyBuilderMotion)
        {
            bool reverse = reverseLastThreeBlocks && index >= 3;
            float phaseDegrees;
            if (reverseLastThreeBlocks)
            {
                float groupPhase = (index % 3) * 120f;
                phaseDegrees = clockOrbitPhaseOffsetDegrees + groupPhase + (reverse ? 180f : 0f);
            }
            else
            {
                phaseDegrees = clockOrbitPhaseOffsetDegrees + (index * 60f);
            }

            phaseDegrees = Mathf.Repeat(phaseDegrees, 360f);
            mover.Configure(
                radiusX: clockOrbitRadius,
                radiusZ: clockOrbitRadius,
                cycleDuration: clockOrbitCycleDuration,
                bobAmplitude: clockOrbitDepthBobAmplitude,
                bobFrequency: clockOrbitDepthBobFrequency,
                assignedPassenger: assignedPassenger,
                plane: clockOrbitPlane,
                reverse: reverse,
                shape: FigureEightCloudPlatform.PathShape.Circle,
                phaseDegrees: phaseDegrees,
                flipIntervalSeconds: 0f);
        }
    }

    private void ClearStandaloneClockOrbitPlatforms()
    {
        ClearStandaloneClockOrbitPlatform(clockOrbitBlockAName);
        ClearStandaloneClockOrbitPlatform(clockOrbitBlockBName);
        ClearStandaloneClockOrbitPlatform(clockOrbitBlockCName);
        ClearStandaloneClockOrbitPlatform(clockOrbitBlockDName);
        ClearStandaloneClockOrbitPlatform(clockOrbitBlockEName);
        ClearStandaloneClockOrbitPlatform(clockOrbitBlockFName);
    }

    private void ClearStandaloneClockOrbitPlatform(string objectName)
    {
        Transform existing = FindStandaloneClockOrbitObjectInCurrentScene(objectName);
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

    private Transform FindStandaloneClockOrbitObjectInCurrentScene(string objectName)
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

    private void RemoveComponentIfPresent<T>(GameObject target) where T : Component
    {
        if (target == null)
        {
            return;
        }

        T component = target.GetComponent<T>();
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
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

    private void BuildPetraFacadePixelArt()
    {
        GameObject cliffPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject sandstonePrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);

        int z = artPlaneZ;
        int baseY = artBaseY + 2;

        for (int x = -17; x <= 17; x++)
        {
            SpawnBlock(sandstonePrefab, x, baseY, z, $"Terrace_{x}");
            if (Mathf.Abs(x) <= 15)
            {
                SpawnBlock(sandstonePrefab, x, baseY + 1, z, $"TerraceTop_{x}");
            }
        }

        for (int row = 0; row <= 12; row++)
        {
            int gy = baseY + 2 + row;
            int canyonInset = Mathf.Min(4, row / 3);
            int leftEdge = -18 + canyonInset;
            int rightEdge = 18 - canyonInset;

            for (int x = -18; x <= leftEdge; x++)
            {
                if (x == leftEdge || ((x + gy) & 1) == 0)
                {
                    SpawnBlock(cliffPrefab, x, gy, z, $"CanyonL_{x}_{gy}");
                }
            }

            for (int x = rightEdge; x <= 18; x++)
            {
                if (x == rightEdge || ((x + gy) & 1) == 0)
                {
                    SpawnBlock(cliffPrefab, x, gy, z, $"CanyonR_{x}_{gy}");
                }
            }
        }

        int facadeMinX = -12;
        int facadeMaxX = 12;
        int facadeMinY = baseY + 2;
        int facadeMaxY = baseY + 12;

        for (int y = facadeMinY; y <= facadeMaxY; y++)
        {
            for (int x = facadeMinX; x <= facadeMaxX; x++)
            {
                bool edge = x == facadeMinX || x == facadeMaxX || y == facadeMinY || y == facadeMaxY;
                bool colonnade = x == -9 || x == -6 || x == -3 || x == 3 || x == 6 || x == 9;
                bool beam = y == facadeMinY + 4 || y == facadeMinY + 8;
                bool doorway = Mathf.Abs(x) <= 2 && y <= facadeMinY + 5;
                bool innerVoid = Mathf.Abs(x) <= 1 && y >= facadeMinY + 9 && y <= facadeMaxY - 1;

                if ((edge || colonnade || beam) && !doorway && !innerVoid)
                {
                    SpawnBlock(sandstonePrefab, x, y, z, $"Facade_{x}_{y}");
                }
            }
        }

        for (int y = facadeMinY; y <= facadeMinY + 5; y++)
        {
            SpawnBlock(accentPrefab, -3, y, z, $"DoorFrameL_{y}");
            SpawnBlock(accentPrefab, 3, y, z, $"DoorFrameR_{y}");
        }

        for (int x = -3; x <= 3; x++)
        {
            SpawnBlock(accentPrefab, x, facadeMinY + 6, z, $"DoorLintel_{x}");
        }

        int pedimentBaseY = facadeMaxY + 1;
        for (int layer = 0; layer < 5; layer++)
        {
            int gy = pedimentBaseY + layer;
            int half = 12 - (layer * 2);
            for (int x = -half; x <= half; x++)
            {
                bool edge = x == -half || x == half || layer == 4;
                bool fill = layer == 0 && ((x & 1) == 0);
                if (edge || fill)
                {
                    SpawnBlock(sandstonePrefab, x, gy, z, $"Pediment_{x}_{gy}");
                }
            }
        }

        int tholosBaseY = pedimentBaseY + 5;
        for (int layer = 0; layer < 5; layer++)
        {
            int gy = tholosBaseY + layer;
            int half = 5 - layer;
            for (int x = -half; x <= half; x++)
            {
                bool shell = x == -half || x == half || layer == 4 || layer == 0;
                bool pillars = layer <= 2 && (x == -2 || x == 0 || x == 2);
                if (shell || pillars)
                {
                    SpawnBlock(cliffPrefab, x, gy, z, $"Tholos_{x}_{gy}");
                }
            }
        }

        SpawnBlock(accentPrefab, 0, tholosBaseY + 5, z, "TopUrn");
        SpawnBlock(accentPrefab, -1, tholosBaseY + 4, z, "TopUrnL");
        SpawnBlock(accentPrefab, 1, tholosBaseY + 4, z, "TopUrnR");

        for (int step = 0; step < 4; step++)
        {
            int gy = baseY - step;
            int half = 4 + step;
            for (int x = -half; x <= half; x++)
            {
                SpawnBlock(sandstonePrefab, x, gy, z, $"Stair_{step}_{x}");
            }
        }
    }

    private void BuildDesertCanyonAndSun()
    {
        GameObject sandPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;

        for (int x = -19; x <= 19; x++)
        {
            if ((x & 1) == 0)
            {
                SpawnBlock(sandPrefab, x, artBaseY + 1, z, $"Dune_{x}");
            }
        }

        for (int x = -14; x <= 14; x += 4)
        {
            SpawnBlock(accentPrefab, x, artBaseY + 2, z, $"Torch_{x}");
        }

        int sunY = artBaseY + artPanelHeight - 4;
        SpawnBlock(accentPrefab, 12, sunY, z, "SunCore");
        SpawnBlock(accentPrefab, 11, sunY, z, "SunL");
        SpawnBlock(accentPrefab, 13, sunY, z, "SunR");
        SpawnBlock(accentPrefab, 12, sunY + 1, z, "SunTop");
        SpawnBlock(accentPrefab, 12, sunY - 1, z, "SunBottom");
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

