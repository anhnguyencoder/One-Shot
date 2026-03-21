using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class MinecraftTerrainBuilder : MonoBehaviour
{
    [Header("Terrain Shape")]
    [SerializeField, Min(6)] private int width = 18;
    [SerializeField, Min(6)] private int depth = 18;
    [SerializeField, Min(1)] private int baseDepth = 4;
    [SerializeField, Min(0)] private int surfaceVariation = 2;
    [SerializeField, Min(0)] private int plateauRadius = 2;
    [SerializeField, Min(0.05f)] private float noiseScale = 0.22f;
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int seed = 1357;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject grassBlockPrefab;
    [SerializeField] private GameObject dirtBlockPrefab;
    [SerializeField] private GameObject stoneBlockPrefab;
    [SerializeField] private GameObject coalBlockPrefab;

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("Egyptian Pyramids")]
    [SerializeField] private bool generateEgyptianPyramids = true;
    [SerializeField, Min(1)] private int pyramidCount = 3;
    [SerializeField, Min(6f)] private float firstPyramidDistance = 14f;
    [SerializeField, Min(2f)] private float distanceStep = 4f;
    [SerializeField, Min(2f)] private float lateralSpacing = 8f;
    [SerializeField, Min(5)] private int mainPyramidBaseSize = 13;
    [SerializeField, Min(1)] private int pyramidBaseStep = 2;
    [SerializeField] private bool carveMainPyramidEntrance = true;
    [SerializeField, Min(1)] private int entranceWidth = 3;
    [SerializeField, Min(1)] private int entranceHeight = 3;
    [SerializeField, Min(1)] private int entranceDepth = 2;

    private const string TerrainRootName = "MinecraftTerrain";
    private const string GrassPath = "Assets/Cube World Kit-zip/Grass Block/Block_Grass.fbx";
    private const string DirtPath = "Assets/Cube World Kit-zip/Dirt Block/Block_Dirt.fbx";
    private const string StonePath = "Assets/Cube World Kit-zip/Stone Block/Stone.fbx";
    private const string CoalPath = "Assets/Cube World Kit-zip/Coal Block/Block_Coal.fbx";

    private Transform terrainRoot;
    private float blockSizeX = 1f;
    private float blockSizeY = 1f;
    private float pivotToTop = 0.5f;

    private void Start()
    {
        if (Application.isPlaying && rebuildOnStart)
        {
            BuildTerrain();
        }
    }

    [ContextMenu("Build Minecraft Terrain")]
    public void BuildTerrain()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("MinecraftTerrainBuilder: Missing block prefabs.");
            return;
        }

        CaptureBlockMetrics();
        PrepareTerrainRoot();
        ClearTerrainChildren();
        GenerateTerrain();
        Physics.SyncTransforms();
        GenerateEgyptianPyramids();
        PlaceCharactersOnSurface();
    }

    [ContextMenu("Clear Minecraft Terrain")]
    public void ClearTerrain()
    {
        PrepareTerrainRoot();
        ClearTerrainChildren();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        TryAutoAssignPrefabs();
    }

    private void OnValidate()
    {
        width = Mathf.Max(6, width);
        depth = Mathf.Max(6, depth);
        baseDepth = Mathf.Max(1, baseDepth);
        surfaceVariation = Mathf.Max(0, surfaceVariation);
        plateauRadius = Mathf.Max(0, plateauRadius);
        noiseScale = Mathf.Max(0.05f, noiseScale);
        pyramidCount = Mathf.Max(1, pyramidCount);
        firstPyramidDistance = Mathf.Max(6f, firstPyramidDistance);
        distanceStep = Mathf.Max(2f, distanceStep);
        lateralSpacing = Mathf.Max(2f, lateralSpacing);
        mainPyramidBaseSize = Mathf.Max(5, mainPyramidBaseSize);
        pyramidBaseStep = Mathf.Max(1, pyramidBaseStep);
        entranceWidth = Mathf.Max(1, entranceWidth);
        entranceHeight = Mathf.Max(1, entranceHeight);
        entranceDepth = Mathf.Max(1, entranceDepth);
        TryAutoAssignPrefabs();
    }

    private bool TryAutoAssignPrefabs()
    {
        grassBlockPrefab = grassBlockPrefab != null ? grassBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(GrassPath);
        dirtBlockPrefab = dirtBlockPrefab != null ? dirtBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DirtPath);
        stoneBlockPrefab = stoneBlockPrefab != null ? stoneBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(StonePath);
        coalBlockPrefab = coalBlockPrefab != null ? coalBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(CoalPath);

        return grassBlockPrefab != null && dirtBlockPrefab != null && stoneBlockPrefab != null;
    }
