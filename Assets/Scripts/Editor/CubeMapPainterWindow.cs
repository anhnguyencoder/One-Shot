using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CubeMapPainterWindow : EditorWindow
{
    private const string WindowTitle = "Công Cụ Xếp Cube";
    private const string DefaultRootName = "CubePaintRoot";
    private const string DefaultCobblestonePath = "Assets/Prefabs/Coblstone.prefab";
    private const string DefaultMinecraftCubePath = "Assets/Prefabs/minecraft_cube.prefab";

    [SerializeField] private bool toolEnabled = true;
    [SerializeField] private bool paintWhileDragging = true;
    [SerializeField] private bool replaceCubeInSameCell = false;
    [SerializeField] private bool eraseOnlyUnderRoot = true;
    [SerializeField] private bool minecraftPlacementMode = true;
    [SerializeField] private bool highlightTargetFace = true;
    [SerializeField] private bool highlightObjectsOnCurrentLayer = true;
    [SerializeField] private bool normalizePrefabSizeToCell = true;
    [SerializeField] private bool showHeightLayer = true;
    [SerializeField] private int verticalCellOffsetSteps;
    [SerializeField] private float heightLayerSize = 18f;
    [SerializeField] private float syncScale = 1f;
    [SerializeField] private bool useSelectedPrefabSize = true;
    [SerializeField] private bool compensatePrefabPivotOffset = true;
    [SerializeField] private Transform cubeRoot;
    [SerializeField] private Vector3 gridSize = Vector3.one;
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private GameObject[] cubePrefabs = new GameObject[4];
    [SerializeField] private int selectedCubeIndex;

    private Vector3 previewPosition;
    private Vector3Int previewCell;
    private bool hasPreviewFace;
    private Vector3 previewHitCubeCenter;
    private Vector3 previewHitCubeSize = Vector3.one;
    private Vector3 previewPlacementAxis = Vector3.up;
    private bool qStepKeyHeld;
    private bool eStepKeyHeld;
    private Vector3Int lastPaintCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    private GameObject cachedMetricsPrefab;
    private bool hasCachedMetrics;
    private Vector3 cachedPrefabCellSize = Vector3.one;
    private Vector3 cachedPrefabCenterOffset = Vector3.zero;

    [MenuItem("Tools/Công Cụ Xếp Cube")]
    private static void OpenWindow()
    {
        CubeMapPainterWindow window = GetWindow<CubeMapPainterWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(320f, 360f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EnsurePaletteHasDefaults();
        EnsureSelectionIndexInRange();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Xếp cube trực tiếp trong Scene view", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Ctrl + chuột trái để đặt, Shift + chuột trái để xóa. Nhấn Q tăng độ cao đặt, E giảm độ cao. Giữ Alt để xoay camera.", MessageType.Info);

        toolEnabled = EditorGUILayout.ToggleLeft("Bật Tool Xếp", toolEnabled);
        paintWhileDragging = EditorGUILayout.ToggleLeft("Vẽ Khi Kéo Chuột", paintWhileDragging);
        replaceCubeInSameCell = EditorGUILayout.ToggleLeft("Thay Cube Ở Cùng Ô", replaceCubeInSameCell);
        eraseOnlyUnderRoot = EditorGUILayout.ToggleLeft("Chỉ Xóa Trong Nhóm Gốc Cube", eraseOnlyUnderRoot);
        minecraftPlacementMode = EditorGUILayout.ToggleLeft("Đặt Kiểu Minecraft (kề mặt)", minecraftPlacementMode);
        highlightTargetFace = EditorGUILayout.ToggleLeft("Làm Sáng Mặt Đặt", highlightTargetFace);
        highlightObjectsOnCurrentLayer = EditorGUILayout.ToggleLeft("Làm Sáng Object Cùng Tầng", highlightObjectsOnCurrentLayer);
        normalizePrefabSizeToCell = EditorGUILayout.ToggleLeft("Chuẩn Hóa Prefab Về Cùng Cỡ", normalizePrefabSizeToCell);
        showHeightLayer = EditorGUILayout.ToggleLeft("Hiển Thị Layer Cao Độ", showHeightLayer);
        verticalCellOffsetSteps = EditorGUILayout.IntField("Độ Cao Theo Ô (Q/E)", verticalCellOffsetSteps);
        if (GUILayout.Button("Reset Độ Cao"))
        {
            verticalCellOffsetSteps = 0;
        }
        EditorGUILayout.LabelField($"Độ Cao Hiện Tại: {verticalCellOffsetSteps} ô");

        EditorGUILayout.Space(6f);
        DrawRootSection();

        EditorGUILayout.Space(6f);
        DrawGridSection();

        EditorGUILayout.Space(6f);
        DrawPaletteSection();
    }

    private void DrawRootSection()
    {
        EditorGUILayout.LabelField("Nhóm Gốc", EditorStyles.boldLabel);
        cubeRoot = (Transform)EditorGUILayout.ObjectField("Nhóm Gốc Cube", cubeRoot, typeof(Transform), true);

        if (GUILayout.Button("Tạo hoặc dùng Nhóm Gốc Cube trong Scene"))
        {
            cubeRoot = EnsureCubeRoot();
            if (cubeRoot != null)
            {
                Selection.activeTransform = cubeRoot;
            }
        }
    }

    private void DrawGridSection()
    {
        EditorGUILayout.LabelField("Lưới", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(normalizePrefabSizeToCell);
        useSelectedPrefabSize = EditorGUILayout.ToggleLeft("Dùng Kích Thước Prefab Đang Chọn", useSelectedPrefabSize);
        EditorGUI.EndDisabledGroup();
        compensatePrefabPivotOffset = EditorGUILayout.ToggleLeft("Bù Lệch Pivot Prefab", compensatePrefabPivotOffset);
        gridSize = EditorGUILayout.Vector3Field("Kích Thước Lưới", gridSize);
        gridOrigin = EditorGUILayout.Vector3Field("Gốc Lưới", gridOrigin);
        verticalOffset = EditorGUILayout.FloatField("Độ Cao Bù", verticalOffset);
        heightLayerSize = EditorGUILayout.FloatField("Kích Thước Layer Cao Độ", heightLayerSize);
        syncScale = EditorGUILayout.FloatField("Tỷ Lệ Đồng Bộ (x)", syncScale);
        if (GUILayout.Button("Nhanh 0.5x"))
        {
            syncScale = 0.5f;
        }

        gridSize.x = Mathf.Max(0.01f, gridSize.x);
        gridSize.y = Mathf.Max(0.01f, gridSize.y);
        gridSize.z = Mathf.Max(0.01f, gridSize.z);
        heightLayerSize = Mathf.Max(2f, heightLayerSize);
        syncScale = Mathf.Max(0.01f, syncScale);

        if (normalizePrefabSizeToCell)
        {
            EditorGUILayout.HelpBox("Mọi prefab sẽ được scale về đúng kích thước ô đang dùng.", MessageType.Info);
        }

        Vector3 activeCellSize = GetPlacementCellSize();
        EditorGUILayout.LabelField(
            "Kích Thước Ô Đang Dùng",
            $"{activeCellSize.x:F3}, {activeCellSize.y:F3}, {activeCellSize.z:F3}");
    }

    private void DrawPaletteSection()
    {
        EditorGUILayout.LabelField("Bảng Cube", EditorStyles.boldLabel);

        int newSize = EditorGUILayout.IntSlider("Số Ô Palette", cubePrefabs.Length, 1, 8);
        if (newSize != cubePrefabs.Length)
        {
            Array.Resize(ref cubePrefabs, newSize);
            EnsureSelectionIndexInRange();
        }

        for (int i = 0; i < cubePrefabs.Length; i++)
        {
            cubePrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"Cube {i + 1}", cubePrefabs[i], typeof(GameObject), false);
        }

        if (GUILayout.Button("Tự Điền Từ Assets/Prefabs"))
        {
            EnsurePaletteHasDefaults(forceRefresh: true);
        }

        EnsureSelectionIndexInRange();
        selectedCubeIndex = GUILayout.Toolbar(selectedCubeIndex, BuildPaletteLabels());

        GameObject selectedPrefab = GetSelectedPrefab();
        string selectedLabel = selectedPrefab == null ? "None" : selectedPrefab.name;
        EditorGUILayout.LabelField("Cube Đang Chọn", selectedLabel);

        if (selectedPrefab == null)
        {
            EditorGUILayout.HelpBox("Hãy chọn ít nhất một cube prefab trước khi vẽ.", MessageType.Warning);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!toolEnabled)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.alt)
        {
            return;
        }

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Q)
        {
            if (!qStepKeyHeld)
            {
                verticalCellOffsetSteps += 1;
                Repaint();
                SceneView.RepaintAll();
            }

            qStepKeyHeld = true;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.KeyUp && currentEvent.keyCode == KeyCode.Q)
        {
            qStepKeyHeld = false;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.E)
        {
            if (!eStepKeyHeld)
            {
                verticalCellOffsetSteps -= 1;
                Repaint();
                SceneView.RepaintAll();
            }

            eStepKeyHeld = true;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.KeyUp && currentEvent.keyCode == KeyCode.E)
        {
            eStepKeyHeld = false;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp)
        {
            lastPaintCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        }

        if (!TryGetPointerHit(currentEvent.mousePosition, out Vector3 hitPoint, out Vector3 hitNormal, out Transform hitTransform))
        {
            return;
        }

        bool forceTopPlacement = false;
        Vector3 cellSize = GetPlacementCellSize();
        previewPosition = CalculatePlacementPosition(
            hitPoint,
            hitNormal,
            hitTransform,
            cellSize,
            forceTopPlacement,
            out previewPlacementAxis,
            out hasPreviewFace,
            out previewHitCubeCenter,
            out previewHitCubeSize);
        previewPosition = ApplyVerticalCellOffset(previewPosition, cellSize);
        previewCell = WorldToCell(previewPosition, cellSize);

        DrawPreviewCube(cellSize);

        bool eraseMode = currentEvent.shift;
        bool placeMode = currentEvent.control;
        bool allowActionThisEvent = eraseMode || placeMode;
        bool canPaintThisEvent =
            allowActionThisEvent &&
            currentEvent.button == 0 &&
            (currentEvent.type == EventType.MouseDown || (paintWhileDragging && currentEvent.type == EventType.MouseDrag));

        if (!canPaintThisEvent)
        {
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (previewCell == lastPaintCell)
        {
            currentEvent.Use();
            return;
        }

        lastPaintCell = previewCell;

        if (eraseMode)
        {
            EraseAtPosition(previewPosition, hitTransform, cellSize);
        }
        else
        {
            PlaceAtPosition(previewPosition, cellSize);
        }

        currentEvent.Use();
    }

    private void DrawPreviewCube(Vector3 cellSize)
    {
        if (showHeightLayer)
        {
            DrawHeightLayerOverlay();
        }

        if (highlightObjectsOnCurrentLayer)
        {
            DrawCurrentLayerObjectHighlights(cellSize);
        }

        Handles.color = new Color(0.2f, 1f, 0.4f, 0.95f);
        Handles.DrawWireCube(previewPosition, cellSize * 0.98f);
        Handles.color = new Color(0.2f, 1f, 0.4f, 0.08f);
        Handles.CubeHandleCap(0, previewPosition, Quaternion.identity, Mathf.Max(cellSize.x, cellSize.y, cellSize.z), EventType.Repaint);

        if (highlightTargetFace && hasPreviewFace)
        {
            DrawHighlightedFaceOverlay(previewHitCubeCenter, previewHitCubeSize, previewPlacementAxis);
        }
    }

    private void DrawHeightLayerOverlay()
    {
        float half = Mathf.Max(1f, heightLayerSize) * 0.5f;
        Vector3 center = cubeRoot != null ? cubeRoot.position : gridOrigin;
        float y = previewPosition.y + 0.01f;

        Vector3[] corners =
        {
            new Vector3(center.x - half, y, center.z - half),
            new Vector3(center.x + half, y, center.z - half),
            new Vector3(center.x + half, y, center.z + half),
            new Vector3(center.x - half, y, center.z + half)
        };

        Handles.DrawSolidRectangleWithOutline(
            corners,
            new Color(0.2f, 0.75f, 1f, 0.07f),
            new Color(0.2f, 0.75f, 1f, 0.55f));

        Vector3 labelPos = corners[0] + new Vector3(0f, 0.08f, 0f);
        Handles.Label(labelPos, $"Layer Y: {previewPosition.y:F2} | Offset: {verticalCellOffsetSteps}");
    }

    private void DrawCurrentLayerObjectHighlights(Vector3 cellSize)
    {
        if (cubeRoot == null)
        {
            return;
        }

        int targetLayerY = previewCell.y;
        Color fillColor = new Color(1f, 0.88f, 0.2f, 0.10f);
        Color wireColor = new Color(1f, 0.9f, 0.25f, 0.95f);

        for (int i = 0; i < cubeRoot.childCount; i++)
        {
            Transform child = cubeRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Vector3 center;
            Vector3 size;
            if (!TryGetWorldBoundsCenterAndSize(child.gameObject, out center, out size))
            {
                center = child.position;
                size = cellSize;
            }

            int childLayerY = Mathf.RoundToInt((center.y - gridOrigin.y) / cellSize.y);
            if (childLayerY != targetLayerY)
            {
                continue;
            }

            Handles.color = wireColor;
            Handles.DrawWireCube(center, size * 1.02f);
            Handles.color = fillColor;
            Handles.CubeHandleCap(0, center, Quaternion.identity, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 1.02f, EventType.Repaint);
        }
    }

    private static void DrawHighlightedFaceOverlay(Vector3 cubeCenter, Vector3 cubeSize, Vector3 axis)
    {
        Vector3 normal = SnapNormalToAxis(axis);
        if (normal == Vector3.zero)
        {
            return;
        }

        float halfX = cubeSize.x * 0.5f;
        float halfY = cubeSize.y * 0.5f;
        float halfZ = cubeSize.z * 0.5f;

        Vector3 faceCenter;
        Vector3 tangent;
        Vector3 bitangent;
        float halfTangent;
        float halfBitangent;

        if (!Mathf.Approximately(normal.x, 0f))
        {
            faceCenter = cubeCenter + new Vector3(Mathf.Sign(normal.x) * halfX, 0f, 0f);
            tangent = Vector3.up;
            bitangent = Vector3.forward;
            halfTangent = halfY;
            halfBitangent = halfZ;
        }
        else if (!Mathf.Approximately(normal.y, 0f))
        {
            faceCenter = cubeCenter + new Vector3(0f, Mathf.Sign(normal.y) * halfY, 0f);
            tangent = Vector3.right;
            bitangent = Vector3.forward;
            halfTangent = halfX;
            halfBitangent = halfZ;
        }
        else
        {
            faceCenter = cubeCenter + new Vector3(0f, 0f, Mathf.Sign(normal.z) * halfZ);
            tangent = Vector3.right;
            bitangent = Vector3.up;
            halfTangent = halfX;
            halfBitangent = halfY;
        }

        float zFightOffset = Mathf.Max(0.005f, Mathf.Min(cubeSize.x, Mathf.Min(cubeSize.y, cubeSize.z)) * 0.02f);
        faceCenter += normal * zFightOffset;

        Vector3[] corners =
        {
            faceCenter + tangent * halfTangent + bitangent * halfBitangent,
            faceCenter + tangent * halfTangent - bitangent * halfBitangent,
            faceCenter - tangent * halfTangent - bitangent * halfBitangent,
            faceCenter - tangent * halfTangent + bitangent * halfBitangent
        };

        Handles.DrawSolidRectangleWithOutline(
            corners,
            new Color(1f, 0.85f, 0.2f, 0.18f),
            new Color(1f, 0.92f, 0.3f, 0.95f));
    }

    private void PlaceAtPosition(Vector3 worldCellCenter, Vector3 cellSize)
    {
        GameObject selectedPrefab = GetSelectedPrefab();
        if (selectedPrefab == null)
        {
            return;
        }

        if (cubeRoot == null)
        {
            cubeRoot = EnsureCubeRoot();
        }

        Transform existing = FindCubeAtWorldPosition(worldCellCenter, cellSize);
        if (existing != null)
        {
            if (!replaceCubeInSameCell)
            {
                Selection.activeTransform = existing;
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Scene targetScene = cubeRoot != null ? cubeRoot.gameObject.scene : SceneManager.GetActiveScene();
        GameObject newCube = PrefabUtility.InstantiatePrefab(selectedPrefab, targetScene) as GameObject;
        if (newCube == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(newCube, "Đặt Cube");

        if (cubeRoot != null)
        {
            Undo.SetTransformParent(newCube.transform, cubeRoot, "Gán Parent Cube");
        }

        if (normalizePrefabSizeToCell && TryGetSelectedPrefabMetrics(out Vector3 sourceSize, out _))
        {
            Vector3 targetSize = GetPlacementCellSize();
            Vector3 ratio = GetScaleRatio(sourceSize, targetSize);
            newCube.transform.localScale = Vector3.Scale(newCube.transform.localScale, ratio);
        }
        else
        {
            float activeScale = Mathf.Max(0.01f, syncScale);
            newCube.transform.localScale = Vector3.Scale(newCube.transform.localScale, Vector3.one * activeScale);
        }

        Vector3 rootOffset = GetSelectedPrefabCenterOffset();
        newCube.transform.position = worldCellCenter - rootOffset;

        Selection.activeGameObject = newCube;
        EditorSceneManager.MarkSceneDirty(newCube.scene);
    }

    private void EraseAtPosition(Vector3 worldCellCenter, Transform hitTransform, Vector3 cellSize)
    {
        Transform eraseTarget = ResolveEraseTarget(hitTransform);
        if (eraseTarget == null)
        {
            eraseTarget = FindCubeAtWorldPosition(worldCellCenter, cellSize);
        }

        if (eraseTarget == null)
        {
            return;
        }

        Scene eraseScene = eraseTarget.gameObject.scene;
        Undo.DestroyObjectImmediate(eraseTarget.gameObject);
        EditorSceneManager.MarkSceneDirty(eraseScene);
    }

    private Transform ResolveEraseTarget(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return null;
        }

        GameObject outermostPrefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(hitTransform.gameObject);
        Transform candidate = outermostPrefabRoot != null ? outermostPrefabRoot.transform : hitTransform;

        if (eraseOnlyUnderRoot && cubeRoot != null && !candidate.IsChildOf(cubeRoot))
        {
            return null;
        }

        if (eraseOnlyUnderRoot && cubeRoot == null)
        {
            return null;
        }

        return candidate;
    }

    private Transform FindCubeAtWorldPosition(Vector3 worldCellCenter, Vector3 cellSize)
    {
        if (cubeRoot == null)
        {
            return null;
        }

        float tolerance = Mathf.Min(cellSize.x, Mathf.Min(cellSize.y, cellSize.z)) * 0.2f;
        float sqrTolerance = tolerance * tolerance;

        for (int i = 0; i < cubeRoot.childCount; i++)
        {
            Transform child = cubeRoot.GetChild(i);
            if (TryGetWorldBoundsCenter(child.gameObject, out Vector3 center))
            {
                if ((center - worldCellCenter).sqrMagnitude <= sqrTolerance)
                {
                    return child;
                }
            }
            else if ((child.position - worldCellCenter).sqrMagnitude <= sqrTolerance)
            {
                return child;
            }
        }

        return null;
    }

    private Vector3 CalculatePlacementPosition(
        Vector3 hitPoint,
        Vector3 hitNormal,
        Transform hitTransform,
        Vector3 selectedCellSize,
        bool forceTopPlacement,
        out Vector3 placementAxis,
        out bool hasHitCubeFace,
        out Vector3 hitCubeCenter,
        out Vector3 hitCubeSize)
    {
        if (TryCalculateMinecraftPlacementPosition(
                hitPoint,
                hitNormal,
                hitTransform,
                selectedCellSize,
                forceTopPlacement,
                out Vector3 minecraftPosition,
                out placementAxis,
                out hitCubeCenter,
                out hitCubeSize))
        {
            hasHitCubeFace = true;
            return minecraftPosition;
        }

        hasHitCubeFace = false;
        hitCubeCenter = Vector3.zero;
        hitCubeSize = Vector3.one;

        Vector3 snappedNormal = forceTopPlacement ? Vector3.up : SnapNormalToAxis(hitNormal);
        placementAxis = snappedNormal;
        Vector3 halfOffset = new Vector3(
            snappedNormal.x * (selectedCellSize.x * 0.5f),
            snappedNormal.y * (selectedCellSize.y * 0.5f),
            snappedNormal.z * (selectedCellSize.z * 0.5f));

        Vector3 rawPosition = hitPoint + halfOffset + (Vector3.up * verticalOffset);
        return SnapToGrid(rawPosition, selectedCellSize);
    }

    private bool TryCalculateMinecraftPlacementPosition(
        Vector3 hitPoint,
        Vector3 hitNormal,
        Transform hitTransform,
        Vector3 selectedCellSize,
        bool forceTopPlacement,
        out Vector3 worldCellCenter,
        out Vector3 placementAxis,
        out Vector3 hitCubeCenter,
        out Vector3 hitCubeSize)
    {
        worldCellCenter = default;
        placementAxis = Vector3.up;
        hitCubeCenter = Vector3.zero;
        hitCubeSize = Vector3.one;

        if (!minecraftPlacementMode || cubeRoot == null)
        {
            return false;
        }

        if (!TryGetHitCubeMetrics(hitTransform, out hitCubeCenter, out hitCubeSize))
        {
            return false;
        }

        placementAxis = forceTopPlacement
            ? Vector3.up
            : ResolveSidePlacementAxis(hitPoint, hitNormal, hitCubeCenter, hitCubeSize);
        float centerDistance = ResolveCenterDistanceForAxis(placementAxis, hitCubeSize, selectedCellSize);
        worldCellCenter = hitCubeCenter + (placementAxis * centerDistance) + (Vector3.up * verticalOffset);
        return true;
    }

    private bool TryGetHitCubeMetrics(Transform hitTransform, out Vector3 center, out Vector3 size)
    {
        center = default;
        size = Vector3.one;
        if (hitTransform == null || cubeRoot == null)
        {
            return false;
        }

        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(hitTransform.gameObject);
        Transform candidate = prefabRoot != null ? prefabRoot.transform : hitTransform;
        if (!candidate.IsChildOf(cubeRoot))
        {
            return false;
        }

        if (!TryGetWorldBoundsCenterAndSize(candidate.gameObject, out center, out size))
        {
            return false;
        }

        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
        size.z = Mathf.Max(0.01f, size.z);
        return true;
    }

    private static Vector3 ResolvePlacementAxis(Vector3 hitPoint, Vector3 hitNormal, Vector3 hitCubeCenter, Vector3 hitCubeSize)
    {
        float absNormalX = Mathf.Abs(hitNormal.x);
        float absNormalY = Mathf.Abs(hitNormal.y);
        float absNormalZ = Mathf.Abs(hitNormal.z);
        float maxNormal = Mathf.Max(absNormalX, Mathf.Max(absNormalY, absNormalZ));
        float minNormal = Mathf.Min(absNormalX, Mathf.Min(absNormalY, absNormalZ));
        float secondNormal = (absNormalX + absNormalY + absNormalZ) - maxNormal - minNormal;

        if (maxNormal - secondNormal > 0.15f)
        {
            return SnapNormalToAxis(hitNormal);
        }

        Vector3 delta = hitPoint - hitCubeCenter;
        float nx = SafeNormalizeComponent(delta.x, hitCubeSize.x);
        float ny = SafeNormalizeComponent(delta.y, hitCubeSize.y);
        float nz = SafeNormalizeComponent(delta.z, hitCubeSize.z);

        float ax = Mathf.Abs(nx);
        float ay = Mathf.Abs(ny);
        float az = Mathf.Abs(nz);
        if (ax >= ay && ax >= az && ax > 0.15f)
        {
            return new Vector3(Mathf.Sign(nx), 0f, 0f);
        }

        if (ay >= ax && ay >= az && ay > 0.15f)
        {
            return new Vector3(0f, Mathf.Sign(ny), 0f);
        }

        if (az >= ax && az >= ay && az > 0.15f)
        {
            return new Vector3(0f, 0f, Mathf.Sign(nz));
        }

        return SnapNormalToAxis(hitNormal);
    }

    private static Vector3 ResolveSidePlacementAxis(Vector3 hitPoint, Vector3 hitNormal, Vector3 hitCubeCenter, Vector3 hitCubeSize)
    {
        Vector3 resolved = ResolvePlacementAxis(hitPoint, hitNormal, hitCubeCenter, hitCubeSize);
        if (Mathf.Approximately(resolved.y, 0f))
        {
            return resolved;
        }

        Vector3 delta = hitPoint - hitCubeCenter;
        float nx = SafeNormalizeComponent(delta.x, hitCubeSize.x);
        float nz = SafeNormalizeComponent(delta.z, hitCubeSize.z);
        float ax = Mathf.Abs(nx);
        float az = Mathf.Abs(nz);

        if (ax > 0.05f || az > 0.05f)
        {
            return ax >= az
                ? new Vector3(Mathf.Sign(nx), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(nz));
        }

        Vector3 snappedNormal = SnapNormalToAxis(hitNormal);
        if (!Mathf.Approximately(snappedNormal.x, 0f))
        {
            return new Vector3(Mathf.Sign(snappedNormal.x), 0f, 0f);
        }

        if (!Mathf.Approximately(snappedNormal.z, 0f))
        {
            return new Vector3(0f, 0f, Mathf.Sign(snappedNormal.z));
        }

        return Vector3.forward;
    }

    private static float ResolveCenterDistanceForAxis(Vector3 axis, Vector3 hitCubeSize, Vector3 selectedCellSize)
    {
        if (!Mathf.Approximately(axis.x, 0f))
        {
            return (hitCubeSize.x + selectedCellSize.x) * 0.5f;
        }

        if (!Mathf.Approximately(axis.y, 0f))
        {
            return (hitCubeSize.y + selectedCellSize.y) * 0.5f;
        }

        return (hitCubeSize.z + selectedCellSize.z) * 0.5f;
    }

    private static float SafeNormalizeComponent(float value, float totalSize)
    {
        float halfSize = Mathf.Max(0.0001f, totalSize * 0.5f);
        return value / halfSize;
    }

    private static Vector3 GetScaleRatio(Vector3 sourceSize, Vector3 targetSize)
    {
        float sx = Mathf.Max(0.01f, sourceSize.x);
        float sy = Mathf.Max(0.01f, sourceSize.y);
        float sz = Mathf.Max(0.01f, sourceSize.z);

        return new Vector3(
            Mathf.Max(0.01f, targetSize.x) / sx,
            Mathf.Max(0.01f, targetSize.y) / sy,
            Mathf.Max(0.01f, targetSize.z) / sz);
    }

    private Vector3 SnapToGrid(Vector3 worldPosition, Vector3 cellSize)
    {
        return new Vector3(
            Mathf.Round((worldPosition.x - gridOrigin.x) / cellSize.x) * cellSize.x + gridOrigin.x,
            Mathf.Round((worldPosition.y - gridOrigin.y) / cellSize.y) * cellSize.y + gridOrigin.y,
            Mathf.Round((worldPosition.z - gridOrigin.z) / cellSize.z) * cellSize.z + gridOrigin.z);
    }

    private Vector3Int WorldToCell(Vector3 worldPosition, Vector3 cellSize)
    {
        return new Vector3Int(
            Mathf.RoundToInt((worldPosition.x - gridOrigin.x) / cellSize.x),
            Mathf.RoundToInt((worldPosition.y - gridOrigin.y) / cellSize.y),
            Mathf.RoundToInt((worldPosition.z - gridOrigin.z) / cellSize.z));
    }

    private Vector3 ApplyVerticalCellOffset(Vector3 worldPosition, Vector3 cellSize)
    {
        if (verticalCellOffsetSteps == 0)
        {
            return worldPosition;
        }

        Vector3 shifted = worldPosition + (Vector3.up * (verticalCellOffsetSteps * cellSize.y));
        return SnapToGrid(shifted, cellSize);
    }

    private static Vector3 SnapNormalToAxis(Vector3 normal)
    {
        Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return new Vector3(Mathf.Sign(normal.x), 0f, 0f);
        }

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return new Vector3(0f, Mathf.Sign(normal.y), 0f);
        }

        return new Vector3(0f, 0f, Mathf.Sign(normal.z));
    }

    private static bool TryGetWorldBoundsCenter(GameObject target, out Vector3 center)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            center = target.transform.position;
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        center = bounds.center;
        return true;
    }

    private bool TryGetPointerHit(Vector2 mousePosition, out Vector3 hitPoint, out Vector3 hitNormal, out Transform hitTransform)
    {
        Ray mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (Physics.Raycast(mouseRay, out RaycastHit hitInfo, 5000f, ~0, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hitInfo.point;
            hitNormal = hitInfo.normal;
            hitTransform = hitInfo.transform;
            return true;
        }

        Plane fallbackPlane = new Plane(Vector3.up, gridOrigin);
        if (fallbackPlane.Raycast(mouseRay, out float enter))
        {
            hitPoint = mouseRay.GetPoint(enter);
            hitNormal = Vector3.up;
            hitTransform = null;
            return true;
        }

        hitPoint = default;
        hitNormal = Vector3.up;
        hitTransform = null;
        return false;
    }

    private Vector3 GetPlacementCellSize()
    {
        float activeScale = Mathf.Max(0.01f, syncScale);
        if (normalizePrefabSizeToCell)
        {
            return gridSize * activeScale;
        }

        if (!useSelectedPrefabSize)
        {
            return gridSize * activeScale;
        }

        if (TryGetSelectedPrefabMetrics(out Vector3 cellSize, out _))
        {
            return cellSize * activeScale;
        }

        return gridSize * activeScale;
    }

    private Vector3 GetSelectedPrefabCenterOffset()
    {
        if (!compensatePrefabPivotOffset)
        {
            return Vector3.zero;
        }

        if (TryGetSelectedPrefabMetrics(out Vector3 sourceSize, out Vector3 centerOffset))
        {
            if (normalizePrefabSizeToCell)
            {
                Vector3 targetSize = GetPlacementCellSize();
                Vector3 ratio = GetScaleRatio(sourceSize, targetSize);
                return Vector3.Scale(centerOffset, ratio);
            }

            float activeScale = Mathf.Max(0.01f, syncScale);
            return centerOffset * activeScale;
        }

        return Vector3.zero;
    }

    private bool TryGetSelectedPrefabMetrics(out Vector3 cellSize, out Vector3 centerOffset)
    {
        GameObject selectedPrefab = GetSelectedPrefab();
        if (selectedPrefab == null)
        {
            cellSize = gridSize;
            centerOffset = Vector3.zero;
            return false;
        }

        if (cachedMetricsPrefab != selectedPrefab || !hasCachedMetrics)
        {
            cachedMetricsPrefab = selectedPrefab;
            hasCachedMetrics = TryCalculatePrefabMetrics(selectedPrefab, out cachedPrefabCellSize, out cachedPrefabCenterOffset);
        }

        cellSize = hasCachedMetrics ? cachedPrefabCellSize : gridSize;
        centerOffset = hasCachedMetrics ? cachedPrefabCenterOffset : Vector3.zero;
        return hasCachedMetrics;
    }

    private static bool TryCalculatePrefabMetrics(GameObject prefab, out Vector3 cellSize, out Vector3 centerOffset)
    {
        cellSize = Vector3.one;
        centerOffset = Vector3.zero;

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        GameObject loadedRoot = null;
        try
        {
            loadedRoot = PrefabUtility.LoadPrefabContents(assetPath);
            if (!TryGetWorldBoundsCenterAndSize(loadedRoot, out Vector3 boundsCenter, out Vector3 boundsSize))
            {
                return false;
            }

            cellSize = new Vector3(
                Mathf.Max(0.01f, boundsSize.x),
                Mathf.Max(0.01f, boundsSize.y),
                Mathf.Max(0.01f, boundsSize.z));
            centerOffset = boundsCenter - loadedRoot.transform.position;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (loadedRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(loadedRoot);
            }
        }
    }

    private static bool TryGetWorldBoundsCenterAndSize(GameObject target, out Vector3 center, out Vector3 size)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            center = target.transform.position;
            size = Vector3.one;
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        center = bounds.center;
        size = bounds.size;
        return true;
    }

    private string[] BuildPaletteLabels()
    {
        string[] labels = new string[cubePrefabs.Length];
        for (int i = 0; i < cubePrefabs.Length; i++)
        {
            labels[i] = cubePrefabs[i] == null ? $"#{i + 1}" : cubePrefabs[i].name;
        }

        return labels;
    }

    private GameObject GetSelectedPrefab()
    {
        EnsureSelectionIndexInRange();
        if (selectedCubeIndex < 0 || selectedCubeIndex >= cubePrefabs.Length)
        {
            return null;
        }

        return cubePrefabs[selectedCubeIndex];
    }

    private void EnsureSelectionIndexInRange()
    {
        if (cubePrefabs == null || cubePrefabs.Length == 0)
        {
            cubePrefabs = new GameObject[1];
        }

        selectedCubeIndex = Mathf.Clamp(selectedCubeIndex, 0, cubePrefabs.Length - 1);
    }

    private void EnsurePaletteHasDefaults(bool forceRefresh = false)
    {
        if (cubePrefabs == null || cubePrefabs.Length == 0)
        {
            cubePrefabs = new GameObject[4];
        }

        if (!forceRefresh && cubePrefabs[0] != null)
        {
            return;
        }

        cubePrefabs[0] = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCobblestonePath);
        if (cubePrefabs.Length > 1)
        {
            cubePrefabs[1] = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMinecraftCubePath);
        }
    }

    private Transform EnsureCubeRoot()
    {
        if (cubeRoot != null)
        {
            return cubeRoot;
        }

        GameObject existing = GameObject.Find(DefaultRootName);
        if (existing != null)
        {
            cubeRoot = existing.transform;
            return cubeRoot;
        }

        GameObject root = new GameObject(DefaultRootName);
        Undo.RegisterCreatedObjectUndo(root, "Tạo Nhóm Gốc Cube");
        cubeRoot = root.transform;
        EditorSceneManager.MarkSceneDirty(root.scene);
        return cubeRoot;
    }
}

