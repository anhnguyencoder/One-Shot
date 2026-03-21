using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class FrozenCitadelLevelBuilder : MonoBehaviour
{
    [Header("Terrain Shape")]
    [SerializeField, Min(10)] private int width = 34;
    [SerializeField, Min(10)] private int depth = 38;
    [SerializeField, Min(1)] private int baseDepth = 3;
    [SerializeField, Min(0)] private int surfaceVariation = 1;
    [SerializeField, Min(0)] private int plateauRadius = 4;
    [SerializeField, Min(0.05f)] private float noiseScale = 0.2f;
    [SerializeField] private float topSurfaceY = 0f;
    [SerializeField] private int seed = 2026;

    [Header("Frozen Citadel")]
    [SerializeField, Min(7)] private int citadelBaseSize = 15;
    [SerializeField, Min(2)] private int citadelLayers = 5;
    [SerializeField, Min(5f)] private float citadelDistanceFromCamera = 13f;
    [SerializeField, Min(3)] private int stairWidth = 7;
    [SerializeField, Min(2)] private int stairLength = 5;
    [SerializeField, Min(4)] private int crystalPillarCount = 6;
    [SerializeField, Min(2)] private int crystalPillarMinHeight = 4;
    [SerializeField, Min(2)] private int crystalPillarMaxHeight = 7;
    [SerializeField, Min(4f)] private float pillarRingRadius = 11f;

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool addBoxColliderIfMissing = true;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject snowBlockPrefab;
    [SerializeField] private GameObject iceBlockPrefab;
    [SerializeField] private GameObject greyBricksPrefab;
    [SerializeField] private GameObject crystalBlockPrefab;

    private const string TerrainRootName = "FrozenCitadelTerrain";
    private const string SnowPath = "Assets/Cube World Kit-zip/Snow Block/Block_Snow.fbx";
    private const string IcePath = "Assets/Cube World Kit-zip/Ice Block/Block_Ice.fbx";
    private const string GreyBricksPath = "Assets/Cube World Kit-zip/Grey Bricks/Block_GreyBricks.fbx";
    private const string CrystalPath = "Assets/Cube World Kit-zip/Crystal Block/Block_Crystal.fbx";

    private Transform terrainRoot;
    private float blockSizeX = 1f;
    private float blockSizeY = 1f;
    private float pivotToTop = 0.5f;

    private void Start()
    {
        if (Application.isPlaying && rebuildOnStart)
        {
            BuildLevel();
        }
    }

    [ContextMenu("Build Frozen Citadel")]
    public void BuildLevel()
    {
        if (!TryResolvePrefabs())
        {
            Debug.LogError("FrozenCitadelLevelBuilder: Missing block prefabs.");
            return;
        }

        CaptureBlockMetrics();
        PrepareTerrainRoot();
        ClearTerrainChildren();
        GenerateTerrain();
        Physics.SyncTransforms();
        GenerateCitadelLandmark();
        GenerateCrystalPillars();
        PlaceCharactersOnSurface();
    }

    [ContextMenu("Clear Frozen Citadel")]
    public void ClearLevel()
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
        width = Mathf.Max(10, width);
        depth = Mathf.Max(10, depth);
        baseDepth = Mathf.Max(1, baseDepth);
        surfaceVariation = Mathf.Max(0, surfaceVariation);
        plateauRadius = Mathf.Max(0, plateauRadius);
        noiseScale = Mathf.Max(0.05f, noiseScale);

        citadelBaseSize = Mathf.Max(7, citadelBaseSize);
        if (citadelBaseSize % 2 == 0) citadelBaseSize += 1;
        citadelLayers = Mathf.Max(2, citadelLayers);
        citadelDistanceFromCamera = Mathf.Max(5f, citadelDistanceFromCamera);
        stairWidth = Mathf.Max(3, stairWidth);
        if (stairWidth % 2 == 0) stairWidth += 1;
        stairLength = Mathf.Max(2, stairLength);
        crystalPillarCount = Mathf.Max(4, crystalPillarCount);
        crystalPillarMinHeight = Mathf.Max(2, crystalPillarMinHeight);
        crystalPillarMaxHeight = Mathf.Max(crystalPillarMinHeight, crystalPillarMaxHeight);
        pillarRingRadius = Mathf.Max(4f, pillarRingRadius);

        TryAutoAssignPrefabs();
    }

    private bool TryAutoAssignPrefabs()
    {
        snowBlockPrefab = snowBlockPrefab != null ? snowBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(SnowPath);
        iceBlockPrefab = iceBlockPrefab != null ? iceBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(IcePath);
        greyBricksPrefab = greyBricksPrefab != null ? greyBricksPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(GreyBricksPath);
        crystalBlockPrefab = crystalBlockPrefab != null ? crystalBlockPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPath);

        return snowBlockPrefab != null && iceBlockPrefab != null && greyBricksPrefab != null && crystalBlockPrefab != null;
    }
