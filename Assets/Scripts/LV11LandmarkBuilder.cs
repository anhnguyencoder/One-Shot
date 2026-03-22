using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV11LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV11_PisaShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 24;
    [SerializeField] private int groundBackDepth = 3;
    [SerializeField] private int groundFrontDepth = 36;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Pisa Mode")]
    [SerializeField] private int artPlaneZ = 24;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 16;
    [SerializeField] private int artPanelHeight = 23;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Enemy Front Platform")]
    [SerializeField] private bool buildEnemyFrontPlatform = true;
    [SerializeField] private int enemyPlatformHalfWidth = 10;
    [SerializeField] private int enemyPlatformForwardLength = 10;
    [SerializeField] private int enemyPlatformY = 0;
    [SerializeField] private int enemyPlatformCenterX = 0;

    [Header("Leaning Lissajous Clouds (LV11)")]
    [SerializeField] private bool buildLeaningLissajousClouds = true;
    [SerializeField] private bool keepManualLissajousTransform = false;
    [SerializeField] private bool keepManualLissajousMotionSettings = true;
    [SerializeField] private int lissajousCenterX = -1;
    [SerializeField] private int lissajousCenterY = 5;
    [SerializeField] private int lissajousCenterZ = 12;
    [SerializeField] private int lissajousLayerSpacingZ = 2;
    [SerializeField] private float lissajousRadiusX = 2.8f;
    [SerializeField] private float lissajousRadiusY = 2.3f;
    [SerializeField] private float lissajousCycleDuration = 19.5f;
    [SerializeField] private float lissajousDepthBobAmplitude = 0.06f;
    [SerializeField] private float lissajousDepthBobFrequency = 1f;
    [SerializeField] private float lissajousPhaseOffsetDegrees = 0f;
    [SerializeField] private bool reverseAlternatingLissajous = true;
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane lissajousPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private string lissajousCloudAName = "LV11_Lissajous_A";
    [SerializeField] private string lissajousCloudBName = "LV11_Lissajous_B";
    [SerializeField] private string lissajousCloudCName = "LV11_Lissajous_C";
    [SerializeField] private string lissajousCloudDName = "LV11_Lissajous_D";
    [SerializeField] private Transform lissajousPassengerA;
    [SerializeField] private Transform lissajousPassengerB;
    [SerializeField] private Transform lissajousPassengerC;
    [SerializeField] private Transform lissajousPassengerD;

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

    [ContextMenu("Build LV11 Pisa Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV11LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildApproachPath();
        BuildLeaningLissajousCloudPlatforms();
        Build2DArtPanel();
        BuildPisaTowerPixelArt();
        BuildTuscanForegroundAndSky();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Clear LV11 Pisa Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
        ClearStandaloneLissajousClouds();
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
        lissajousCenterY = Mathf.Max(1, lissajousCenterY);
        lissajousCenterZ = Mathf.Clamp(lissajousCenterZ, 2, groundFrontDepth - 2);
        lissajousLayerSpacingZ = Mathf.Max(1, lissajousLayerSpacingZ);
        lissajousRadiusX = Mathf.Max(0.2f, lissajousRadiusX);
        lissajousRadiusY = Mathf.Max(0.2f, lissajousRadiusY);
        lissajousCycleDuration = Mathf.Max(0.2f, lissajousCycleDuration);
        lissajousDepthBobAmplitude = Mathf.Max(0f, lissajousDepthBobAmplitude);
        lissajousDepthBobFrequency = Mathf.Max(0f, lissajousDepthBobFrequency);
        lissajousPhaseOffsetDegrees = Mathf.Repeat(lissajousPhaseOffsetDegrees, 360f);
        if (string.IsNullOrWhiteSpace(lissajousCloudAName))
        {
            lissajousCloudAName = "LV11_Lissajous_A";
        }
        if (string.IsNullOrWhiteSpace(lissajousCloudBName))
        {
            lissajousCloudBName = "LV11_Lissajous_B";
        }
        if (string.IsNullOrWhiteSpace(lissajousCloudCName))
        {
            lissajousCloudCName = "LV11_Lissajous_C";
        }
        if (string.IsNullOrWhiteSpace(lissajousCloudDName))
        {
            lissajousCloudDName = "LV11_Lissajous_D";
        }
        if (lissajousCloudBName == lissajousCloudAName)
        {
            lissajousCloudBName = lissajousCloudAName + "_B";
        }
        if (lissajousCloudCName == lissajousCloudAName || lissajousCloudCName == lissajousCloudBName)
        {
            lissajousCloudCName = "LV11_Lissajous_C";
        }
        if (lissajousCloudDName == lissajousCloudAName || lissajousCloudDName == lissajousCloudBName || lissajousCloudDName == lissajousCloudCName)
        {
            lissajousCloudDName = "LV11_Lissajous_D";
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

    private void BuildLeaningLissajousCloudPlatforms()
    {
        if (!buildLeaningLissajousClouds)
        {
            ClearStandaloneLissajousClouds();
            return;
        }

        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        if (cloudPrefab == null)
        {
            return;
        }

        EnsureLissajousCloudPlatform(lissajousCloudAName, 0, lissajousPassengerA, cloudPrefab);
        EnsureLissajousCloudPlatform(lissajousCloudBName, 1, lissajousPassengerB, cloudPrefab);
        EnsureLissajousCloudPlatform(lissajousCloudCName, 2, lissajousPassengerC, cloudPrefab);
        EnsureLissajousCloudPlatform(lissajousCloudDName, 3, lissajousPassengerD, cloudPrefab);
    }

    private void EnsureLissajousCloudPlatform(
        string objectName,
        int index,
        Transform assignedPassenger,
        GameObject cloudPrefab)
    {
        if (cloudPrefab == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform existing = FindStandaloneLissajousObjectInCurrentScene(objectName);
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

        if (createdNew || !keepManualLissajousTransform)
        {
            AlignBlockToGrid(cloud, lissajousCenterX, lissajousCenterY, ResolveLissajousLayerZ(index));
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

        bool shouldApplyBuilderMotion = createdNew || createdMover || !keepManualLissajousMotionSettings;
        if (shouldApplyBuilderMotion)
        {
            bool reverse = reverseAlternatingLissajous && ((index & 1) == 1);
            float phaseDegrees = Mathf.Repeat(lissajousPhaseOffsetDegrees + (index * 90f), 360f);
            mover.Configure(
                radiusX: lissajousRadiusX,
                radiusZ: lissajousRadiusY,
                cycleDuration: lissajousCycleDuration,
                bobAmplitude: lissajousDepthBobAmplitude,
                bobFrequency: lissajousDepthBobFrequency,
                assignedPassenger: assignedPassenger,
                plane: lissajousPlane,
                reverse: reverse,
                shape: FigureEightCloudPlatform.PathShape.Lissajous,
                phaseDegrees: phaseDegrees,
                flipIntervalSeconds: 0f);
        }
    }

    private void ClearStandaloneLissajousClouds()
    {
        ClearStandaloneLissajousCloud(lissajousCloudAName);
        ClearStandaloneLissajousCloud(lissajousCloudBName);
        ClearStandaloneLissajousCloud(lissajousCloudCName);
        ClearStandaloneLissajousCloud(lissajousCloudDName);
    }

    private int ResolveLissajousLayerZ(int index)
    {
        int offset = (index * lissajousLayerSpacingZ) - ((3 * lissajousLayerSpacingZ) / 2);
        int z = lissajousCenterZ + offset;
        return Mathf.Clamp(z, 2, groundFrontDepth - 2);
    }

    private void ClearStandaloneLissajousCloud(string objectName)
    {
        Transform existing = FindStandaloneLissajousObjectInCurrentScene(objectName);
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

    private Transform FindStandaloneLissajousObjectInCurrentScene(string objectName)
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

    private void BuildPisaTowerPixelArt()
    {
        GameObject stonePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject marblePrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;
        int baseY = artBaseY + 2;

        for (int x = -18; x <= 18; x++)
        {
            SpawnBlock(marblePrefab, x, baseY, z, $"Piazza_{x}");
            if (Mathf.Abs(x) <= 16)
            {
                SpawnBlock(marblePrefab, x, baseY + 1, z, $"PiazzaTop_{x}");
            }
        }

        int towerBaseY = baseY + 2;
        for (int level = 0; level < 8; level++)
        {
            int centerX = -4 + level;
            int ringY = towerBaseY + (level * 2);
            int half = level < 2 ? 4 : 3;

            for (int x = centerX - half; x <= centerX + half; x++)
            {
                bool edge = x == centerX - half || x == centerX + half;
                bool beam = ((x - centerX) & 1) == 0;
                if (edge || beam)
                {
                    SpawnBlock(stonePrefab, x, ringY, z, $"TowerRing_{level}_{x}");
                }
            }

            for (int x = centerX - half + 1; x <= centerX + half - 1; x++)
            {
                bool outerPillar = x == centerX - half + 1 || x == centerX + half - 1;
                bool innerPillar = x == centerX - 1 || x == centerX + 1;
                bool opening = Mathf.Abs(x - centerX) <= 1 && (level % 2 == 0);
                if ((outerPillar || innerPillar) && !opening)
                {
                    SpawnBlock(marblePrefab, x, ringY + 1, z, $"TowerPillar_{level}_{x}");
                }
            }

            SpawnBlock(accentPrefab, centerX, ringY + 1, z, $"TowerWindow_{level}");
        }

        int belfryBaseY = towerBaseY + 16;
        int belfryCenter = 4;
        for (int y = belfryBaseY; y <= belfryBaseY + 4; y++)
        {
            int half = y < belfryBaseY + 3 ? 3 : 2;
            for (int x = belfryCenter - half; x <= belfryCenter + half; x++)
            {
                bool edge = x == belfryCenter - half || x == belfryCenter + half;
                bool top = y == belfryBaseY + 4;
                bool bellVoid = Mathf.Abs(x - belfryCenter) <= 1 && y <= belfryBaseY + 2;
                if ((edge || top) && !bellVoid)
                {
                    SpawnBlock(stonePrefab, x, y, z, $"Belfry_{x}_{y}");
                }
            }
        }

        for (int cap = 0; cap < 3; cap++)
        {
            int gy = belfryBaseY + 5 + cap;
            int half = 2 - cap;
            for (int x = belfryCenter - half; x <= belfryCenter + half; x++)
            {
                SpawnBlock(marblePrefab, x, gy, z, $"Cap_{x}_{gy}");
            }
        }

        SpawnBlock(accentPrefab, belfryCenter, belfryBaseY + 8, z, "Spire");

        for (int x = -13; x <= -7; x++)
        {
            SpawnBlock(stonePrefab, x, baseY + 2, z, $"CathedralWing_{x}");
            if (x >= -12 && x <= -8)
            {
                SpawnBlock(marblePrefab, x, baseY + 3, z, $"CathedralRoof_{x}");
            }
        }
    }

    private void BuildTuscanForegroundAndSky()
    {
        GameObject grassPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;

        for (int x = -18; x <= 18; x++)
        {
            if ((x & 1) == 0)
            {
                SpawnBlock(grassPrefab, x, artBaseY + 1, z, $"Grass_{x}");
            }
        }

        for (int x = -10; x <= 10; x += 5)
        {
            SpawnBlock(accentPrefab, x, artBaseY + 2, z, $"Lamp_{x}");
        }

        int sunY = artBaseY + artPanelHeight - 4;
        SpawnBlock(accentPrefab, -11, sunY, z, "SunCore");
        SpawnBlock(accentPrefab, -12, sunY, z, "SunL");
        SpawnBlock(accentPrefab, -10, sunY, z, "SunR");
        SpawnBlock(accentPrefab, -11, sunY + 1, z, "SunTop");
        SpawnBlock(accentPrefab, -11, sunY - 1, z, "SunBottom");
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
