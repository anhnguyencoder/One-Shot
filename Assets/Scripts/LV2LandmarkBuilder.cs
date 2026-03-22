using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV2LandmarkBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV2_ToriiShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 20;
    [SerializeField] private int groundBackDepth = 4;
    [SerializeField] private int groundFrontDepth = 32;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;

    [Header("2D Torii Mode")]
    [SerializeField] private bool use2DPixelArtMode = true;
    [SerializeField] private int artPlaneZ = 20;
    [SerializeField] private int artBaseY = 1;
    [SerializeField] private int artPanelHalfWidth = 11;
    [SerializeField] private int artPanelHeight = 18;
    [SerializeField] private bool buildPanelBehindArt = true;

    [Header("Colosseum Landmark")]
    [SerializeField] private int arenaCenterZ = 16;
    [SerializeField] private int outerRadiusX = 12;
    [SerializeField] private int outerRadiusZ = 9;
    [SerializeField] private int wallThickness = 2;
    [SerializeField] private int wallHeight = 8;
    [SerializeField] private int entranceWidth = 4;
    [SerializeField] private int entranceHeight = 4;
    [SerializeField] private int seatingSteps = 4;

    [Header("Flying Cloud Platforms (Cân đẩu vân)")]
    [SerializeField] private bool buildFlyingClouds = true;
    [Tooltip("Vị trí grid bắt đầu của cloud bên trái.")]
    [SerializeField] private Vector3Int cloudLeftGridPos = new Vector3Int(-8, 3, 12);
    [Tooltip("Vị trí grid bắt đầu của cloud bên phải.")]
    [SerializeField] private Vector3Int cloudRightGridPos = new Vector3Int(8, 3, 12);
    [Tooltip("Quỹ đạo di chuyển (world units). Mặc định lên xuống.")]
    [SerializeField] private Vector3 cloudTravelOffset = new Vector3(0f, 6f, 0f);
    [Tooltip("Thời gian đi hết 1 chiều (giây).")]
    [SerializeField] private float cloudDuration = 3f;
    [SerializeField] private string cloudLeftName = "LV2_CloudLeft";
    [SerializeField] private string cloudRightName = "LV2_CloudRight";

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

    [ContextMenu("Build LV2 Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV2LandmarkBuilder: Could not resolve any usable block prefab.");
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
        BuildCeremonialRoad();

        if (use2DPixelArtMode)
        {
            Build2DArtPanel();
            BuildToriiPixelArt();
            BuildMountFujiPixelArt();
            BuildLanternAccents();
        }
        else
        {
            BuildOuterWall();
            BuildInnerSeating();
            BuildArenaFloor();
        }

        BuildFlyingClouds();

        Physics.SyncTransforms();
#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Clear LV2 Landmark")]
    public void ClearShowcase()
    {
        PrepareMapRoot();
        ClearMapRootChildren();
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
        groundFrontDepth = Mathf.Max(12, groundFrontDepth);
        artPlaneZ = Mathf.Clamp(artPlaneZ, 8, groundFrontDepth - 2);
        artBaseY = Mathf.Max(1, artBaseY);
        artPanelHalfWidth = Mathf.Clamp(artPanelHalfWidth, 6, Mathf.Max(6, groundHalfWidth - 2));
        artPanelHeight = Mathf.Max(10, artPanelHeight);
        arenaCenterZ = Mathf.Max(8, arenaCenterZ);
        outerRadiusX = Mathf.Max(6, outerRadiusX);
        outerRadiusZ = Mathf.Max(5, outerRadiusZ);
        wallThickness = Mathf.Clamp(wallThickness, 1, 4);
        wallHeight = Mathf.Max(4, wallHeight);
        entranceWidth = Mathf.Max(2, entranceWidth);
        entranceHeight = Mathf.Clamp(entranceHeight, 2, wallHeight - 1);
        seatingSteps = Mathf.Max(2, seatingSteps);
        cloudDuration = Mathf.Max(0.5f, cloudDuration);
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
        GameObject grassPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject edgePrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        int roadTargetZ = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : (arenaCenterZ - outerRadiusZ + 1);

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                bool border = x == -groundHalfWidth || x == groundHalfWidth || z == -groundBackDepth || z == groundFrontDepth;
                bool entryRoad = Mathf.Abs(x) <= 2 && z >= -groundBackDepth && z <= roadTargetZ;
                GameObject prefab = border ? edgePrefab : (entryRoad ? pathPrefab : grassPrefab);
                SpawnBlock(prefab, x, 0, z, $"Ground_{x}_{z}");
            }
        }
    }

    private void BuildCeremonialRoad()
    {
        GameObject pathPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int roadEnd = use2DPixelArtMode ? Mathf.Max(8, artPlaneZ - 2) : (arenaCenterZ - outerRadiusZ + 1);
        int halfRoadWidth = Mathf.Max(2, (entranceWidth / 2) + 1);

        for (int z = 0; z <= roadEnd; z++)
        {
            for (int x = -halfRoadWidth; x <= halfRoadWidth; x++)
            {
                SpawnBlock(pathPrefab, x, 1, z, $"Road_{x}_{z}");
            }

            if (z % 3 == 0)
            {
                SpawnBlock(accentPrefab, -(halfRoadWidth + 1), 1, z, $"RoadAccentL_{z}");
                SpawnBlock(accentPrefab, halfRoadWidth + 1, 1, z, $"RoadAccentR_{z}");
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

    private void BuildToriiPixelArt()
    {
        GameObject toriiPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int z = artPlaneZ;
        int baseY = artBaseY + 1;

        for (int y = baseY; y <= baseY + 10; y++)
        {
            SpawnBlock(toriiPrefab, -6, y, z, $"ToriiPostL_{y}");
            SpawnBlock(toriiPrefab, 6, y, z, $"ToriiPostR_{y}");
        }

        for (int x = -10; x <= 10; x++)
        {
            SpawnBlock(accentPrefab, x, baseY + 11, z, $"ToriiTop_{x}");
        }

        for (int x = -8; x <= 8; x++)
        {
            SpawnBlock(toriiPrefab, x, baseY + 9, z, $"ToriiBeam_{x}");
        }

        for (int x = -4; x <= 4; x++)
        {
            SpawnBlock(toriiPrefab, x, baseY + 6, z, $"ToriiBrace_{x}");
        }
    }

    private void BuildMountFujiPixelArt()
    {
        GameObject mountainPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        GameObject snowPrefab = PickPrefab(tntBlockPrefab, minecraftCubePrefab, cobblestonePrefab);
        int z = artPlaneZ;
        int centerY = artBaseY + 3;

        string[] rows =
        {
            ".............#.............",
            "............###............",
            "...........#####...........",
            "..........#######..........",
            ".........#########.........",
            "........###########........",
            ".......#############.......",
            "......###############......",
            ".....#################.....",
            "....###################....",
            "...#####################...",
            "..#######################..",
            ".#########################.",
            "###########################"
        };

        int centerIndex = rows[0].Length / 2;
        int topY = centerY + rows.Length - 1;

        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            int gy = topY - r;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] != '#')
                {
                    continue;
                }

                int gx = i - centerIndex;
                bool snowCap = r <= 3 || (r <= 5 && Mathf.Abs(gx) <= 2);
                SpawnBlock(snowCap ? snowPrefab : mountainPrefab, gx, gy, z, $"Fuji_{gx}_{gy}");
            }
        }
    }

    private void BuildLanternAccents()
    {
        GameObject accentPrefab = PickPrefab(tntBlockPrefab, cobblestonePrefab, minecraftCubePrefab);
        int z = artPlaneZ;
        int y = artBaseY + 2;

        SpawnBlock(accentPrefab, -9, y, z, "LanternL_Base");
        SpawnBlock(accentPrefab, -9, y + 1, z, "LanternL_Top");
        SpawnBlock(accentPrefab, 9, y, z, "LanternR_Base");
        SpawnBlock(accentPrefab, 9, y + 1, z, "LanternR_Top");
    }

    // ─── Flying Cloud Platforms ───────────────────────────────────────

    private void BuildFlyingClouds()
    {
        if (!buildFlyingClouds)
        {
            return;
        }

        GameObject cloudPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);
        EnsureCloudPlatform(cloudLeftName, cloudLeftGridPos, cloudPrefab);
        EnsureCloudPlatform(cloudRightName, cloudRightGridPos, cloudPrefab);
    }

    private void EnsureCloudPlatform(string cloudName, Vector3Int gridPos, GameObject prefab)
    {
        if (prefab == null) return;

        // Nếu đã tồn tại → giữ nguyên vị trí
        Transform existing = FindSceneObjectByName(cloudName);
        if (existing != null) return;

        // Tạo mới (standalone, không thuộc mapRoot nên không bị xóa khi rebuild)
        GameObject cloud = Instantiate(prefab);
        cloud.transform.position = Vector3.zero;

        if (normalizePrefabScaleToCell)
        {
            Vector3 scaleMultiplier = GetScaleMultiplier(prefab, cloud);
            cloud.transform.localScale = Vector3.Scale(cloud.transform.localScale, scaleMultiplier);
        }

        AlignBlockToGrid(cloud, gridPos.x, gridPos.y, gridPos.z);
        cloud.name = cloudName;
        EnsureCollider(cloud);

        FlyingMinecraftCloudPlatform mover = cloud.AddComponent<FlyingMinecraftCloudPlatform>();
    }

    private Transform FindSceneObjectByName(string objectName)
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return null;

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

    private void BuildOuterWall()
    {
        GameObject wallPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);

        int innerRadiusX = Mathf.Max(2, outerRadiusX - wallThickness);
        int innerRadiusZ = Mathf.Max(2, outerRadiusZ - wallThickness);

        for (int y = 1; y <= wallHeight; y++)
        {
            for (int x = -outerRadiusX - 1; x <= outerRadiusX + 1; x++)
            {
                for (int z = arenaCenterZ - outerRadiusZ - 1; z <= arenaCenterZ + outerRadiusZ + 1; z++)
                {
                    if (!IsInEllipse(x, z - arenaCenterZ, outerRadiusX, outerRadiusZ))
                    {
                        continue;
                    }

                    if (IsInEllipse(x, z - arenaCenterZ, innerRadiusX, innerRadiusZ))
                    {
                        continue;
                    }

                    SpawnBlock(wallPrefab, x, y, z, $"ColosseumWall_{x}_{y}_{z}");
                }
            }
        }
    }

    private void BuildInnerSeating()
    {
        GameObject seatPrefab = PickPrefab(cobblestonePrefab, minecraftCubePrefab, tntBlockPrefab);

        int baseRadiusX = Mathf.Max(3, outerRadiusX - wallThickness - 1);
        int baseRadiusZ = Mathf.Max(3, outerRadiusZ - wallThickness - 1);
        int seatHeightTop = wallHeight - 1;

        for (int step = 0; step < seatingSteps; step++)
        {
            int rx = baseRadiusX - step;
            int rz = baseRadiusZ - step;
            if (rx < 2 || rz < 2)
            {
                break;
            }

            int innerRx = Mathf.Max(1, rx - 1);
            int innerRz = Mathf.Max(1, rz - 1);
            int gy = Mathf.Max(2, seatHeightTop - step);

            for (int x = -rx; x <= rx; x++)
            {
                for (int z = arenaCenterZ - rz; z <= arenaCenterZ + rz; z++)
                {
                    int relZ = z - arenaCenterZ;
                    bool onOuter = IsInEllipse(x, relZ, rx, rz);
                    bool onInner = IsInEllipse(x, relZ, innerRx, innerRz);
                    if (!onOuter || onInner)
                    {
                        continue;
                    }

                    SpawnBlock(seatPrefab, x, gy, z, $"Seat_{step}_{x}_{z}");
                }
            }
        }
    }

    private void BuildArenaFloor()
    {
        GameObject floorPrefab = PickPrefab(minecraftCubePrefab, cobblestonePrefab, tntBlockPrefab);

        int floorRadiusX = Mathf.Max(2, outerRadiusX - wallThickness - 3);
        int floorRadiusZ = Mathf.Max(2, outerRadiusZ - wallThickness - 3);
        int floorY = 1;

        for (int x = -floorRadiusX; x <= floorRadiusX; x++)
        {
            for (int z = arenaCenterZ - floorRadiusZ; z <= arenaCenterZ + floorRadiusZ; z++)
            {
                if (!IsInEllipse(x, z - arenaCenterZ, floorRadiusX, floorRadiusZ))
                {
                    continue;
                }

                SpawnBlock(floorPrefab, x, floorY, z, $"ArenaFloor_{x}_{z}");
            }
        }
    }

    private bool IsInEllipse(int x, int z, int radiusX, int radiusZ)
    {
        if (radiusX <= 0 || radiusZ <= 0)
        {
            return false;
        }

        float nx = x / (float)radiusX;
        float nz = z / (float)radiusZ;
        return (nx * nx) + (nz * nz) <= 1f;
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

        GameObject block = Instantiate(prefab, Vector3.zero, Quaternion.identity, mapRoot);
        block.transform.localPosition = Vector3.zero;
        block.transform.localRotation = Quaternion.identity;

        if (normalizePrefabScaleToCell)
        {
            Vector3 scaleMultiplier = GetScaleMultiplier(prefab, block);
            block.transform.localScale = Vector3.Scale(block.transform.localScale, scaleMultiplier);
        }

        AlignBlockToGrid(block, gx, gy, gz);
        block.transform.localRotation = Quaternion.identity;
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
