using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV12LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV12_ArcDeTriompheShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 24;
    [SerializeField] private int groundBackDepth = 3;
    [SerializeField] private int groundFrontDepth = 36;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Arc De Triomphe Mode")]
    [SerializeField] private int artPlaneZ = 24;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 17;
    [SerializeField] private int artPanelHeight = 24;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = true;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;

    [Header("Tri-Arc Lissajous Clouds (LV12)")]
    [SerializeField] private bool buildTriArcLissajousClouds = true;
    [SerializeField] private bool keepManualTriArcCloudTransform = false;
    [SerializeField] private bool keepManualTriArcCloudMotionSettings = true;
    [SerializeField] private int triArcCloudAX = -6;
    [SerializeField] private int triArcCloudAY = 5;
    [SerializeField] private int triArcCloudAZ = 10;
    [SerializeField] private int triArcCloudBX = -6;
    [SerializeField] private int triArcCloudBY = 5;
    [SerializeField] private int triArcCloudBZ = 14;
    [SerializeField] private int triArcCloudCX = 0;
    [SerializeField] private int triArcCloudCY = 6;
    [SerializeField] private int triArcCloudCZ = 11;
    [SerializeField] private int triArcCloudDX = 0;
    [SerializeField] private int triArcCloudDY = 6;
    [SerializeField] private int triArcCloudDZ = 15;
    [SerializeField] private int triArcCloudEX = 6;
    [SerializeField] private int triArcCloudEY = 5;
    [SerializeField] private int triArcCloudEZ = 10;
    [SerializeField] private int triArcCloudFX = 6;
    [SerializeField] private int triArcCloudFY = 5;
    [SerializeField] private int triArcCloudFZ = 14;
    [SerializeField] private float triArcRadiusX = 1.9f;
    [SerializeField] private float triArcRadiusY = 2.2f;
    [SerializeField] private float triArcCycleDuration = 17.5f;
    [SerializeField] private float triArcDepthBobAmplitude = 0.04f;
    [SerializeField] private float triArcDepthBobFrequency = 1f;
    [SerializeField] private float triArcPhaseOffsetDegrees = 0f;
    [SerializeField] private bool reverseAlternatingTriArcClouds = true;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane triArcPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private string triArcCloudAName = "LV12_TriArc_A";
    [SerializeField] private string triArcCloudBName = "LV12_TriArc_B";
    [SerializeField] private string triArcCloudCName = "LV12_TriArc_C";
    [SerializeField] private string triArcCloudDName = "LV12_TriArc_D";
    [SerializeField] private string triArcCloudEName = "LV12_TriArc_E";
    [SerializeField] private string triArcCloudFName = "LV12_TriArc_F";
    [SerializeField] private Transform triArcPassengerA;
    [SerializeField] private Transform triArcPassengerB;
    [SerializeField] private Transform triArcPassengerC;
    [SerializeField] private Transform triArcPassengerD;
    [SerializeField] private Transform triArcPassengerE;
    [SerializeField] private Transform triArcPassengerF;

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

    [ContextMenu("Build LV12 Arc De Triomphe Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV12LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildTriArcLissajousCloudPlatforms();
        Build2DArtPanel();
        BuildArcDeTriomphePixelArt();
        BuildParisForegroundAndSky();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Clear LV12 Arc De Triomphe Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
        ClearStandaloneTriArcCloudPlatforms();
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
        int minCloudX = -groundHalfWidth + 2;
        int maxCloudX = groundHalfWidth - 2;
        triArcCloudAX = Mathf.Clamp(triArcCloudAX, minCloudX, maxCloudX);
        triArcCloudBX = Mathf.Clamp(triArcCloudBX, minCloudX, maxCloudX);
        triArcCloudCX = Mathf.Clamp(triArcCloudCX, minCloudX, maxCloudX);
        triArcCloudDX = Mathf.Clamp(triArcCloudDX, minCloudX, maxCloudX);
        triArcCloudEX = Mathf.Clamp(triArcCloudEX, minCloudX, maxCloudX);
        triArcCloudFX = Mathf.Clamp(triArcCloudFX, minCloudX, maxCloudX);
        triArcCloudAY = Mathf.Max(1, triArcCloudAY);
        triArcCloudBY = Mathf.Max(1, triArcCloudBY);
        triArcCloudCY = Mathf.Max(1, triArcCloudCY);
        triArcCloudDY = Mathf.Max(1, triArcCloudDY);
        triArcCloudEY = Mathf.Max(1, triArcCloudEY);
        triArcCloudFY = Mathf.Max(1, triArcCloudFY);
        triArcCloudAZ = Mathf.Clamp(triArcCloudAZ, 2, groundFrontDepth - 2);
        triArcCloudBZ = Mathf.Clamp(triArcCloudBZ, 2, groundFrontDepth - 2);
        triArcCloudCZ = Mathf.Clamp(triArcCloudCZ, 2, groundFrontDepth - 2);
        triArcCloudDZ = Mathf.Clamp(triArcCloudDZ, 2, groundFrontDepth - 2);
        triArcCloudEZ = Mathf.Clamp(triArcCloudEZ, 2, groundFrontDepth - 2);
        triArcCloudFZ = Mathf.Clamp(triArcCloudFZ, 2, groundFrontDepth - 2);
        triArcRadiusX = Mathf.Max(0.2f, triArcRadiusX);
        triArcRadiusY = Mathf.Max(0.2f, triArcRadiusY);
        triArcCycleDuration = Mathf.Max(0.2f, triArcCycleDuration);
        triArcDepthBobAmplitude = Mathf.Max(0f, triArcDepthBobAmplitude);
        triArcDepthBobFrequency = Mathf.Max(0f, triArcDepthBobFrequency);
        triArcPhaseOffsetDegrees = Mathf.Repeat(triArcPhaseOffsetDegrees, 360f);
        if (string.IsNullOrWhiteSpace(triArcCloudAName))
        {
            triArcCloudAName = "LV12_TriArc_A";
        }
        if (string.IsNullOrWhiteSpace(triArcCloudBName))
        {
            triArcCloudBName = "LV12_TriArc_B";
        }
        if (string.IsNullOrWhiteSpace(triArcCloudCName))
        {
            triArcCloudCName = "LV12_TriArc_C";
        }
        if (string.IsNullOrWhiteSpace(triArcCloudDName))
        {
            triArcCloudDName = "LV12_TriArc_D";
        }
        if (string.IsNullOrWhiteSpace(triArcCloudEName))
        {
            triArcCloudEName = "LV12_TriArc_E";
        }
        if (string.IsNullOrWhiteSpace(triArcCloudFName))
        {
            triArcCloudFName = "LV12_TriArc_F";
        }
        if (triArcCloudBName == triArcCloudAName)
        {
            triArcCloudBName = triArcCloudAName + "_B";
        }
        if (triArcCloudCName == triArcCloudAName || triArcCloudCName == triArcCloudBName)
        {
            triArcCloudCName = "LV12_TriArc_C";
        }
        if (triArcCloudDName == triArcCloudAName || triArcCloudDName == triArcCloudBName || triArcCloudDName == triArcCloudCName)
        {
            triArcCloudDName = "LV12_TriArc_D";
        }
        if (triArcCloudEName == triArcCloudAName || triArcCloudEName == triArcCloudBName || triArcCloudEName == triArcCloudCName || triArcCloudEName == triArcCloudDName)
        {
            triArcCloudEName = "LV12_TriArc_E";
        }
        if (triArcCloudFName == triArcCloudAName || triArcCloudFName == triArcCloudBName || triArcCloudFName == triArcCloudCName || triArcCloudFName == triArcCloudDName || triArcCloudFName == triArcCloudEName)
        {
            triArcCloudFName = "LV12_TriArc_F";
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

    private void BuildTriArcLissajousCloudPlatforms()
    {
        if (!buildTriArcLissajousClouds)
        {
            ClearStandaloneTriArcCloudPlatforms();
            return;
        }

        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        if (cloudPrefab == null)
        {
            return;
        }

        EnsureTriArcLissajousCloudPlatform(triArcCloudAName, 0, triArcCloudAX, triArcCloudAY, triArcCloudAZ, triArcPassengerA, cloudPrefab);
        EnsureTriArcLissajousCloudPlatform(triArcCloudBName, 1, triArcCloudBX, triArcCloudBY, triArcCloudBZ, triArcPassengerB, cloudPrefab);
        EnsureTriArcLissajousCloudPlatform(triArcCloudCName, 2, triArcCloudCX, triArcCloudCY, triArcCloudCZ, triArcPassengerC, cloudPrefab);
        EnsureTriArcLissajousCloudPlatform(triArcCloudDName, 3, triArcCloudDX, triArcCloudDY, triArcCloudDZ, triArcPassengerD, cloudPrefab);
        EnsureTriArcLissajousCloudPlatform(triArcCloudEName, 4, triArcCloudEX, triArcCloudEY, triArcCloudEZ, triArcPassengerE, cloudPrefab);
        EnsureTriArcLissajousCloudPlatform(triArcCloudFName, 5, triArcCloudFX, triArcCloudFY, triArcCloudFZ, triArcPassengerF, cloudPrefab);
    }

    private void EnsureTriArcLissajousCloudPlatform(
        string objectName,
        int index,
        int startX,
        int startY,
        int startZ,
        Transform assignedPassenger,
        GameObject cloudPrefab)
    {
        if (cloudPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneTriArcCloudPlatformInCurrentScene(objectName);
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

        if (createdNew || !keepManualTriArcCloudTransform)
        {
            AlignBlockToGrid(cloud, startX, startY, startZ);
        }

        EnsureCollider(cloud);
        RemoveComponentIfPresent<RadialShuttleCloudPlatform>(cloud);
        RemoveComponentIfPresent<VerticalWaveCloudPlatform>(cloud);
        RemoveComponentIfPresent<FlyingMinecraftCloudPlatform>(cloud);

        FigureEightCloudPlatform mover = cloud.GetComponent<FigureEightCloudPlatform>();
        bool createdMover = false;
        if (mover == null)
        {
            mover = cloud.AddComponent<FigureEightCloudPlatform>();
            createdMover = true;
        }

        bool shouldApplyBuilderMotion = createdNew || createdMover || !keepManualTriArcCloudMotionSettings;
        if (shouldApplyBuilderMotion)
        {
            bool reverse = reverseAlternatingTriArcClouds && ((index & 1) == 1);
            mover.Configure(
                radiusX: triArcRadiusX,
                radiusZ: triArcRadiusY,
                cycleDuration: triArcCycleDuration,
                bobAmplitude: triArcDepthBobAmplitude,
                bobFrequency: triArcDepthBobFrequency,
                assignedPassenger: assignedPassenger,
                plane: triArcPlane,
                reverse: reverse,
                shape: FigureEightCloudPlatform.PathShape.Lissajous,
                phaseDegrees: ResolveTriArcPhase(index),
                flipIntervalSeconds: 0f);
        }
    }

    private float ResolveTriArcPhase(int index)
    {
        float patternPhase;
        switch (index)
        {
            case 0:
                patternPhase = 0f;
                break;
            case 1:
                patternPhase = 180f;
                break;
            case 2:
                patternPhase = 60f;
                break;
            case 3:
                patternPhase = 240f;
                break;
            case 4:
                patternPhase = 120f;
                break;
            case 5:
                patternPhase = 300f;
                break;
            default:
                patternPhase = index * 60f;
                break;
        }

        return Mathf.Repeat(triArcPhaseOffsetDegrees + patternPhase, 360f);
    }

    private void ClearStandaloneTriArcCloudPlatforms()
    {
        ClearStandaloneTriArcCloudPlatform(triArcCloudAName);
        ClearStandaloneTriArcCloudPlatform(triArcCloudBName);
        ClearStandaloneTriArcCloudPlatform(triArcCloudCName);
        ClearStandaloneTriArcCloudPlatform(triArcCloudDName);
        ClearStandaloneTriArcCloudPlatform(triArcCloudEName);
        ClearStandaloneTriArcCloudPlatform(triArcCloudFName);
    }

    private void ClearStandaloneTriArcCloudPlatform(string objectName)
    {
        Transform existing = FindStandaloneTriArcCloudPlatformInCurrentScene(objectName);
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

    private Transform FindStandaloneTriArcCloudPlatformInCurrentScene(string objectName)
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

    private void BuildArcDeTriomphePixelArt()
    {
        GameObject stonePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject marblePrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;
        int baseY = artBaseY + 2;

        for (int x = -18; x <= 18; x++)
        {
            SpawnBlock(marblePrefab, x, baseY, z, $"Plaza_{x}");
            if (Mathf.Abs(x) <= 16)
            {
                SpawnBlock(marblePrefab, x, baseY + 1, z, $"PlazaTop_{x}");
            }
        }

        int bodyMinX = -12;
        int bodyMaxX = 12;
        int bodyMinY = baseY + 2;
        int bodyMaxY = baseY + 16;

        for (int y = bodyMinY; y <= bodyMaxY; y++)
        {
            for (int x = bodyMinX; x <= bodyMaxX; x++)
            {
                bool edge = x == bodyMinX || x == bodyMaxX || y == bodyMinY || y == bodyMaxY;
                bool cornice = y == bodyMinY + 4 || y == bodyMinY + 9 || y == bodyMaxY - 2;
                bool pilaster = x == -10 || x == -6 || x == -4 || x == 4 || x == 6 || x == 10;
                bool sidePiers = (x == -2 || x == 2) && y <= bodyMinY + 10;

                bool centralArchVoid = Mathf.Abs(x) <= 3 && y <= bodyMinY + 10;
                bool leftArchVoid = Mathf.Abs(x + 8) <= 1 && y <= bodyMinY + 7;
                bool rightArchVoid = Mathf.Abs(x - 8) <= 1 && y <= bodyMinY + 7;

                bool build = edge || cornice || pilaster || sidePiers;
                if (build && !centralArchVoid && !leftArchVoid && !rightArchVoid)
                {
                    GameObject prefab = edge || cornice ? stonePrefab : marblePrefab;
                    SpawnBlock(prefab, x, y, z, $"ArcBody_{x}_{y}");
                }
            }
        }

        int roofBaseY = bodyMaxY + 1;
        for (int layer = 0; layer < 4; layer++)
        {
            int gy = roofBaseY + layer;
            int half = 11 - (layer * 2);
            for (int x = -half; x <= half; x++)
            {
                bool edge = x == -half || x == half;
                bool top = layer == 3;
                bool ribs = (x & 1) == 0 && layer == 0;
                if (edge || top || ribs)
                {
                    SpawnBlock(stonePrefab, x, gy, z, $"Roof_{x}_{gy}");
                }
            }
        }

        for (int x = -9; x <= 9; x += 3)
        {
            SpawnBlock(accentPrefab, x, bodyMaxY - 1, z, $"Inscription_{x}");
        }

        SpawnBlock(accentPrefab, -1, roofBaseY + 4, z, "TopAccentL");
        SpawnBlock(accentPrefab, 0, roofBaseY + 5, z, "TopAccentCenter");
        SpawnBlock(accentPrefab, 1, roofBaseY + 4, z, "TopAccentR");

        for (int step = 0; step < 4; step++)
        {
            int gy = baseY - step;
            int half = 7 + step;
            for (int x = -half; x <= half; x++)
            {
                SpawnBlock(marblePrefab, x, gy, z, $"Step_{step}_{x}");
            }
        }
    }

    private void BuildParisForegroundAndSky()
    {
        GameObject groundPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;

        for (int x = -19; x <= 19; x++)
        {
            if ((x & 1) == 0)
            {
                SpawnBlock(groundPrefab, x, artBaseY + 1, z, $"Avenue_{x}");
            }
        }

        for (int x = -14; x <= 14; x += 7)
        {
            SpawnBlock(accentPrefab, x, artBaseY + 2, z, $"Lamp_{x}");
            SpawnBlock(accentPrefab, x, artBaseY + 3, z, $"LampTop_{x}");
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
