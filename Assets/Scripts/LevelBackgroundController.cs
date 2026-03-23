using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LevelBackgroundControllerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAttachToScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        TryAttachToScene(scene);
    }

    private static void TryAttachToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (!TryGetLevel(scene.name, out int level) || level < 1 || level > 10)
        {
            return;
        }

        Camera targetCamera = FindTargetCamera(scene);
        if (targetCamera == null)
        {
            return;
        }

        if (targetCamera.GetComponent<LevelBackgroundController>() == null)
        {
            targetCamera.gameObject.AddComponent<LevelBackgroundController>();
        }
    }

    private static Camera FindTargetCamera(Scene scene)
    {
        Camera fallbackCamera = null;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cameras.Length; j++)
            {
                Camera camera = cameras[j];
                if (camera == null)
                {
                    continue;
                }

                if (fallbackCamera == null)
                {
                    fallbackCamera = camera;
                }

                if (camera.CompareTag("MainCamera"))
                {
                    return camera;
                }
            }
        }

        return fallbackCamera;
    }

    private static bool TryGetLevel(string sceneName, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (!sceneName.StartsWith("LV", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = sceneName.Substring(2);
        return int.TryParse(suffix, out level) && level > 0;
    }
}

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class LevelBackgroundController : MonoBehaviour
{
    [Header("Level Mapping")]
    [SerializeField] private string levelScenePrefix = "LV";
    [SerializeField] private int firstLevel = 1;
    [SerializeField] private int lastLevel = 10;
    [SerializeField] private string resourcesFolder = "Background";

    [Header("Background Quad")]
    [SerializeField] private string backgroundObjectName = "OneShotLevelBackground";
    [SerializeField, Range(0.1f, 0.99f)] private float distanceFromFarClipRatio = 0.9f;

    private static Mesh s_quadMesh;

    private Camera _camera;
    private Transform _quadTransform;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private float _textureAspect = 1f;

    private void Awake()
    {
        RefreshBackground();
    }

    private void OnEnable()
    {
        RefreshBackground();
    }

    private void OnValidate()
    {
        distanceFromFarClipRatio = Mathf.Clamp(distanceFromFarClipRatio, 0.1f, 0.99f);
        RefreshBackground();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || _camera == null || _quadTransform == null)
        {
            return;
        }

        UpdateQuadTransform();
    }

    private void RefreshBackground()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            return;
        }

        if (!TryGetLevelIndex(gameObject.scene.name, out int levelIndex))
        {
            return;
        }

        if (levelIndex < firstLevel || levelIndex > lastLevel)
        {
            return;
        }

        EnsureBackgroundObject();

        if (_meshFilter != null && _meshFilter.sharedMesh == null)
        {
            _meshFilter.sharedMesh = GetQuadMesh();
        }

        if (_meshRenderer == null)
        {
            return;
        }

        EnsureMaterial();
        ApplyTexture(levelIndex);
        ApplyRendererSettings();
        UpdateQuadTransform();
    }

    private void EnsureBackgroundObject()
    {
        Transform existing = transform.Find(backgroundObjectName);
        if (existing == null)
        {
            GameObject quadObject = new GameObject(backgroundObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            existing = quadObject.transform;
            existing.SetParent(transform, false);
        }

        _quadTransform = existing;
        _meshFilter = existing.GetComponent<MeshFilter>();
        _meshRenderer = existing.GetComponent<MeshRenderer>();
    }

    private void EnsureMaterial()
    {
        if (_meshRenderer.sharedMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return;
        }

        Material material = new Material(shader)
        {
            name = "OneShotLevelBackgroundMat"
        };

        _meshRenderer.sharedMaterial = material;
    }

    private void ApplyTexture(int levelIndex)
    {
        Material material = _meshRenderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        Texture2D levelTexture = Resources.Load<Texture2D>($"{resourcesFolder}/{levelIndex}");
        if (levelTexture == null)
        {
            Debug.LogWarning($"LevelBackgroundController: Missing Resources/{resourcesFolder}/{levelIndex}.png");
            return;
        }

        _textureAspect = Mathf.Max(0.0001f, (float)levelTexture.width / levelTexture.height);

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", levelTexture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", levelTexture);
        }
    }

    private void ApplyRendererSettings()
    {
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void UpdateQuadTransform()
    {
        float farClip = Mathf.Max(_camera.nearClipPlane + 1f, _camera.farClipPlane);
        float distance = Mathf.Clamp(
            farClip * distanceFromFarClipRatio,
            _camera.nearClipPlane + 0.5f,
            farClip - 0.1f);

        _quadTransform.position = _camera.transform.position + _camera.transform.forward * distance;
        _quadTransform.rotation = _camera.transform.rotation;

        float height;
        if (_camera.orthographic)
        {
            height = _camera.orthographicSize * 2f;
        }
        else
        {
            float halfFovRadians = 0.5f * _camera.fieldOfView * Mathf.Deg2Rad;
            height = 2f * distance * Mathf.Tan(halfFovRadians);
        }

        float viewWidth = height * _camera.aspect;
        float quadWidth = viewWidth;
        float quadHeight = quadWidth / Mathf.Max(0.0001f, _textureAspect);

        if (quadHeight < height)
        {
            quadHeight = height;
            quadWidth = quadHeight * Mathf.Max(0.0001f, _textureAspect);
        }

        _quadTransform.localScale = new Vector3(quadWidth, quadHeight, 1f);
    }

    private static Mesh GetQuadMesh()
    {
        if (s_quadMesh != null)
        {
            return s_quadMesh;
        }

        GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        MeshFilter meshFilter = tempQuad.GetComponent<MeshFilter>();
        s_quadMesh = meshFilter != null ? meshFilter.sharedMesh : null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(tempQuad);
        }
        else
#endif
        {
            Destroy(tempQuad);
        }

        return s_quadMesh;
    }

    private bool TryGetLevelIndex(string sceneName, out int levelIndex)
    {
        levelIndex = 0;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (!sceneName.StartsWith(levelScenePrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = sceneName.Substring(levelScenePrefix.Length);
        if (!int.TryParse(suffix, out int parsed) || parsed <= 0)
        {
            return false;
        }

        levelIndex = parsed;
        return true;
    }
}
