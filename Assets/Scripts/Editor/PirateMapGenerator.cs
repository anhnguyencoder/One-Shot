using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PirateMapGenerator : EditorWindow
{
    private const string AssetPath = "Assets/Environment/kenney_pirate-kit/Models/FBX format/";
    private static List<Vector3> occupiedPositions = new List<Vector3>();
    private const float PropSafetyRadius = 1.5f;

    [MenuItem("Tools/Pirate Map/1. Clear Current Map")]
    public static void ClearMap()
    {
        GameObject oldMap = GameObject.Find("PirateOutpost_Map");
        if (oldMap != null)
        {
            Undo.DestroyObjectImmediate(oldMap);
        }
    }

    [MenuItem("Tools/Pirate Map/2. Generate Clean Outpost")]
    public static void Generate()
    {
        ClearMap();
        occupiedPositions.Clear();

        // Create root object
        GameObject root = new GameObject("PirateOutpost_Map");
        Undo.RegisterCreatedObjectUndo(root, "Generate Pirate Map");

        // --- GRID / GROUND ---
        GameObject groundContainer = new GameObject("Ground");
        groundContainer.transform.SetParent(root.transform);
        
        GameObject sandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "patch-sand.fbx");
        GameObject grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "patch-grass.fbx");

        int gridSize = 12; // Larger area
        float spacing = 2.0f; // Kenney tiles are 2x2

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                float noise = Mathf.PerlinNoise(x * 0.3f, z * 0.3f);
                GameObject prefab = noise > 0.45f ? grassPrefab : sandPrefab;
                
                if (prefab != null)
                {
                    Vector3 pos = new Vector3(x * spacing, 0, z * spacing);
                    GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    tile.transform.position = pos;
                    tile.transform.SetParent(groundContainer.transform);
                }
            }
        }

        // --- WALLS & TOWERS (Edge Logic) ---
        GameObject wallContainer = new GameObject("Walls");
        wallContainer.transform.SetParent(root.transform);
        GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "castle-wall.fbx");
        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "castle-gate.fbx");
        GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "tower-complete-large.fbx");

        float minBound = 0;
        float maxBound = (gridSize - 1) * spacing;

        // Place Corner Towers
        PlaceProp(towerPrefab, new Vector3(minBound, 0, minBound), Vector3.zero, wallContainer.transform);
        PlaceProp(towerPrefab, new Vector3(maxBound, 0, minBound), Vector3.zero, wallContainer.transform);
        PlaceProp(towerPrefab, new Vector3(minBound, 0, maxBound), Vector3.zero, wallContainer.transform);
        PlaceProp(towerPrefab, new Vector3(maxBound, 0, maxBound), Vector3.zero, wallContainer.transform);

        // Place Walls along edges (skipping corners)
        for (int i = 1; i < gridSize - 1; i++)
        {
            float pos = i * spacing;
            // Bottom edge (Gate in middle)
            if (i == gridSize / 2)
                PlaceProp(gatePrefab, new Vector3(pos, 0, minBound), Vector3.zero, wallContainer.transform);
            else
                PlaceProp(wallPrefab, new Vector3(pos, 0, minBound), Vector3.zero, wallContainer.transform);

            // Top edge
            PlaceProp(wallPrefab, new Vector3(pos, 0, maxBound), new Vector3(0, 180, 0), wallContainer.transform);

            // Left edge
            PlaceProp(wallPrefab, new Vector3(minBound, 0, pos), new Vector3(0, 90, 0), wallContainer.transform);

            // Right edge
            PlaceProp(wallPrefab, new Vector3(maxBound, 0, pos), new Vector3(0, -90, 0), wallContainer.transform);
        }

        // --- INTERIOR STRUCTURES ---
        // Main building in the center. We reserve a large space for it.
        GameObject structPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "structure.fbx");
        Vector3 centerPos = new Vector3(maxBound / 2f, 0, maxBound / 2f + 2);
        PlaceProp(structPrefab, centerPos, new Vector3(0, 180, 0), root.transform, 4f); // Larger safety radius

        // --- DECORATIONS & PROPS (Smart Placement) ---
        GameObject[] props = new GameObject[] {
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "barrel.fbx"),
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "crate.fbx"),
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "cannon-mobile.fbx"),
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "chest.fbx")
        };

        for (int i = 0; i < 20; i++)
        {
            GameObject p = props[Random.Range(0, props.Length)];
            Vector3 randomPos = new Vector3(Random.Range(4, maxBound - 4), 0, Random.Range(4, maxBound - 4));
            
            if (IsPositionClear(randomPos, PropSafetyRadius))
            {
                PlaceProp(p, randomPos, new Vector3(0, Random.Range(0, 360), 0), root.transform);
            }
        }

        // --- NATURE ---
        GameObject palmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath + "palm-straight.fbx");
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPos = new Vector3(Random.Range(-2, maxBound + 2), 0, Random.Range(-2, maxBound + 2));
            // Trees can overlap slightly more or be outside the walls
            if (IsPositionClear(randomPos, 2f))
            {
                PlaceProp(palmPrefab, randomPos, new Vector3(0, Random.Range(0, 360), 0), root.transform);
            }
        }

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Generated Collision-Free Pirate Map!");
    }

    private static bool IsPositionClear(Vector3 pos, float radius)
    {
        foreach (Vector3 occupied in occupiedPositions)
        {
            if (Vector3.Distance(pos, occupied) < radius)
                return false;
        }
        return true;
    }

    private static void PlaceProp(GameObject prefab, Vector3 pos, Vector3 rot, Transform parent, float safetyRadius = PropSafetyRadius)
    {
        if (prefab == null) return;

        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.transform.position = pos;
        obj.transform.eulerAngles = rot;
        obj.transform.SetParent(parent);
        
        occupiedPositions.Add(pos);
    }

    [MenuItem("Tools/Pirate Map/Randomize Selected Rotations")]
    public static void RandomizeSelected()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj.transform, "Randomize Rotation");
            obj.transform.eulerAngles = new Vector3(0, Random.Range(0, 360), 0);
        }
    }
}