#endif

    private bool TryResolvePrefabs()
    {
#if UNITY_EDITOR
        TryAutoAssignPrefabs();
#endif
        return grassBlockPrefab != null && dirtBlockPrefab != null && stoneBlockPrefab != null;
    }

    private void PrepareTerrainRoot()
    {
        if (terrainRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(TerrainRootName);
        if (existing != null)
        {
            terrainRoot = existing;
            return;
        }

        GameObject root = new GameObject(TerrainRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        terrainRoot = root.transform;
    }

    private void ClearTerrainChildren()
    {
        if (terrainRoot == null)
        {
            return;
        }

        for (int i = terrainRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = terrainRoot.GetChild(i);
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
        GameObject probe = Instantiate(grassBlockPrefab);
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

    private void GenerateTerrain()
    {
        float startX = -((width - 1) * 0.5f * blockSizeX);
        float startZ = -((depth - 1) * 0.5f * blockSizeX);
        int centerX = width / 2;
        int centerZ = depth / 2;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int topLevel = ResolveTopLevel(x, z, centerX, centerZ);

                for (int y = -baseDepth; y <= topLevel; y++)
                {
                    GameObject prefab = ResolveBlockPrefab(x, y, z, topLevel);
                    if (prefab == null)
                    {
                        continue;
                    }

                    GameObject block = Instantiate(prefab, terrainRoot);
                    float localX = startX + (x * blockSizeX);
                    float localY = (topSurfaceY - pivotToTop) + (y * blockSizeY);
                    float localZ = startZ + (z * blockSizeX);

                    block.transform.localPosition = new Vector3(localX, localY, localZ);
                    block.transform.localRotation = Quaternion.identity;
                    block.transform.localScale = Vector3.one;
                    block.name = $"{prefab.name}_{x}_{y}_{z}";

                    EnsureCollider(block);
                }
            }
        }
    }

    private void GenerateEgyptianPyramids()
    {
        if (!generateEgyptianPyramids || pyramidCount <= 0)
        {
            return;
        }

        int spawned = 0;
        int ring = 0;
        while (spawned < pyramidCount)
        {
            if (ring == 0)
            {
                SpawnPyramidFromCamera(0f, firstPyramidDistance, mainPyramidBaseSize, carveMainPyramidEntrance);
                spawned++;
                ring = 1;
                continue;
            }

            int ringBaseSize = Mathf.Max(5, mainPyramidBaseSize - (ring * pyramidBaseStep));
            float ringDistance = firstPyramidDistance + (ring * distanceStep);
            float ringLateral = ring * lateralSpacing;

            SpawnPyramidFromCamera(-ringLateral, ringDistance, ringBaseSize, false);
            spawned++;
            if (spawned >= pyramidCount)
            {
                break;
            }

            SpawnPyramidFromCamera(ringLateral, ringDistance, ringBaseSize, false);
            spawned++;
            ring++;
        }
    }

    private void SpawnPyramidFromCamera(float lateralOffset, float distance, int requestedBaseSize, bool carveEntrance)
    {
        if (requestedBaseSize < 5)
        {
            requestedBaseSize = 5;
        }

        int baseSize = requestedBaseSize % 2 == 0 ? requestedBaseSize + 1 : requestedBaseSize;
        int half = baseSize / 2;

        Vector3 localForward;
        float localGroundY;
        Vector3 centerLocal = ResolvePyramidCenterLocal(distance, lateralOffset, out localGroundY, out localForward);

        // Keep pyramid fully inside generated ground.
        float minX = -((width - 1) * 0.5f * blockSizeX) + (half * blockSizeX);
        float maxX = ((width - 1) * 0.5f * blockSizeX) - (half * blockSizeX);
        float minZ = -((depth - 1) * 0.5f * blockSizeX) + (half * blockSizeX);
        float maxZ = ((depth - 1) * 0.5f * blockSizeX) - (half * blockSizeX);

        centerLocal.x = Mathf.Clamp(centerLocal.x, minX, maxX);
        centerLocal.z = Mathf.Clamp(centerLocal.z, minZ, maxZ);
        BuildPyramid(centerLocal, localGroundY, baseSize, localForward, carveEntrance);
    }

    private Vector3 ResolvePyramidCenterLocal(float distance, float lateralOffset, out float localGroundY, out Vector3 localForward)
    {
        Camera cameraForPlacement = Camera.main;
        Vector3 worldSurfaceOrigin = transform.TransformPoint(new Vector3(0f, topSurfaceY, 0f));
        float defaultGroundWorldY = worldSurfaceOrigin.y;

        if (cameraForPlacement == null)
        {
            localGroundY = topSurfaceY;
            localForward = Vector3.forward;
            return new Vector3(lateralOffset, topSurfaceY, distance);
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(cameraForPlacement.transform.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }

        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
        Vector3 sampleWorld = cameraForPlacement.transform.position + (flatForward * distance) + (flatRight * lateralOffset);
        sampleWorld.y = cameraForPlacement.transform.position.y + 35f;

        float groundWorldY = defaultGroundWorldY;
        if (Physics.Raycast(sampleWorld, Vector3.down, out RaycastHit hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundWorldY = hitInfo.point.y;
        }

        Vector3 worldCenter = new Vector3(sampleWorld.x, groundWorldY, sampleWorld.z);
        localGroundY = terrainRoot.InverseTransformPoint(worldCenter).y;
        localForward = terrainRoot.InverseTransformDirection(flatForward).normalized;

        return terrainRoot.InverseTransformPoint(worldCenter);
    }

    private void BuildPyramid(Vector3 centerLocal, float groundTopLocalY, int baseSize, Vector3 localForward, bool carveEntrance)
    {
        int layers = (baseSize + 1) / 2;
        float pivotFromFloor = blockSizeY - pivotToTop;

        Vector3 forward = new Vector3(localForward.x, 0f, localForward.z);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);

        for (int layer = 0; layer < layers; layer++)
        {
            int layerSize = baseSize - (layer * 2);
            float half = (layerSize - 1) * 0.5f;
            float localY = groundTopLocalY + pivotFromFloor + (layer * blockSizeY);
            bool isCapLayer = layer == layers - 1;
            GameObject layerPrefab = isCapLayer ? stoneBlockPrefab : dirtBlockPrefab;

            for (int x = 0; x < layerSize; x++)
            {
                for (int z = 0; z < layerSize; z++)
                {
                    if (carveEntrance && ShouldSkipForEntrance(layer, x, z, layerSize))
                    {
                        continue;
                    }

                    float offsetRight = (x - half) * blockSizeX;
                    float offsetForward = (z - half) * blockSizeX;
                    Vector3 localPos = centerLocal + (right * offsetRight) + (forward * offsetForward);
                    localPos.y = localY;

                    SpawnPyramidBlock(layerPrefab, localPos, x, z, layer);
                }
            }
        }
    }

    private bool ShouldSkipForEntrance(int layer, int x, int z, int layerSize)
    {
        if (layer >= entranceHeight)
        {
            return false;
        }

        int half = layerSize / 2;
        int halfEntranceWidth = Mathf.Max(0, (entranceWidth - 1) / 2);
        bool inWidth = Mathf.Abs(x - half) <= halfEntranceWidth;
        bool inDepth = z < entranceDepth;
        return inWidth && inDepth;
    }

    private void SpawnPyramidBlock(GameObject prefab, Vector3 localPosition, int x, int z, int layer)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject block = Instantiate(prefab, terrainRoot);
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = Vector3.one;
        block.name = $"{prefab.name}_Pyramid_L{layer}_{x}_{z}";
        EnsureCollider(block);
    }

    private int ResolveTopLevel(int x, int z, int centerX, int centerZ)
    {
        if (Mathf.Abs(x - centerX) <= plateauRadius && Mathf.Abs(z - centerZ) <= plateauRadius)
        {
            return 0;
        }

        float noise = Mathf.PerlinNoise((x + seed) * noiseScale, (z + seed * 2) * noiseScale);
        int offset = Mathf.RoundToInt((noise - 0.5f) * 2f * surfaceVariation);
        return Mathf.Clamp(offset, -surfaceVariation, surfaceVariation);
    }

    private GameObject ResolveBlockPrefab(int x, int y, int z, int topLevel)
    {
        int depthFromTop = topLevel - y;
        if (depthFromTop == 0)
        {
            return grassBlockPrefab;
        }

        if (depthFromTop <= 2)
        {
            return dirtBlockPrefab;
        }

        if (coalBlockPrefab != null && depthFromTop > 2 && Hash01(x, y, z, seed) > 0.92f)
        {
            return coalBlockPrefab;
        }

        return stoneBlockPrefab;
    }

    private void EnsureCollider(GameObject block)
    {
        if (!addBoxColliderIfMissing || block.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        if (!TryGetWorldBounds(block, out Bounds worldBounds))
        {
            return;
        }

        BoxCollider box = block.AddComponent<BoxCollider>();
        Vector3 center = block.transform.InverseTransformPoint(worldBounds.center);
        Vector3 size = worldBounds.size;

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

    private void PlaceCharactersOnSurface()
    {
        Animator[] actors = GetComponentsInChildren<Animator>();
        for (int i = 0; i < actors.Length; i++)
        {
            Animator actor = actors[i];
            if (terrainRoot != null && actor.transform.IsChildOf(terrainRoot))
            {
                continue;
            }

            if (!TryGetWorldBounds(actor.gameObject, out Bounds actorBounds))
            {
                continue;
            }

            float feetOffset = actor.transform.position.y - actorBounds.min.y;
            Vector3 position = actor.transform.position;
            position.y = transform.TransformPoint(new Vector3(0f, topSurfaceY, 0f)).y + feetOffset;
            actor.transform.position = position;
        }
    }

    private static bool TryGetWorldBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
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

    private static float Hash01(int x, int y, int z, int baseSeed)
    {
        unchecked
        {
            int hash = baseSeed;
            hash = (hash * 397) ^ x;
            hash = (hash * 397) ^ y;
            hash = (hash * 397) ^ z;
            hash ^= (hash >> 16);
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
