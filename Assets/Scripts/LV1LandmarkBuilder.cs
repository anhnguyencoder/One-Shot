using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV1LandmarkBuilder : MonoBehaviour
{
    private enum CloudTravelAxis
    {
        X,
        Z
    }

    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV1_Giza2DShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 18;
    [SerializeField] private int groundBackDepth = 4;
    [SerializeField] private int groundFrontDepth = 30;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Pyramid Mode")]
    [SerializeField] private bool use2DPixelArtMode = true;
    [SerializeField] private int artPlaneZ = 18;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 10;
    [SerializeField] private int artPanelHeight = 16;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Liberty Landmark")]
    [SerializeField] private int islandRadiusX = 10;
    [SerializeField] private int islandRadiusZ = 8;
    [SerializeField] private int islandCenterZ = 14;
    [SerializeField] private int pedestalHalf = 4;
    [SerializeField] private int pedestalHeight = 4;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject cobblestonePrefab;
    [SerializeField] private GameObject minecraftCubePrefab;
    [SerializeField] private GameObject tntBlockPrefab;

    [Header("Flying Cloud Platform")]
    [SerializeField] private bool buildFlyingCloudPlatform = true;
    [SerializeField] private int cloudStartX = -6;
    [SerializeField] private int cloudStartY = 2;
    [SerializeField] private int cloudStartZ = 5;
    [SerializeField] private CloudTravelAxis cloudTravelAxis = CloudTravelAxis.X;
    [SerializeField] private int cloudTravelDistance = 12;
    [SerializeField] private float cloudTravelDuration = 4.2f;
    [SerializeField] private float cloudHoldDuration = 0.4f;
    [SerializeField] private float cloudBobAmplitude = 0f;
    [SerializeField] private float cloudBobFrequency = 1.25f;
    [SerializeField] private string cloudSceneObjectName = "LV1_FlyingCloudPlatform";
    [SerializeField] private Transform cloudPassenger;
    [SerializeField] private bool autoAssignFocusGroupChildAsPassenger = true;
    [SerializeField] private bool snapPassengerToCloudInEditor = false;
    [SerializeField] private bool snapPassengerToCloudOnPlay = false;
    [SerializeField] private Vector3 cloudPassengerOffset = Vector3.zero;

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

    [ContextMenu("Build LV1 Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV1LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildCauseway();

        if (use2DPixelArtMode)
        {
            Build2DArtPanel();
            BuildPyramidPixelArt();
            BuildDesertAccents();
        }
        else
        {
            BuildPedestal();
            BuildStatue();
        }

        BuildFlyingCloudChallenge();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear LV1 Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
        ClearStandaloneCloudPlatform();
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
        islandRadiusX = Mathf.Max(6, islandRadiusX);
        islandRadiusZ = Mathf.Max(5, islandRadiusZ);
        islandCenterZ = Mathf.Max(8, islandCenterZ);
        pedestalHalf = Mathf.Max(3, pedestalHalf);
        pedestalHeight = Mathf.Max(3, pedestalHeight);
        cloudStartY = Mathf.Max(1, cloudStartY);
        if (cloudTravelDistance == 0)
        {
            cloudTravelDistance = 1;
        }
        cloudTravelDuration = Mathf.Max(0.5f, cloudTravelDuration);
        cloudHoldDuration = Mathf.Max(0f, cloudHoldDuration);
        cloudBobAmplitude = Mathf.Max(0f, cloudBobAmplitude);
        cloudBobFrequency = Mathf.Max(0f, cloudBobFrequency);
        if (string.IsNullOrWhiteSpace(cloudSceneObjectName))
        {
            cloudSceneObjectName = "LV1_FlyingCloudPlatform";
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
        GameObject islandPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject waterPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);

        if (use2DPixelArtMode)
        {
            int roadTargetZ = Mathf.Max(8, artPlaneZ - 2);
            for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
            {
                for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
                {
                    bool border = x == -groundHalfWidth || x == groundHalfWidth || z == -groundBackDepth || z == groundFrontDepth;
                    bool entryRoad = Mathf.Abs(x) <= 2 && z >= -groundBackDepth && z <= roadTargetZ;
                    GameObject prefab = border ? waterPrefab : (entryRoad ? waterPrefab : islandPrefab);
                    SpawnBlock(prefab, x, 0, z, $"Ground_{x}_{z}");
                }
            }

            return;
        }

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                float nx = x / (float)islandRadiusX;
                float nz = (z - islandCenterZ) / (float)islandRadiusZ;
                bool island = (nx * nx) + (nz * nz) <= 1f;
                SpawnBlock(island ? islandPrefab : waterPrefab, x, 0, z, $"Ground_{x}_{z}");
            }
        }
    }

    private void BuildCauseway()
    {
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int pathEnd = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : (islandCenterZ - islandRadiusZ + 1);
        for (int z = -groundBackDepth; z <= pathEnd; z++)
        {
            for (int x = -2; x <= 2; x++)
            {
                SpawnBlock(pathPrefab, x, 1, z, $"Causeway_{x}_{z}");
            }

            if (z % 3 == 0)
            {
                SpawnBlock(accentPrefab, -3, 1, z, $"CausewayAccentL_{z}");
                SpawnBlock(accentPrefab, 3, 1, z, $"CausewayAccentR_{z}");
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

    private void BuildPyramidPixelArt()
    {
        GameObject stonePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject capPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int z = artPlaneZ;
        int baseY = artBaseY + 2;

        BuildPyramid2D(0, baseY, 11, z, stonePrefab, capPrefab, "MainPyramid");
        BuildPyramid2D(-8, baseY, 6, z, stonePrefab, capPrefab, "LeftPyramid");
        BuildPyramid2D(8, baseY, 5, z, stonePrefab, capPrefab, "RightPyramid");
    }

    private void BuildPyramid2D(int centerX, int baseY, int height, int z, GameObject bodyPrefab, GameObject capPrefab, string prefix)
    {
        for (int layer = 0; layer < height; layer++)
        {
            int gy = baseY + layer;
            int half = (height - 1) - layer;
            for (int x = centerX - half; x <= centerX + half; x++)
            {
                SpawnBlock(bodyPrefab, x, gy, z, $"{prefix}_{x}_{gy}");
            }
        }

        SpawnBlock(capPrefab, centerX, baseY + height, z, $"{prefix}_Cap");
    }

    private void BuildDesertAccents()
    {
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int z = artPlaneZ;
        int sunY = artBaseY + artPanelHeight - 3;

        SpawnBlock(accentPrefab, 7, sunY, z, "SunCore");
        SpawnBlock(accentPrefab, 6, sunY, z, "SunL");
        SpawnBlock(accentPrefab, 8, sunY, z, "SunR");
        SpawnBlock(accentPrefab, 7, sunY + 1, z, "SunTop");
        SpawnBlock(accentPrefab, 7, sunY - 1, z, "SunBottom");

        for (int x = -10; x <= 10; x += 2)
        {
            SpawnBlock(accentPrefab, x, artBaseY + 1, z, $"Dune_{x}");
        }
    }

    private void BuildFlyingCloudChallenge()
    {
        if (!buildFlyingCloudPlatform)
        {
            ClearStandaloneCloudPlatform();
            return;
        }

        Vector3Int endCell = ResolveCloudEndCell();
        GameObject dockPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, tntBlockPrefab, cobblestonePrefab);

        BuildCloudLaunchMarkers(cloudStartX, cloudStartY, cloudStartZ, dockPrefab, accentPrefab);
        BuildCloudLandingDock(endCell.x, endCell.y, endCell.z, dockPrefab, accentPrefab);
        EnsureFlyingCloudPlatform(startX: cloudStartX, startY: cloudStartY, startZ: cloudStartZ,
            endX: endCell.x, endY: endCell.y, endZ: endCell.z, cloudPrefab);
    }

    private Vector3Int ResolveCloudEndCell()
    {
        int endX = cloudStartX;
        int endZ = cloudStartZ;

        if (cloudTravelAxis == CloudTravelAxis.X)
        {
            endX += cloudTravelDistance;
        }
        else
        {
            endZ += cloudTravelDistance;
        }

        return new Vector3Int(endX, cloudStartY, endZ);
    }

    private void BuildCloudLaunchMarkers(int centerX, int centerY, int centerZ, GameObject dockPrefab, GameObject accentPrefab)
    {
        for (int y = 1; y <= centerY; y++)
        {
            SpawnBlock(dockPrefab, centerX - 1, y, centerZ, $"CloudLaunchL_{y}");
            SpawnBlock(dockPrefab, centerX + 1, y, centerZ, $"CloudLaunchR_{y}");
        }

        SpawnBlock(accentPrefab, centerX - 1, centerY + 1, centerZ, "CloudLaunchBeaconL");
        SpawnBlock(accentPrefab, centerX + 1, centerY + 1, centerZ, "CloudLaunchBeaconR");
        Vector3Int direction = ResolveCloudTravelDirection();
        SpawnBlock(accentPrefab, centerX + direction.x, centerY + 1, centerZ + direction.z, "CloudLaunchArrow");
    }

    private void BuildCloudLandingDock(int centerX, int centerY, int centerZ, GameObject dockPrefab, GameObject accentPrefab)
    {
        for (int y = 1; y < centerY; y++)
        {
            SpawnBlock(dockPrefab, centerX, y, centerZ, $"CloudDockCore_{y}");
        }

        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            for (int z = centerZ - 1; z <= centerZ + 1; z++)
            {
                if (x == centerX && z == centerZ)
                {
                    continue;
                }

                SpawnBlock(dockPrefab, x, centerY, z, $"CloudDock_{x}_{centerY}_{z}");
            }
        }

        SpawnBlock(accentPrefab, centerX - 1, centerY + 1, centerZ - 1, "CloudDockBeacon_NW");
        SpawnBlock(accentPrefab, centerX + 1, centerY + 1, centerZ - 1, "CloudDockBeacon_NE");
        SpawnBlock(accentPrefab, centerX - 1, centerY + 1, centerZ + 1, "CloudDockBeacon_SW");
        SpawnBlock(accentPrefab, centerX + 1, centerY + 1, centerZ + 1, "CloudDockBeacon_SE");
    }

    /// <summary>
    /// Nếu platform đã tồn tại trong scene → giữ nguyên.
    /// Chỉ tạo mới nếu chưa có.
    /// </summary>
    private void EnsureFlyingCloudPlatform(int startX, int startY, int startZ, int endX, int endY, int endZ, GameObject cloudPrefab)
    {
        if (cloudPrefab == null)
        {
            return;
        }

        // Nếu platform đã tồn tại → không tạo lại
        Transform existing = FindStandaloneCloudPlatformInCurrentScene();
        if (existing != null)
        {
            return;
        }

        // Tạo mới
        GameObject cloud = Instantiate(cloudPrefab);
        cloud.transform.position = Vector3.zero;

        if (normalizePrefabScaleToCell)
        {
            Vector3 scaleMultiplier = GetScaleMultiplier(cloudPrefab, cloud);
            cloud.transform.localScale = Vector3.Scale(cloud.transform.localScale, scaleMultiplier);
        }

        AlignBlockToGrid(cloud, startX, startY, startZ);
        cloud.name = cloudSceneObjectName;
        EnsureCollider(cloud);

        if (cloud.GetComponent<FlyingMinecraftCloudPlatform>() == null)
        {
            cloud.AddComponent<FlyingMinecraftCloudPlatform>();
        }
    }

    private Vector3Int ResolveCloudTravelDirection()
    {
        int direction = cloudTravelDistance >= 0 ? 1 : -1;
        return cloudTravelAxis == CloudTravelAxis.X
            ? new Vector3Int(direction, 0, 0)
            : new Vector3Int(0, 0, direction);
    }

    private void ClearStandaloneCloudPlatform()
    {
        Transform existing = FindStandaloneCloudPlatformInCurrentScene();
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

    private Transform FindStandaloneCloudPlatformInCurrentScene()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == cloudSceneObjectName)
            {
                return roots[i].transform;
            }
        }

        return null;
    }

    private Transform ResolveCloudPassenger()
    {
        if (cloudPassenger != null)
        {
            return cloudPassenger;
        }

        // Explicit assignment only: avoid unexpectedly moving showcase actors
        // (for example Gangnam Style) when passenger field is empty.
        return null;
    }

    private void BuildPedestal()
    {
        GameObject pedestalPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        for (int y = 0; y < pedestalHeight; y++)
        {
            int half = pedestalHalf - (y / 2);
            half = Mathf.Max(2, half);
            int gy = 1 + y;

            for (int x = -half; x <= half; x++)
            {
                for (int z = islandCenterZ - half; z <= islandCenterZ + half; z++)
                {
                    bool shell = x == -half || x == half || z == islandCenterZ - half || z == islandCenterZ + half;
                    if (!shell && y != pedestalHeight - 1)
                    {
                        continue;
                    }

                    SpawnBlock(pedestalPrefab, x, gy, z, $"Pedestal_{x}_{gy}_{z}");
                }
            }
        }
    }

    private void BuildStatue()
    {
        GameObject bodyPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);

        int cz = islandCenterZ;
        int baseY = 1 + pedestalHeight;

        for (int y = baseY; y <= baseY + 3; y++)
        {
            SpawnBlock(bodyPrefab, -1, y, cz, $"LegL_{y}");
            SpawnBlock(bodyPrefab, 1, y, cz, $"LegR_{y}");
        }

        for (int y = baseY + 4; y <= baseY + 9; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                SpawnBlock(bodyPrefab, x, y, cz, $"Torso_{x}_{y}");
            }
        }

        for (int y = baseY + 10; y <= baseY + 12; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                SpawnBlock(bodyPrefab, x, y, cz, $"Head_{x}_{y}");
            }
        }

        for (int x = -2; x <= 2; x++)
        {
            if (x == 0)
            {
                continue;
            }

            SpawnBlock(accentPrefab, x, baseY + 13, cz, $"Crown_{x}");
        }

        for (int y = baseY + 6; y <= baseY + 9; y++)
        {
            SpawnBlock(bodyPrefab, -2, y, cz, $"ArmL_{y}");
        }

        for (int y = baseY + 8; y <= baseY + 14; y++)
        {
            SpawnBlock(bodyPrefab, 3, y, cz, $"ArmR_{y}");
        }

        SpawnBlock(accentPrefab, 3, baseY + 15, cz, "Torch_1");
        SpawnBlock(accentPrefab, 3, baseY + 16, cz, "Torch_2");
        SpawnBlock(accentPrefab, 2, baseY + 15, cz, "Torch_Side");

        for (int y = baseY + 8; y <= baseY + 9; y++)
        {
            SpawnBlock(accentPrefab, -3, y, cz + 1, $"BookL_{y}");
            SpawnBlock(accentPrefab, -2, y, cz + 1, $"BookR_{y}");
        }

        for (int y = baseY + 4; y <= baseY + 10; y++)
        {
            SpawnBlock(bodyPrefab, 0, y, cz + 1, $"Cape_{y}");
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
        Vector3 center = block.transform.InverseTransformPoint(bounds.center);
        Vector3 size = bounds.size;

        // BoxCollider does not support negative size. Convert world size back to local
        // using absolute lossy scale so mirrored meshes still get valid collider extents.
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
