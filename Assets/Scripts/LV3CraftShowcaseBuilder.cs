using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class LV3CraftShowcaseBuilder : MonoBehaviour
{
    [Header("Build Trigger")]
    [SerializeField] private bool autoBuildInEditMode = true;
    [SerializeField] private bool rebuildOnPlay = true;
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("References")]
    [SerializeField] private Transform focusGroup;
    [SerializeField] private string mapRootName = "LV3_TajMahalShowcase";

    [Header("Layout")]
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int groundHalfWidth = 12;
    [SerializeField] private int groundBackDepth = 6;
    [SerializeField] private int groundFrontDepth = 26;
    [SerializeField] private int citadelCenterZ = 16;
    [SerializeField] private bool normalizePrefabScaleToCell = true;
    [SerializeField] private bool preventCellOverlap = true;
    [SerializeField] private bool buildSimpleShowcase = true;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject grassBlockPrefab;
    [SerializeField] private GameObject dirtBlockPrefab;
    [SerializeField] private GameObject stoneBlockPrefab;
    [SerializeField] private GameObject brickBlockPrefab;
    [SerializeField] private GameObject metalBlockPrefab;
    [SerializeField] private GameObject crystalBlockPrefab;
    [SerializeField] private GameObject cobblestonePrefab;
    [SerializeField] private GameObject minecraftCubePrefab;
    [SerializeField] private GameObject tntBlockPrefab;

    private const string GrassPath = "Assets/Cube World Kit-zip/Grass Block/Block_Grass.fbx";
    private const string DirtPath = "Assets/Cube World Kit-zip/Dirt Block/Block_Dirt.fbx";
    private const string StonePath = "Assets/Cube World Kit-zip/Stone Block/Stone.fbx";
    private const string BrickPath = "Assets/Cube World Kit-zip/Brick Block/Block_Brick.fbx";
    private const string MetalPath = "Assets/Cube World Kit-zip/Metal Block/Block_Metal.fbx";
    private const string CrystalPath = "Assets/Cube World Kit-zip/Crystal Block/Block_Crystal.fbx";
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

    private void Start()
    {
        if (Application.isPlaying && rebuildOnPlay)
        {
            BuildShowcase();
        }
    }

    [ContextMenu("Build LV3 Landmark")]
    public void BuildShowcase()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("LV3CraftShowcaseBuilder: Could not resolve any usable block prefab.");
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

        if (buildSimpleShowcase)
        {
            BuildSimpleMap();
        }
        else
        {
            BuildGround();
            BuildRunway();
            BuildPerformerPads();
            BuildSidePillars();
            BuildHeroArch();
            BuildCitadel();
            BuildSkyCrown();
        }

        Physics.SyncTransforms();
