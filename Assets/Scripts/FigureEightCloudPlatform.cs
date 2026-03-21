using UnityEngine;

/// <summary>
/// Cloud platform movement for LV4.
/// Moves in a figure-eight path with optional vertical bobbing.
/// </summary>
[DisallowMultipleComponent]
public class FigureEightCloudPlatform : MonoBehaviour
{
    public enum PathShape
    {
        FigureEight = 0,
        Circle = 1
    }

    public enum FigureEightPlane
    {
        FrontXY = 0,
        TopDownXZ = 1,
        SideYZ = 2
    }

    [Header("Path")]
    [SerializeField] private PathShape pathShape = PathShape.FigureEight;
    [SerializeField] private FigureEightPlane pathPlane = FigureEightPlane.FrontXY;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField] private float radiusX = 5.5f;
    [SerializeField] private float radiusZ = 3f;
    [SerializeField] private float cycleDuration = 6f;
    [SerializeField] private float bobAmplitude = 0.35f;
    [SerializeField] private float bobFrequency = 1.25f;
    [SerializeField] private bool reverseDirection = false;
    [SerializeField] private float directionFlipInterval = 0f;

    [Header("Passenger")]
    [SerializeField] private Transform passenger;
    [Tooltip("Tick để tự động đặt passenger lên đỉnh cloud trong Editor.")]
    [SerializeField] private bool snapPassengerToTop = false;

    private Vector3 _anchorPosition;
    private Vector3 _lastPosition;
    private float _startTime;
    private bool _initialized;

    private void Start()
    {
        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            InitializeRuntime(force: true);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntime();

        float duration = Mathf.Max(0.1f, cycleDuration);
        float elapsed = Time.time - _startTime;
        float direction = reverseDirection ? -1f : 1f;
        float phaseRadians = phaseOffsetDegrees * Mathf.Deg2Rad;
        float travelSeconds = directionFlipInterval > 0f
            ? Mathf.PingPong(elapsed, Mathf.Max(0.05f, directionFlipInterval))
            : elapsed;
        float t = ((travelSeconds / duration) * Mathf.PI * 2f * direction) + phaseRadians;

        Vector3 offset = EvaluatePathOffset(t);
        Vector3 targetPosition = _anchorPosition + offset;
        Vector3 delta = targetPosition - _lastPosition;

        transform.position = targetPosition;

        if (passenger != null)
        {
            passenger.position += delta;
        }

        _lastPosition = targetPosition;
    }

    public void Configure(
        float radiusX,
        float radiusZ,
        float cycleDuration,
        float bobAmplitude,
        float bobFrequency,
        Transform assignedPassenger,
        FigureEightPlane plane,
        bool reverse,
        PathShape shape = PathShape.FigureEight,
        float phaseDegrees = 0f,
        float flipIntervalSeconds = 0f)
    {
        this.radiusX = Mathf.Max(0.1f, radiusX);
        this.radiusZ = Mathf.Max(0.1f, radiusZ);
        this.cycleDuration = Mathf.Max(0.1f, cycleDuration);
        this.bobAmplitude = Mathf.Max(0f, bobAmplitude);
        this.bobFrequency = Mathf.Max(0f, bobFrequency);
        pathShape = shape;
        pathPlane = plane;
        reverseDirection = reverse;
        phaseOffsetDegrees = phaseDegrees;
        directionFlipInterval = Mathf.Max(0f, flipIntervalSeconds);
        if (assignedPassenger != null)
        {
            passenger = assignedPassenger;
        }

        if (Application.isPlaying)
        {
            InitializeRuntime(force: true);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radiusX = Mathf.Max(0.1f, radiusX);
        radiusZ = Mathf.Max(0.1f, radiusZ);
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        directionFlipInterval = Mathf.Max(0f, directionFlipInterval);

        if (snapPassengerToTop && passenger != null && !Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || passenger == null)
                {
                    return;
                }

                SnapPassengerOnTop();
            };
        }
    }
#endif

    private void InitializeRuntime(bool force = false)
    {
        if (_initialized && !force)
        {
            return;
        }

        _anchorPosition = transform.position;
        _lastPosition = _anchorPosition;
        _startTime = Time.time;
        _initialized = true;
    }

    private Vector3 EvaluatePathOffset(float t)
    {
        float major = Mathf.Sin(t) * radiusX;
        float minor = pathShape == PathShape.Circle
            ? Mathf.Cos(t) * radiusZ
            : Mathf.Sin(t) * Mathf.Cos(t) * radiusZ;
        float bob = bobAmplitude > 0f ? Mathf.Sin(t * bobFrequency) * bobAmplitude : 0f;

        switch (pathPlane)
        {
            case FigureEightPlane.TopDownXZ:
                return new Vector3(major, bob, minor);
            case FigureEightPlane.SideYZ:
                return new Vector3(bob, major, minor);
            default:
                // Front view: the "8" appears in X-Y plane, depth wobble is on Z.
                return new Vector3(major, minor, bob);
        }
    }

    [ContextMenu("Snap Passenger To Top")]
    private void SnapPassengerOnTop()
    {
        if (passenger == null)
        {
            return;
        }

        Vector3 topPos = transform.position;
        Renderer[] cloudRenderers = GetComponentsInChildren<Renderer>(true);
        if (cloudRenderers.Length > 0)
        {
            Bounds cloudBounds = cloudRenderers[0].bounds;
            for (int i = 1; i < cloudRenderers.Length; i++)
            {
                cloudBounds.Encapsulate(cloudRenderers[i].bounds);
            }

            topPos = new Vector3(cloudBounds.center.x, cloudBounds.max.y, cloudBounds.center.z);
        }

        float feetOffset = 0f;
        Renderer[] passengerRenderers = passenger.GetComponentsInChildren<Renderer>(true);
        if (passengerRenderers.Length > 0)
        {
            Bounds passengerBounds = passengerRenderers[0].bounds;
            for (int i = 1; i < passengerRenderers.Length; i++)
            {
                passengerBounds.Encapsulate(passengerRenderers[i].bounds);
            }

            feetOffset = passenger.position.y - passengerBounds.min.y;
        }

        passenger.position = new Vector3(topPos.x, topPos.y + feetOffset, topPos.z);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(passenger);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying && _initialized ? _anchorPosition : transform.position;

        Gizmos.color = new Color(0.95f, 0.75f, 0.15f, 1f);
        const int segments = 64;
        Vector3 prev = center + EvaluatePathOffset(0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + EvaluatePathOffset(t);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(center, 0.12f);
    }
}
