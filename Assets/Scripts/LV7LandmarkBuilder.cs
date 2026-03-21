using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV7LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV7_GoldenGateShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 24;
    [SerializeField] private int groundBackDepth = 3;
    [SerializeField] private int groundFrontDepth = 36;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Bridge Mode")]
    [SerializeField] private int artPlaneZ = 24;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 14;
    [SerializeField] private int artPanelHeight = 20;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = true;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;

    [Header("Boomerang Trajectory Platforms (LV7)")]
    [SerializeField] private bool buildBoomerangTrajectoryPlatforms = true;
    [SerializeField] private bool keepManualTrajectoryPlatformTransform = true;
    [SerializeField] private string trajectoryPlatformAName = "LV7_Trajectory_A";
    [SerializeField] private string trajectoryPlatformBName = "LV7_Trajectory_B";
    [SerializeField] private string trajectoryPlatformCName = "LV7_Trajectory_C";
    [SerializeField] private int trajectoryCenterX = 0;
    [SerializeField] private int trajectoryCenterY = 4;
    [SerializeField] private int trajectoryCenterZ = 10;
    [SerializeField] private int trajectoryLaneSpacing = 6;
    [SerializeField] private float trajectoryRadiusX = 2.8f;
    [SerializeField] private float trajectoryRadiusZ = 1.4f;
    [SerializeField] private float trajectoryCycleDuration = 5.4f;
    [SerializeField] private float trajectoryBobAmplitude = 0.18f;
    [SerializeField] private float trajectoryBobFrequency = 1.1f;
    [SerializeField] private float trajectoryFlipInterval = 2.8f;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane trajectoryPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private Transform trajectoryPlatformAPassenger;
    [SerializeField] private Transform trajectoryPlatformBPassenger;
    [SerializeField] private Transform trajectoryPlatformCPassenger;
    [SerializeField] private float shuttleInnerRadius = 0.8f;
    [SerializeField] private float shuttleOuterRadius = 3.8f;
    [SerializeField] private float shuttleCycleDuration = 3.6f;
    [SerializeField] private Vector3 shuttleAxis = Vector3.right;
    [SerializeField] private float shuttleBobAmplitude = 0.1f;
    [SerializeField] private float shuttleBobFrequency = 1.3f;

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

    [ContextMenu("Build LV7 Golden Gate Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV7LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildBoomerangTrajectoryPlatforms();
        Build2DArtPanel();
        BuildGoldenGatePixelArt();
        BuildSeaAndSky();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Clear LV7 Golden Gate Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
        ClearStandaloneTrajectoryPlatforms();
#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
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
        trajectoryCenterY = Mathf.Max(1, trajectoryCenterY);
        trajectoryLaneSpacing = Mathf.Max(2, trajectoryLaneSpacing);
        trajectoryRadiusX = Mathf.Max(0.2f, trajectoryRadiusX);
        trajectoryRadiusZ = Mathf.Max(0.2f, trajectoryRadiusZ);
        trajectoryCycleDuration = Mathf.Max(0.2f, trajectoryCycleDuration);
        trajectoryBobAmplitude = Mathf.Max(0f, trajectoryBobAmplitude);
        trajectoryBobFrequency = Mathf.Max(0f, trajectoryBobFrequency);
        trajectoryFlipInterval = Mathf.Max(0f, trajectoryFlipInterval);
        shuttleInnerRadius = Mathf.Max(0f, shuttleInnerRadius);
        shuttleOuterRadius = Mathf.Max(shuttleInnerRadius + 0.1f, shuttleOuterRadius);
        shuttleCycleDuration = Mathf.Max(0.2f, shuttleCycleDuration);
        shuttleBobAmplitude = Mathf.Max(0f, shuttleBobAmplitude);
        shuttleBobFrequency = Mathf.Max(0f, shuttleBobFrequency);
        if (shuttleAxis.sqrMagnitude <= 0.0001f)
        {
            shuttleAxis = Vector3.right;
        }
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

    private void BuildBoomerangTrajectoryPlatforms()
    {
        if (!buildBoomerangTrajectoryPlatforms)
        {
            ClearStandaloneTrajectoryPlatforms();
            return;
        }

        GameObject platformPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        EnsureFigureEightTrajectoryPlatform(
            trajectoryPlatformAName,
            laneIndex: -1,
            phaseDegrees: 0f,
            shape: FigureEightCloudPlatform.PathShape.FigureEight,
            reverseDirection: false,
            flipIntervalSeconds: 0f,
            assignedPassenger: trajectoryPlatformAPassenger,
            platformPrefab: platformPrefab);

        EnsureFigureEightTrajectoryPlatform(
            trajectoryPlatformBName,
            laneIndex: 0,
            phaseDegrees: 90f,
            shape: FigureEightCloudPlatform.PathShape.Circle,
            reverseDirection: false,
            flipIntervalSeconds: trajectoryFlipInterval,
            assignedPassenger: trajectoryPlatformBPassenger,
            platformPrefab: platformPrefab);

        EnsureShuttleTrajectoryPlatform(
            trajectoryPlatformCName,
            laneIndex: 1,
            assignedPassenger: trajectoryPlatformCPassenger,
            platformPrefab: platformPrefab);
    }

    private void EnsureFigureEightTrajectoryPlatform(
        string objectName,
        int laneIndex,
        float phaseDegrees,
        FigureEightCloudPlatform.PathShape shape,
        bool reverseDirection,
        float flipIntervalSeconds,
        Transform assignedPassenger,
        GameObject platformPrefab)
    {
        if (platformPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneObjectInCurrentScene(objectName);
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

        if (createdNew || !keepManualTrajectoryPlatformTransform)
        {
            AlignBlockToGrid(
                platform,
                trajectoryCenterX + (laneIndex * trajectoryLaneSpacing),
                trajectoryCenterY,
                trajectoryCenterZ);
        }

        EnsureCollider(platform);
        RemoveComponentIfPresent<RadialShuttleCloudPlatform>(platform);

        FigureEightCloudPlatform mover = platform.GetComponent<FigureEightCloudPlatform>();
        if (mover == null)
        {
            mover = platform.AddComponent<FigureEightCloudPlatform>();
        }

        float laneRadiusMultiplier = laneIndex == 0 ? 1.1f : 1f;
        float laneDurationMultiplier = laneIndex == 0 ? 0.85f : 1f;
        mover.Configure(
            radiusX: trajectoryRadiusX * laneRadiusMultiplier,
            radiusZ: trajectoryRadiusZ,
            cycleDuration: trajectoryCycleDuration * laneDurationMultiplier,
            bobAmplitude: trajectoryBobAmplitude,
            bobFrequency: trajectoryBobFrequency,
            assignedPassenger: assignedPassenger,
            plane: trajectoryPlane,
            reverse: reverseDirection,
            shape: shape,
            phaseDegrees: phaseDegrees,
            flipIntervalSeconds: flipIntervalSeconds);
    }

    private void EnsureShuttleTrajectoryPlatform(
        string objectName,
        int laneIndex,
        Transform assignedPassenger,
        GameObject platformPrefab)
    {
        if (platformPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneObjectInCurrentScene(objectName);
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

        if (createdNew || !keepManualTrajectoryPlatformTransform)
        {
            AlignBlockToGrid(
                platform,
                trajectoryCenterX + (laneIndex * trajectoryLaneSpacing),
                trajectoryCenterY,
                trajectoryCenterZ);
        }

        EnsureCollider(platform);
        RemoveComponentIfPresent<FigureEightCloudPlatform>(platform);

        RadialShuttleCloudPlatform mover = platform.GetComponent<RadialShuttleCloudPlatform>();
        if (mover == null)
        {
            mover = platform.AddComponent<RadialShuttleCloudPlatform>();
        }

        mover.Configure(
            innerRadius: shuttleInnerRadius,
            outerRadius: shuttleOuterRadius,
            cycleDuration: shuttleCycleDuration,
            bobAmplitude: shuttleBobAmplitude,
            bobFrequency: shuttleBobFrequency,
            radialAxis: shuttleAxis,
            plane: trajectoryPlane,
            flipIntervalSeconds: trajectoryFlipInterval,
            assignedPassenger: assignedPassenger);
    }

    private void ClearStandaloneTrajectoryPlatforms()
    {
        ClearStandaloneTrajectoryPlatform(trajectoryPlatformAName);
        ClearStandaloneTrajectoryPlatform(trajectoryPlatformBName);
        ClearStandaloneTrajectoryPlatform(trajectoryPlatformCName);
    }

    private void ClearStandaloneTrajectoryPlatform(string objectName)
    {
        Transform existing = FindStandaloneObjectInCurrentScene(objectName);
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

    private Transform FindStandaloneObjectInCurrentScene(string objectName)
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

    private void BuildGoldenGatePixelArt()
    {
        GameObject bridgePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject cablePrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);

        int z = artPlaneZ;
        int baseY = artBaseY + 2;
        int leftTowerX = -10;
        int rightTowerX = 10;
        int towerTopY = baseY + 16;
        int deckY = baseY + 7;

        for (int x = -16; x <= 16; x++)
        {
            SpawnBlock(bridgePrefab, x, deckY, z, $"Deck_{x}");
            if (Mathf.Abs(x) <= 14)
            {
                SpawnBlock(bridgePrefab, x, deckY - 1, z, $"DeckUnder_{x}");
            }
        }

        BuildTower(leftTowerX, baseY, towerTopY, z, bridgePrefab, accentPrefab, "TowerL");
        BuildTower(rightTowerX, baseY, towerTopY, z, bridgePrefab, accentPrefab, "TowerR");

        for (int x = leftTowerX; x <= rightTowerX; x++)
        {
            float t = Mathf.Abs(x) / (float)rightTowerX;
            int cableY = towerTopY - Mathf.RoundToInt(t * t * 6f);
            SpawnBlock(cablePrefab, x, cableY, z, $"CableMain_{x}");

            if ((x & 1) == 0)
            {
                for (int y = deckY + 1; y < cableY; y++)
                {
                    SpawnBlock(cablePrefab, x, y, z, $"Hanger_{x}_{y}");
                }
            }
        }

        for (int x = -20; x < leftTowerX; x++)
        {
            int y = deckY + 1 + Mathf.RoundToInt((leftTowerX - x) * 0.6f);
            SpawnBlock(cablePrefab, x, y, z, $"CableLeft_{x}");
        }

        for (int x = rightTowerX + 1; x <= 20; x++)
        {
            int y = deckY + 1 + Mathf.RoundToInt((x - rightTowerX) * 0.6f);
            SpawnBlock(cablePrefab, x, y, z, $"CableRight_{x}");
        }
    }

    private void BuildTower(int centerX, int baseY, int topY, int z, GameObject bodyPrefab, GameObject accentPrefab, string prefix)
    {
        for (int y = baseY; y <= topY; y++)
        {
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                SpawnBlock(bodyPrefab, x, y, z, $"{prefix}_{x}_{y}");
            }

            if (y == baseY + 6 || y == baseY + 11)
            {
                SpawnBlock(accentPrefab, centerX - 2, y, z, $"{prefix}_CrossL_{y}");
                SpawnBlock(accentPrefab, centerX + 2, y, z, $"{prefix}_CrossR_{y}");
            }
        }

        for (int x = centerX - 2; x <= centerX + 2; x++)
        {
            SpawnBlock(accentPrefab, x, topY + 1, z, $"{prefix}_Cap_{x}");
        }
    }

    private void BuildSeaAndSky()
    {
        GameObject waterPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;
        int waterY = artBaseY + 1;

        for (int x = -20; x <= 20; x++)
        {
            if ((x & 1) == 0)
            {
                SpawnBlock(waterPrefab, x, waterY, z, $"Wave_{x}");
            }
        }

        int sunY = artBaseY + artPanelHeight - 4;
        SpawnBlock(accentPrefab, 11, sunY, z, "SunCore");
        SpawnBlock(accentPrefab, 10, sunY, z, "SunL");
        SpawnBlock(accentPrefab, 12, sunY, z, "SunR");
        SpawnBlock(accentPrefab, 11, sunY + 1, z, "SunTop");
        SpawnBlock(accentPrefab, 11, sunY - 1, z, "SunBottom");
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