#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Clear LV3 Landmark")]
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
        groundHalfWidth = Mathf.Max(8, groundHalfWidth);
        groundBackDepth = Mathf.Max(3, groundBackDepth);
        groundFrontDepth = Mathf.Max(10, groundFrontDepth);
        citadelCenterZ = Mathf.Max(10, citadelCenterZ);
        TryAutoAssignPrefabs();
    }

    private bool TryAutoAssignPrefabs()
    {
        grassBlockPrefab = grassBlockPrefab != null ? grassBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(GrassPath);
        dirtBlockPrefab = dirtBlockPrefab != null ? dirtBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DirtPath);
        stoneBlockPrefab = stoneBlockPrefab != null ? stoneBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(StonePath);
        brickBlockPrefab = brickBlockPrefab != null ? brickBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(BrickPath);
        metalBlockPrefab = metalBlockPrefab != null ? metalBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(MetalPath);
        crystalBlockPrefab = crystalBlockPrefab != null ? crystalBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPath);
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
        return FirstNonNull(
            grassBlockPrefab,
            dirtBlockPrefab,
            stoneBlockPrefab,
            brickBlockPrefab,
            metalBlockPrefab,
            crystalBlockPrefab,
            cobblestonePrefab,
            minecraftCubePrefab,
            tntBlockPrefab);
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
        GameObject probePrefab = PickPrefab(
            grassBlockPrefab,
            stoneBlockPrefab,
            dirtBlockPrefab,
            cobblestonePrefab,
            minecraftCubePrefab,
            tntBlockPrefab,
            brickBlockPrefab,
            metalBlockPrefab,
            crystalBlockPrefab);

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
        GameObject groundStone = PickPrefab(
            stoneBlockPrefab,
            cobblestonePrefab,
            minecraftCubePrefab,
            dirtBlockPrefab,
            grassBlockPrefab,
            tntBlockPrefab);
        GameObject groundDirt = PickPrefab(
            dirtBlockPrefab,
            grassBlockPrefab,
            stoneBlockPrefab,
            cobblestonePrefab,
            minecraftCubePrefab,
            tntBlockPrefab);

        for (int z = -groundBackDepth; z <= groundFrontDepth; z++)
        {
            for (int x = -groundHalfWidth; x <= groundHalfWidth; x++)
            {
                SpawnBlock(groundStone, x, -2, z, $"Ground_Stone_{x}_{z}");
                SpawnBlock(groundDirt, x, -1, z, $"Ground_Dirt_{x}_{z}");

                GameObject topPrefab = ResolveTopSurfacePrefab(x, z);
                SpawnBlock(topPrefab, x, 0, z, $"Ground_Top_{x}_{z}");
            }
        }
    }

    private void BuildSimpleMap()
    {
        GameObject groundPrefab = PickPrefab(
            minecraftCubePrefab,
            grassBlockPrefab,
            dirtBlockPrefab,
            cobblestonePrefab,
            stoneBlockPrefab,
            brickBlockPrefab,
            metalBlockPrefab,
            crystalBlockPrefab,
            tntBlockPrefab);

        GameObject pathPrefab = PickPrefab(
            cobblestonePrefab,
            stoneBlockPrefab,
            brickBlockPrefab,
            metalBlockPrefab,
            minecraftCubePrefab,
            grassBlockPrefab,
            dirtBlockPrefab,
            crystalBlockPrefab,
            tntBlockPrefab);

        GameObject bodyPrefab = PickPrefab(
            crystalBlockPrefab,
            minecraftCubePrefab,
            stoneBlockPrefab,
            cobblestonePrefab,
            brickBlockPrefab,
            metalBlockPrefab,
            grassBlockPrefab,
            dirtBlockPrefab,
            tntBlockPrefab);

        GameObject accentPrefab = PickPrefab(
            cobblestonePrefab,
            brickBlockPrefab,
            stoneBlockPrefab,
            minecraftCubePrefab,
            metalBlockPrefab,
            crystalBlockPrefab,
            grassBlockPrefab,
            dirtBlockPrefab,
            tntBlockPrefab);

        GameObject highlightPrefab = PickPrefab(
            tntBlockPrefab,
            crystalBlockPrefab,
            cobblestonePrefab,
            stoneBlockPrefab,
            minecraftCubePrefab,
            brickBlockPrefab,
            metalBlockPrefab,
            grassBlockPrefab,
            dirtBlockPrefab);

        if (groundPrefab == null && pathPrefab == null && bodyPrefab == null)
        {
            return;
        }

        if (groundPrefab == null)
        {
            groundPrefab = pathPrefab;
        }

        if (pathPrefab == null)
        {
            pathPrefab = groundPrefab;
        }

        if (bodyPrefab == null)
        {
            bodyPrefab = pathPrefab;
        }

        if (accentPrefab == null)
        {
            accentPrefab = pathPrefab;
        }

        if (highlightPrefab == null)
        {
            highlightPrefab = accentPrefab;
        }

        int minX = -13;
        int maxX = 13;
        int minZ = 0;
        int maxZ = 24;
        int tajCenterZ = 14;

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool path = Mathf.Abs(x) <= 2 && z <= tajCenterZ - 2;
                bool plaza = Mathf.Abs(x) <= 10 && z >= tajCenterZ - 3 && z <= tajCenterZ + 5;
                bool border = x == minX || x == maxX || z == minZ || z == maxZ;
                GameObject topPrefab = (path || plaza || border) ? pathPrefab : groundPrefab;
                SpawnBlock(topPrefab, x, 0, z, $"Simple_Top_{x}_{z}");
            }
        }

        BuildReflectingPool(pathPrefab, highlightPrefab, tajCenterZ);
        BuildTajMahalPixelArt(bodyPrefab, accentPrefab, highlightPrefab, tajCenterZ);
    }

    private void BuildTajMahalPixelArt(GameObject bodyPrefab, GameObject accentPrefab, GameObject highlightPrefab, int centerZ)
    {
        if (bodyPrefab == null)
        {
            return;
        }

        if (accentPrefab == null)
        {
            accentPrefab = bodyPrefab;
        }

        if (highlightPrefab == null)
        {
            highlightPrefab = accentPrefab;
        }

        int baseY = 1;

        // Front terrace.
        for (int x = -11; x <= 11; x++)
        {
            SpawnBlock(accentPrefab, x, baseY + 1, centerZ, $"TajPlinth_{x}");
        }

        for (int x = -10; x <= 10; x++)
        {
            SpawnBlock(accentPrefab, x, baseY + 2, centerZ, $"TajTerrace_{x}");
        }

        // Main palace body with a central arch opening.
        for (int y = baseY + 3; y <= baseY + 8; y++)
        {
            for (int x = -8; x <= 8; x++)
            {
                bool shell = x == -8 || x == 8 || y == baseY + 8 || y == baseY + 3;
                bool pillar = (Mathf.Abs(x) == 5 || Mathf.Abs(x) == 2) && y <= baseY + 7;
                bool centerArchVoid = Mathf.Abs(x) <= 1 && y >= baseY + 4 && y <= baseY + 6;
                if (centerArchVoid && !shell)
                {
                    continue;
                }

                if (shell || pillar)
                {
                    SpawnBlock(bodyPrefab, x, y, centerZ, $"TajBody_{x}_{y}");
                }
            }
        }

        // Central onion dome.
        int domeBaseY = baseY + 9;
        int domeHalf = 5;
        for (int layer = 0; layer <= 5; layer++)
        {
            int half = domeHalf - layer;
            int gy = domeBaseY + layer;
            for (int x = -half; x <= half; x++)
            {
                bool shell = layer < 5 ? Mathf.Abs(x) == half : true;
                if (!shell)
                {
                    continue;
                }

                SpawnBlock(bodyPrefab, x, gy, centerZ, $"TajDome_{x}_{gy}");
            }
        }

        SpawnBlock(highlightPrefab, 0, domeBaseY + 6, centerZ, "TajDome_Crescent");
        BuildSideDome(-6, baseY + 7, centerZ, bodyPrefab, highlightPrefab, "SideDomeL");
        BuildSideDome(6, baseY + 7, centerZ, bodyPrefab, highlightPrefab, "SideDomeR");

        BuildMinaret(-13, baseY + 1, centerZ, accentPrefab, highlightPrefab, "MinaretL");
        BuildMinaret(13, baseY + 1, centerZ, accentPrefab, highlightPrefab, "MinaretR");
    }

    private void BuildSideDome(int centerX, int baseY, int centerZ, GameObject bodyPrefab, GameObject capPrefab, string prefix)
    {
        for (int y = baseY - 2; y <= baseY; y++)
        {
            SpawnBlock(bodyPrefab, centerX, y, centerZ, $"{prefix}_Stem_{y}");
        }

        for (int layer = 0; layer <= 2; layer++)
        {
            int half = 2 - layer;
            int gy = baseY + layer;
            for (int x = centerX - half; x <= centerX + half; x++)
            {
                bool shell = layer < 2 ? x == centerX - half || x == centerX + half : true;
                if (!shell)
                {
                    continue;
                }

                SpawnBlock(bodyPrefab, x, gy, centerZ, $"{prefix}_{x}_{gy}");
            }
        }

        SpawnBlock(capPrefab, centerX, baseY + 3, centerZ, $"{prefix}_Cap");
    }

    private void BuildMinaret(int x, int baseY, int centerZ, GameObject shaftPrefab, GameObject capPrefab, string prefix)
    {
        for (int y = baseY; y <= baseY + 10; y++)
        {
            SpawnBlock(shaftPrefab, x, y, centerZ, $"{prefix}_Shaft_{y}");

            if (y == baseY + 3 || y == baseY + 7)
            {
                SpawnBlock(shaftPrefab, x - 1, y, centerZ, $"{prefix}_BalconyL_{y}");
                SpawnBlock(shaftPrefab, x + 1, y, centerZ, $"{prefix}_BalconyR_{y}");
            }
        }

        SpawnBlock(capPrefab, x, baseY + 11, centerZ, $"{prefix}_Cap1");
        SpawnBlock(capPrefab, x, baseY + 12, centerZ, $"{prefix}_Cap2");
    }

    private void BuildReflectingPool(GameObject borderPrefab, GameObject waterPrefab, int centerZ)
    {
        if (borderPrefab == null || waterPrefab == null)
        {
            return;
        }

        int zStart = 3;
        int zEnd = centerZ - 4;
        for (int z = zStart; z <= zEnd; z++)
        {
            for (int x = -3; x <= 3; x++)
            {
                bool border = Mathf.Abs(x) == 3 || z == zStart || z == zEnd;
                SpawnBlock(border ? borderPrefab : waterPrefab, x, 1, z, $"Pool_{x}_{z}");
            }
        }
    }

    private GameObject ResolveTopSurfacePrefab(int x, int z)
    {
        bool walkway = Mathf.Abs(x) <= 1 && z >= -groundBackDepth && z <= 12;
        bool walkwayEdge = Mathf.Abs(x) == 2 && z >= -2 && z <= 12;
        bool frontPlaza = Mathf.Abs(x) <= 5 && z >= 13 && z <= 18;
        bool checker = ((x + z) & 1) == 0;

        if (walkway)
        {
            return PickPrefab(
                metalBlockPrefab,
                cobblestonePrefab,
                minecraftCubePrefab,
                stoneBlockPrefab,
                brickBlockPrefab,
                grassBlockPrefab,
                dirtBlockPrefab,
                tntBlockPrefab);
        }

        if (walkwayEdge)
        {
            return PickPrefab(
                brickBlockPrefab,
                cobblestonePrefab,
                minecraftCubePrefab,
                stoneBlockPrefab,
                metalBlockPrefab,
                dirtBlockPrefab,
                grassBlockPrefab,
                tntBlockPrefab);
        }

        if (frontPlaza)
        {
            if (checker)
            {
                return PickPrefab(
                    cobblestonePrefab,
                    minecraftCubePrefab,
                    brickBlockPrefab,
                    stoneBlockPrefab,
                    metalBlockPrefab,
                    grassBlockPrefab,
                    dirtBlockPrefab,
                    tntBlockPrefab);
            }

            return PickPrefab(
                brickBlockPrefab,
                cobblestonePrefab,
                minecraftCubePrefab,
                stoneBlockPrefab,
                metalBlockPrefab,
                dirtBlockPrefab,
                grassBlockPrefab,
                tntBlockPrefab);
        }

        return checker
            ? PickPrefab(
                grassBlockPrefab,
                dirtBlockPrefab,
                minecraftCubePrefab,
                cobblestonePrefab,
                stoneBlockPrefab,
                brickBlockPrefab,
                metalBlockPrefab,
                tntBlockPrefab)
            : PickPrefab(
                dirtBlockPrefab,
                grassBlockPrefab,
                minecraftCubePrefab,
                cobblestonePrefab,
                stoneBlockPrefab,
                brickBlockPrefab,
                metalBlockPrefab,
                tntBlockPrefab);
    }

    private void BuildRunway()
    {
        for (int z = -4; z <= 9; z++)
        {
            for (int x = -4; x <= 4; x++)
            {
                bool border = Mathf.Abs(x) == 4 || z == -4 || z == 9;
                GameObject prefab = border
                    ? PickPrefab(
                        brickBlockPrefab,
                        cobblestonePrefab,
                        minecraftCubePrefab,
                        stoneBlockPrefab,
                        metalBlockPrefab,
                        dirtBlockPrefab,
                        grassBlockPrefab,
                        tntBlockPrefab)
                    : PickPrefab(
                        cobblestonePrefab,
                        minecraftCubePrefab,
                        stoneBlockPrefab,
                        brickBlockPrefab,
                        dirtBlockPrefab,
                        grassBlockPrefab,
                        metalBlockPrefab,
                        tntBlockPrefab);
                SpawnBlock(prefab, x, 1, z, $"Runway_{x}_{z}");
            }
        }
    }

    private void BuildPerformerPads()
    {
        int[] padX = { -3, 0, 3 };
        for (int i = 0; i < padX.Length; i++)
        {
            int cx = padX[i];
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    bool center = x == cx && z == 0;
                    GameObject prefab = center
                        ? PickPrefab(
                            crystalBlockPrefab,
                            metalBlockPrefab,
                            minecraftCubePrefab,
                            cobblestonePrefab,
                            stoneBlockPrefab,
                            brickBlockPrefab,
                            grassBlockPrefab,
                            dirtBlockPrefab,
                            tntBlockPrefab)
                        : PickPrefab(
                            metalBlockPrefab,
                            crystalBlockPrefab,
                            minecraftCubePrefab,
                            cobblestonePrefab,
                            stoneBlockPrefab,
                            brickBlockPrefab,
                            dirtBlockPrefab,
                            grassBlockPrefab,
                            tntBlockPrefab);
                    SpawnBlock(prefab, x, 1, z, $"Pad_{i}_{x}_{z}");
                }
            }

            SpawnBlock(
                PickPrefab(
                    crystalBlockPrefab,
                    metalBlockPrefab,
                    minecraftCubePrefab,
                    cobblestonePrefab,
                    stoneBlockPrefab,
                    brickBlockPrefab,
                    grassBlockPrefab,
                    dirtBlockPrefab,
                    tntBlockPrefab),
                cx,
                2,
                0,
                $"PadCore_{i}");
        }
    }

    private void BuildSidePillars()
    {
        int[] pillarX = { -5, 5 };
        int[] pillarZ = { 2, 7, 12 };
        for (int i = 0; i < pillarX.Length; i++)
        {
            for (int j = 0; j < pillarZ.Length; j++)
            {
                int baseX = pillarX[i];
                int baseZ = pillarZ[j];
                int height = 4 + j;
                for (int y = 1; y <= height; y++)
                {
                    SpawnBlock(
                        PickPrefab(
                            stoneBlockPrefab,
                            cobblestonePrefab,
                            minecraftCubePrefab,
                            brickBlockPrefab,
                            metalBlockPrefab,
                            dirtBlockPrefab,
                            grassBlockPrefab,
                            tntBlockPrefab),
                        baseX,
                        y,
                        baseZ,
                        $"Pillar_{i}_{j}_{y}");
                }

                SpawnBlock(
                    PickPrefab(
                        crystalBlockPrefab,
                        metalBlockPrefab,
                        minecraftCubePrefab,
                        cobblestonePrefab,
                        brickBlockPrefab,
                        stoneBlockPrefab,
                        grassBlockPrefab,
                        dirtBlockPrefab,
                        tntBlockPrefab),
                    baseX,
                    height + 1,
                    baseZ,
                    $"PillarTop_{i}_{j}");
            }
        }
    }

    private void BuildHeroArch()
    {
        int minX = -6;
        int maxX = 6;
        int minY = 1;
        int maxY = 8;

        for (int z = 8; z <= 9; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    bool outerFrame = x == minX || x == maxX || y == minY || y == maxY;
                    bool crownDetail = y >= maxY - 1 && Mathf.Abs(x) <= 1;
                    if (!outerFrame && !crownDetail)
                    {
                        continue;
                    }

                    GameObject prefab = crownDetail
                        ? PickPrefab(
                            crystalBlockPrefab,
                            metalBlockPrefab,
                            minecraftCubePrefab,
                            cobblestonePrefab,
                            brickBlockPrefab,
                            stoneBlockPrefab,
                            grassBlockPrefab,
                            dirtBlockPrefab,
                            tntBlockPrefab)
                        : PickPrefab(
                            metalBlockPrefab,
                            crystalBlockPrefab,
                            minecraftCubePrefab,
                            cobblestonePrefab,
                            brickBlockPrefab,
                            stoneBlockPrefab,
                            dirtBlockPrefab,
                            grassBlockPrefab,
                            tntBlockPrefab);
                    SpawnBlock(prefab, x, y, z, $"Arch_{x}_{y}_{z}");
                }
            }
        }
    }

    private void BuildCitadel()
    {
        int layers = 7;
        for (int layer = 0; layer < layers; layer++)
        {
            int half = 7 - layer;
            int y = 1 + layer;
            int zCenter = citadelCenterZ + layer;

            for (int x = -half; x <= half; x++)
            {
                for (int z = zCenter - half; z <= zCenter + half; z++)
                {
                    bool shell = x == -half || x == half || z == zCenter - half || z == zCenter + half;
                    bool cap = layer >= layers - 2;
                    if (!shell && !cap)
                    {
                        continue;
                    }

                    GameObject prefab = layer < 3
                        ? PickPrefab(
                            stoneBlockPrefab,
                            cobblestonePrefab,
                            minecraftCubePrefab,
                            brickBlockPrefab,
                            dirtBlockPrefab,
                            grassBlockPrefab,
                            metalBlockPrefab,
                            tntBlockPrefab)
                        : (layer < 5
                            ? PickPrefab(
                                brickBlockPrefab,
                                cobblestonePrefab,
                                minecraftCubePrefab,
                                stoneBlockPrefab,
                                metalBlockPrefab,
                                dirtBlockPrefab,
                                grassBlockPrefab,
                                tntBlockPrefab)
                            : PickPrefab(
                                metalBlockPrefab,
                                brickBlockPrefab,
                                cobblestonePrefab,
                                minecraftCubePrefab,
                                stoneBlockPrefab,
                                dirtBlockPrefab,
                                grassBlockPrefab,
                                tntBlockPrefab));
                    SpawnBlock(prefab, x, y, z, $"Citadel_{layer}_{x}_{z}");
                }
            }
        }

        for (int y = 8; y <= 10; y++)
        {
            SpawnBlock(
                PickPrefab(
                    crystalBlockPrefab,
                    metalBlockPrefab,
                    minecraftCubePrefab,
                    cobblestonePrefab,
                    brickBlockPrefab,
                    stoneBlockPrefab,
                    grassBlockPrefab,
                    dirtBlockPrefab,
                    tntBlockPrefab),
                0,
                y,
                citadelCenterZ + 6,
                $"Beacon_{y}");
        }
    }

    private void BuildSkyCrown()
    {
        int crownY = 11;
        int crownZ = citadelCenterZ + 6;
        for (int x = -2; x <= 2; x++)
        {
            SpawnBlock(
                PickPrefab(
                    crystalBlockPrefab,
                    metalBlockPrefab,
                    minecraftCubePrefab,
                    cobblestonePrefab,
                    brickBlockPrefab,
                    stoneBlockPrefab,
                    grassBlockPrefab,
                    dirtBlockPrefab,
                    tntBlockPrefab),
                x,
                crownY,
                crownZ,
                $"SkyRow_X_{x}");
        }

        for (int z = crownZ - 2; z <= crownZ + 2; z++)
        {
            SpawnBlock(
                PickPrefab(
                    crystalBlockPrefab,
                    metalBlockPrefab,
                    minecraftCubePrefab,
                    cobblestonePrefab,
                    brickBlockPrefab,
                    stoneBlockPrefab,
                    grassBlockPrefab,
                    dirtBlockPrefab,
                    tntBlockPrefab),
                0,
                crownY,
                z,
                $"SkyRow_Z_{z}");
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
