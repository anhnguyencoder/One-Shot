using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV6LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV6_BigBenShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 20;
    [SerializeField] private int groundBackDepth = 3;
    [SerializeField] private int groundFrontDepth = 34;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Big Ben Mode")]
    [SerializeField] private int artPlaneZ = 22;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 12;
    [SerializeField] private int artPanelHeight = 22;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = false;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;

    [Header("Orbit Cloud Carousel (LV6)")]
    [SerializeField] private bool buildOrbitCloudPlatforms = true;
    [SerializeField] private bool keepManualCloudTransform = true;
    [SerializeField] private string orbitCloudAName = "LV6_Cloud_A";
    [SerializeField] private string orbitCloudBName = "LV6_Cloud_B";
    [SerializeField] private string orbitCloudCName = "LV6_Cloud_C";
    [SerializeField] private string orbitCloudDName = "LV6_Cloud_D";
    [SerializeField] private string orbitCloudCoreName = "LV6_Cloud_Core";
    [SerializeField] private bool buildCoreShuttleCloud = false;
    [SerializeField] private int orbitCenterX = 0;
    [SerializeField] private int orbitCenterY = 4;
    [SerializeField] private int orbitCenterZ = 10;
    [SerializeField] private float outerOrbitRadius = 4f;
    [SerializeField] private float outerOrbitDuration = 8f;
    [SerializeField] private float outerOrbitBobAmplitude = 0.15f;
    [SerializeField] private float outerOrbitBobFrequency = 1f;
    [SerializeField] private float directionFlipInterval = 6f;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane orbitPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private Transform orbitCloudAPassenger;
    [SerializeField] private Transform orbitCloudBPassenger;
    [SerializeField] private Transform orbitCloudCPassenger;
    [SerializeField] private Transform orbitCloudDPassenger;
    [SerializeField] private Transform orbitCloudCorePassenger;
    [SerializeField] private float coreInnerRadius = 1.2f;
    [SerializeField] private float coreOuterRadius = 4.8f;
    [SerializeField] private float coreShuttleDuration = 4f;
    [SerializeField] private Vector3 coreRadialAxis = Vector3.up;
    [SerializeField] private float coreBobAmplitude = 0.08f;
    [SerializeField] private float coreBobFrequency = 1.2f;

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

    [ContextMenu("Build LV6 Big Ben Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV6LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildOrbitCloudPlatforms();
        Build2DArtPanel();
        BuildBigBenPixelArt();
        BuildSkyAccents();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear LV6 Big Ben Landmark")]
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
        groundFrontDepth = Mathf.Max(14, groundFrontDepth);
        artPlaneZ = Mathf.Clamp(artPlaneZ, 8, groundFrontDepth - 2);
        artBaseY = Mathf.Max(1, artBaseY);
        artPanelHalfWidth = Mathf.Clamp(artPanelHalfWidth, 6, Mathf.Max(6, groundHalfWidth - 2));
        artPanelHeight = Mathf.Max(12, artPanelHeight);
        enemyPlatformHalfWidth = Mathf.Max(4, enemyPlatformHalfWidth);
        enemyPlatformForwardLength = Mathf.Max(2, enemyPlatformForwardLength);
        orbitCenterY = Mathf.Max(1, orbitCenterY);
        outerOrbitRadius = Mathf.Max(0.2f, outerOrbitRadius);
        outerOrbitDuration = Mathf.Max(0.2f, outerOrbitDuration);
        outerOrbitBobAmplitude = Mathf.Max(0f, outerOrbitBobAmplitude);
        outerOrbitBobFrequency = Mathf.Max(0f, outerOrbitBobFrequency);
        directionFlipInterval = Mathf.Max(0f, directionFlipInterval);
        coreInnerRadius = Mathf.Max(0f, coreInnerRadius);
        coreOuterRadius = Mathf.Max(coreInnerRadius + 0.1f, coreOuterRadius);
        coreShuttleDuration = Mathf.Max(0.2f, coreShuttleDuration);
        coreBobAmplitude = Mathf.Max(0f, coreBobAmplitude);
        coreBobFrequency = Mathf.Max(0f, coreBobFrequency);
        if (coreRadialAxis.sqrMagnitude <= 0.0001f)
        {
            coreRadialAxis = Vector3.up;
        }
        if (string.IsNullOrWhiteSpace(orbitCloudAName))
        {
            orbitCloudAName = "LV6_Cloud_A";
        }
        if (string.IsNullOrWhiteSpace(orbitCloudBName))
        {
            orbitCloudBName = "LV6_Cloud_B";
        }
        if (string.IsNullOrWhiteSpace(orbitCloudCName))
        {
            orbitCloudCName = "LV6_Cloud_C";
        }
        if (string.IsNullOrWhiteSpace(orbitCloudDName))
        {
            orbitCloudDName = "LV6_Cloud_D";
        }
        if (string.IsNullOrWhiteSpace(orbitCloudCoreName))
        {
            orbitCloudCoreName = "LV6_Cloud_Core";
        }
        if (orbitCloudBName == orbitCloudAName)
        {
            orbitCloudBName = orbitCloudAName + "_B";
        }
        if (orbitCloudCName == orbitCloudAName || orbitCloudCName == orbitCloudBName)
        {
            orbitCloudCName = "LV6_Cloud_C";
        }
        if (orbitCloudDName == orbitCloudAName || orbitCloudDName == orbitCloudBName || orbitCloudDName == orbitCloudCName)
        {
            orbitCloudDName = "LV6_Cloud_D";
        }
        if (orbitCloudCoreName == orbitCloudAName || orbitCloudCoreName == orbitCloudBName || orbitCloudCoreName == orbitCloudCName || orbitCloudCoreName == orbitCloudDName)
        {
            orbitCloudCoreName = "LV6_Cloud_Core";
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
        GameObject roadPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        int roadTargetZ = Mathf.Max(8, artPlaneZ - 2);

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                bool border = x == -groundHalfWidth || x == groundHalfWidth || z == -groundBackDepth || z == groundFrontDepth;
                bool road = Mathf.Abs(x) <= 2 && z >= -groundBackDepth && z <= roadTargetZ;
                SpawnBlock(border ? borderPrefab : (road ? roadPrefab : groundPrefab), x, 0, z, $"Ground_{x}_{z}");
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

    private void BuildOrbitCloudPlatforms()
    {
        if (!buildOrbitCloudPlatforms)
        {
            ClearStandaloneCloudPlatforms();
            return;
        }

        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        EnsureOrbitCloudPlatform(orbitCloudAName, 0f, orbitCloudAPassenger, cloudPrefab);
        EnsureOrbitCloudPlatform(orbitCloudBName, 90f, orbitCloudBPassenger, cloudPrefab);
        EnsureOrbitCloudPlatform(orbitCloudCName, 180f, orbitCloudCPassenger, cloudPrefab);
        EnsureOrbitCloudPlatform(orbitCloudDName, 270f, orbitCloudDPassenger, cloudPrefab);
        if (buildCoreShuttleCloud)
        {
            EnsureRadialShuttleCloud(orbitCloudCoreName, orbitCloudCorePassenger, cloudPrefab);
        }
        else
        {
            ClearStandaloneCloudPlatform(orbitCloudCoreName);
        }
    }

    private void EnsureOrbitCloudPlatform(
        string objectName,
        float phaseDegrees,
        Transform assignedPassenger,
        GameObject cloudPrefab)
    {
        if (cloudPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneObjectInCurrentScene(objectName);
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
            AlignBlockToGrid(cloud, orbitCenterX, orbitCenterY, orbitCenterZ);
        }

        EnsureCollider(cloud);
        RemoveComponentIfPresent<RadialShuttleCloudPlatform>(cloud);

        FigureEightCloudPlatform mover = cloud.GetComponent<FigureEightCloudPlatform>();
        if (mover == null)
        {
            mover = cloud.AddComponent<FigureEightCloudPlatform>();
        }

        mover.Configure(
            radiusX: outerOrbitRadius,
            radiusZ: outerOrbitRadius,
            cycleDuration: outerOrbitDuration,
            bobAmplitude: outerOrbitBobAmplitude,
            bobFrequency: outerOrbitBobFrequency,
            assignedPassenger: assignedPassenger,
            plane: orbitPlane,
            reverse: false,
            shape: FigureEightCloudPlatform.PathShape.Circle,
            phaseDegrees: phaseDegrees,
            flipIntervalSeconds: directionFlipInterval);
    }

    private void EnsureRadialShuttleCloud(
        string objectName,
        Transform assignedPassenger,
        GameObject cloudPrefab)
    {
        if (cloudPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneObjectInCurrentScene(objectName);
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
            AlignBlockToGrid(cloud, orbitCenterX, orbitCenterY, orbitCenterZ);
        }

        EnsureCollider(cloud);
        RemoveComponentIfPresent<FigureEightCloudPlatform>(cloud);

        RadialShuttleCloudPlatform mover = cloud.GetComponent<RadialShuttleCloudPlatform>();
        if (mover == null)
        {
            mover = cloud.AddComponent<RadialShuttleCloudPlatform>();
        }

        mover.Configure(
            innerRadius: coreInnerRadius,
            outerRadius: coreOuterRadius,
            cycleDuration: coreShuttleDuration,
            bobAmplitude: coreBobAmplitude,
            bobFrequency: coreBobFrequency,
            radialAxis: coreRadialAxis,
            plane: orbitPlane,
            flipIntervalSeconds: directionFlipInterval,
            assignedPassenger: assignedPassenger);
    }

    private void ClearStandaloneCloudPlatforms()
    {
        ClearStandaloneCloudPlatform(orbitCloudAName);
        ClearStandaloneCloudPlatform(orbitCloudBName);
        ClearStandaloneCloudPlatform(orbitCloudCName);
        ClearStandaloneCloudPlatform(orbitCloudDName);
        ClearStandaloneCloudPlatform(orbitCloudCoreName);
    }

    private void ClearStandaloneCloudPlatform(string objectName)
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

    private void BuildBigBenPixelArt()
    {
        GameObject bodyPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject trimPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject clockPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;
        int baseY = artBaseY + 2;

        for (int x = -10; x <= 10; x++)
        {
            SpawnBlock(trimPrefab, x, baseY, z, $"Plinth_{x}");
            if (Mathf.Abs(x) <= 8)
            {
                SpawnBlock(trimPrefab, x, baseY + 1, z, $"PlinthTop_{x}");
            }
        }

        for (int side = -1; side <= 1; side += 2)
        {
            int wingCenter = side * 8;
            for (int y = baseY + 2; y <= baseY + 8; y++)
            {
                for (int x = wingCenter - 2; x <= wingCenter + 2; x++)
                {
                    SpawnBlock(bodyPrefab, x, y, z, $"Wing_{side}_{x}_{y}");
                }
            }

            for (int x = wingCenter - 3; x <= wingCenter + 3; x++)
            {
                SpawnBlock(trimPrefab, x, baseY + 9, z, $"WingRoof_{side}_{x}");
            }
        }

        for (int y = baseY + 2; y <= baseY + 19; y++)
        {
            int half = y <= baseY + 6 ? 4 : (y <= baseY + 14 ? 3 : 2);
            for (int x = -half; x <= half; x++)
            {
                SpawnBlock(bodyPrefab, x, y, z, $"Tower_{x}_{y}");
            }

            if (y % 3 == 0)
            {
                SpawnBlock(trimPrefab, -half, y, z, $"TowerTrimL_{y}");
                SpawnBlock(trimPrefab, half, y, z, $"TowerTrimR_{y}");
            }
        }

        int clockBaseY = baseY + 13;
        for (int y = clockBaseY; y <= clockBaseY + 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                bool edge = Mathf.Abs(x) == 2 || y == clockBaseY || y == clockBaseY + 2;
                SpawnBlock(edge ? clockPrefab : trimPrefab, x, y, z, $"Clock_{x}_{y}");
            }
        }

        int spireBaseY = baseY + 20;
        for (int layer = 0; layer < 7; layer++)
        {
            int gy = spireBaseY + layer;
            int half = layer < 2 ? 2 : (layer < 5 ? 1 : 0);
            for (int x = -half; x <= half; x++)
            {
                SpawnBlock(trimPrefab, x, gy, z, $"Spire_{x}_{gy}");
            }
        }

        SpawnBlock(clockPrefab, 0, spireBaseY + 7, z, "SpireCap");
    }

    private void BuildSkyAccents()
    {
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;
        int moonY = artBaseY + artPanelHeight - 4;

        SpawnBlock(accentPrefab, 9, moonY, z, "MoonCore");
        SpawnBlock(accentPrefab, 8, moonY, z, "MoonL");
        SpawnBlock(accentPrefab, 9, moonY + 1, z, "MoonTop");
        SpawnBlock(accentPrefab, 10, moonY - 1, z, "MoonCurve");

        for (int x = -11; x <= -7; x++)
        {
            SpawnBlock(accentPrefab, x, moonY - 2, z, $"CloudL_{x}");
        }

        for (int x = 2; x <= 5; x++)
        {
            SpawnBlock(accentPrefab, x, moonY - 3, z, $"CloudR_{x}");
        }
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