#endif

    private bool TryResolvePrefabs()
    {
#if UNITY_EDITOR
        TryAutoAssignPrefabs();
#endif
        return snowBlockPrefab != null && iceBlockPrefab != null && greyBricksPrefab != null && crystalBlockPrefab != null;
    }

    private void CaptureBlockMetrics()
    {
        GameObject probe = Instantiate(snowBlockPrefab);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;
        probe.transform.localScale = Vector3.one;

        if (TryGetWorldBounds(probe, out Bounds bounds))
        {
            blockSizeX = Mathf.Max(0.01f, bounds.size.x);
            blockSizeY = Mathf.Max(0.01f, bounds.size.y);
            pivotToTop = bounds.max.y - probe.transform.position.y;
            if (pivotToTop <= 0f) pivotToTop = blockSizeY * 0.5f;
        }
        else
        {
            blockSizeX = 1f;
            blockSizeY = 1f;
            pivotToTop = 0.5f;
        }

        if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);
    }

    private void PrepareTerrainRoot()
    {
        if (terrainRoot != null) return;

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
        if (terrainRoot == null) return;

        for (int i = terrainRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = terrainRoot.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
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
                    GameObject prefab = ResolveTerrainBlockPrefab(topLevel - y);
                    GameObject block = Instantiate(prefab, terrainRoot);

                    float localX = startX + (x * blockSizeX);
                    float localY = (topSurfaceY - pivotToTop) + (y * blockSizeY);
                    float localZ = startZ + (z * blockSizeX);

                    block.transform.localPosition = new Vector3(localX, localY, localZ);
                    block.transform.localRotation = Quaternion.identity;
                    block.transform.localScale = Vector3.one;
                    block.name = $"{prefab.name}_FZ_{x}_{y}_{z}";

                    EnsureCollider(block);
                }
            }
        }
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

    private GameObject ResolveTerrainBlockPrefab(int depthFromTop)
    {
        if (depthFromTop <= 0) return snowBlockPrefab;
        if (depthFromTop <= 2) return iceBlockPrefab;
        return greyBricksPrefab;
    }

    private void GenerateCitadelLandmark()
    {
        Vector3 localForward;
        float groundY;
        Vector3 center = ResolveLandmarkCenterLocal(citadelDistanceFromCamera, 0f, out groundY, out localForward);
        ClampCenterInsideTerrain(ref center, citadelBaseSize);

        float floorY = groundY + (blockSizeY - pivotToTop);
        int layers = Mathf.Min(citadelLayers, (citadelBaseSize + 1) / 2);

        for (int layer = 0; layer < layers; layer++)
        {
            int layerSize = citadelBaseSize - (layer * 2);
            if (layerSize < 3) break;

            float half = (layerSize - 1) * 0.5f;
            float y = floorY + (layer * blockSizeY);
            GameObject layerPrefab = layer <= 1 ? greyBricksPrefab : iceBlockPrefab;

            for (int x = 0; x < layerSize; x++)
            {
                for (int z = 0; z < layerSize; z++)
                {
                    Vector3 localPos = center + new Vector3((x - half) * blockSizeX, y - center.y, (z - half) * blockSizeX);
                    localPos.y = y;
                    SpawnLandmarkBlock(layerPrefab, localPos, $"Citadel_{layer}_{x}_{z}");
                }
            }
        }

        BuildFrontStairs(center, floorY, localForward);
    }

    private void BuildFrontStairs(Vector3 center, float floorY, Vector3 localForward)
    {
        Vector3 forward = new Vector3(localForward.x, 0f, localForward.z).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Vector3 faceCameraDir = -forward;
        Vector3 right = new Vector3(faceCameraDir.z, 0f, -faceCameraDir.x);

        float halfBase = (citadelBaseSize - 1) * 0.5f;
        float halfStair = (stairWidth - 1) * 0.5f;

        for (int step = 0; step < stairLength; step++)
        {
            float y = floorY + (step * blockSizeY);
            float forwardOffset = (halfBase + step + 1) * blockSizeX;

            for (int w = 0; w < stairWidth; w++)
            {
                float side = (w - halfStair) * blockSizeX;
                Vector3 localPos = center + (faceCameraDir * forwardOffset) + (right * side);
                localPos.y = y;
                SpawnLandmarkBlock(iceBlockPrefab, localPos, $"Stair_{step}_{w}");
            }
        }
    }

    private void GenerateCrystalPillars()
    {
        Vector3 localForward;
        float groundY;
        Vector3 center = ResolveLandmarkCenterLocal(citadelDistanceFromCamera, 0f, out groundY, out localForward);
        ClampCenterInsideTerrain(ref center, citadelBaseSize);

        Vector3 flatForward = new Vector3(localForward.x, 0f, localForward.z);
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        flatForward.Normalize();
        Vector3 flatRight = new Vector3(flatForward.z, 0f, -flatForward.x);

        float baseY = groundY + (blockSizeY - pivotToTop);
        float safeRadius = Mathf.Max(pillarRingRadius, (citadelBaseSize * 0.5f + 2f) * blockSizeX);

        for (int i = 0; i < crystalPillarCount; i++)
        {
            float t = (float)i / crystalPillarCount;
            float angle = t * Mathf.PI * 2f;
            Vector3 ringOffset = (flatRight * Mathf.Cos(angle) + flatForward * Mathf.Sin(angle)) * safeRadius;
            Vector3 basePos = center + ringOffset;
            ClampCenterInsideTerrain(ref basePos, 1);

            float jitter = Hash01(i, seed, crystalPillarCount, 77);
            int height = Mathf.RoundToInt(Mathf.Lerp(crystalPillarMinHeight, crystalPillarMaxHeight, jitter));

            for (int h = 0; h < height; h++)
            {
                Vector3 localPos = new Vector3(basePos.x, baseY + (h * blockSizeY), basePos.z);
                GameObject prefab = h == 0 ? iceBlockPrefab : crystalBlockPrefab;
                SpawnLandmarkBlock(prefab, localPos, $"Crystal_{i}_{h}");
            }
        }
    }

    private Vector3 ResolveLandmarkCenterLocal(float distance, float lateralOffset, out float localGroundY, out Vector3 localForward)
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
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

        Vector3 sampleWorld = cameraForPlacement.transform.position + (flatForward * distance) + (flatRight * lateralOffset);
        sampleWorld.y = cameraForPlacement.transform.position.y + 40f;

        float groundWorldY = defaultGroundWorldY;
        if (Physics.Raycast(sampleWorld, Vector3.down, out RaycastHit hit, 140f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundWorldY = hit.point.y;
        }

        Vector3 worldCenter = new Vector3(sampleWorld.x, groundWorldY, sampleWorld.z);
        localGroundY = terrainRoot.InverseTransformPoint(worldCenter).y;
        localForward = terrainRoot.InverseTransformDirection(flatForward).normalized;
        return terrainRoot.InverseTransformPoint(worldCenter);
    }

    private void ClampCenterInsideTerrain(ref Vector3 centerLocal, int halfExtentInBlocks)
    {
        float halfX = (width - 1) * 0.5f * blockSizeX;
        float halfZ = (depth - 1) * 0.5f * blockSizeX;
        float margin = halfExtentInBlocks * blockSizeX;

        centerLocal.x = Mathf.Clamp(centerLocal.x, -halfX + margin, halfX - margin);
        centerLocal.z = Mathf.Clamp(centerLocal.z, -halfZ + margin, halfZ - margin);
    }

    private void SpawnLandmarkBlock(GameObject prefab, Vector3 localPosition, string suffix)
    {
        if (prefab == null) return;

        GameObject block = Instantiate(prefab, terrainRoot);
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = Vector3.one;
        block.name = $"{prefab.name}_{suffix}";
        EnsureCollider(block);
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

        box.center = center;
        box.size = new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));
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

    private static float Hash01(int a, int b, int c, int d)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + a;
            hash = hash * 31 + b;
            hash = hash * 31 + c;
            hash = hash * 31 + d;
            hash ^= (hash >> 16);
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
