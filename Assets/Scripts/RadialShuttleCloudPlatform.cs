using UnityEngine;

[DisallowMultipleComponent]
public class RadialShuttleCloudPlatform : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private FigureEightCloudPlatform.FigureEightPlane motionPlane = FigureEightCloudPlatform.FigureEightPlane.FrontXY;
    [SerializeField] private Vector3 radialAxis = Vector3.right;
    [SerializeField] private float innerRadius = 1.2f;
    [SerializeField] private float outerRadius = 4.8f;
    [SerializeField] private float cycleDuration = 4f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobFrequency = 1f;
    [SerializeField] private float directionFlipInterval = 0f;
    [SerializeField] private float phaseOffsetDegrees = 0f;

    [Header("Passenger")]
    [SerializeField] private Transform passenger;
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
        float travel = directionFlipInterval > 0f
            ? Mathf.PingPong(elapsed, Mathf.Max(0.05f, directionFlipInterval))
            : elapsed;
        float theta = ((travel / duration) * Mathf.PI * 2f) + (phaseOffsetDegrees * Mathf.Deg2Rad);

        float t = 0.5f - (0.5f * Mathf.Cos(theta));
        float radius = Mathf.Lerp(innerRadius, outerRadius, t);
        Vector3 axis = ResolveRadialAxis();
        Vector3 bobAxis = ResolveBobAxis();
        float bob = bobAmplitude > 0f ? Mathf.Sin(theta * Mathf.Max(0f, bobFrequency)) * bobAmplitude : 0f;

        Vector3 targetPosition = _anchorPosition + (axis * radius) + (bobAxis * bob);
        Vector3 delta = targetPosition - _lastPosition;

        transform.position = targetPosition;

        if (passenger != null)
        {
            passenger.position += delta;
        }

        _lastPosition = targetPosition;
    }

    public void Configure(
        float innerRadius,
        float outerRadius,
        float cycleDuration,
        float bobAmplitude,
        float bobFrequency,
        Vector3 radialAxis,
        FigureEightCloudPlatform.FigureEightPlane plane,
        float flipIntervalSeconds,
        Transform assignedPassenger,
        float phaseDegrees = 0f)
    {
        this.innerRadius = Mathf.Max(0f, innerRadius);
        this.outerRadius = Mathf.Max(this.innerRadius + 0.1f, outerRadius);
        this.cycleDuration = Mathf.Max(0.1f, cycleDuration);
        this.bobAmplitude = Mathf.Max(0f, bobAmplitude);
        this.bobFrequency = Mathf.Max(0f, bobFrequency);
        this.radialAxis = radialAxis.sqrMagnitude > 0.0001f ? radialAxis.normalized : Vector3.right;
        motionPlane = plane;
        directionFlipInterval = Mathf.Max(0f, flipIntervalSeconds);
        phaseOffsetDegrees = Mathf.Repeat(phaseDegrees, 360f);
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
        innerRadius = Mathf.Max(0f, innerRadius);
        outerRadius = Mathf.Max(innerRadius + 0.1f, outerRadius);
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        directionFlipInterval = Mathf.Max(0f, directionFlipInterval);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        if (radialAxis.sqrMagnitude <= 0.0001f)
        {
            radialAxis = Vector3.right;
        }

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

    private Vector3 ResolveRadialAxis()
    {
        Vector3 axis;
        switch (motionPlane)
        {
            case FigureEightCloudPlatform.FigureEightPlane.TopDownXZ:
                axis = new Vector3(radialAxis.x, 0f, radialAxis.z);
                if (axis.sqrMagnitude <= 0.0001f)
                {
                    axis = Vector3.right;
                }
                break;
            case FigureEightCloudPlatform.FigureEightPlane.SideYZ:
                axis = new Vector3(0f, radialAxis.y, radialAxis.z);
                if (axis.sqrMagnitude <= 0.0001f)
                {
                    axis = Vector3.up;
                }
                break;
            default:
                axis = new Vector3(radialAxis.x, radialAxis.y, 0f);
                if (axis.sqrMagnitude <= 0.0001f)
                {
                    axis = Vector3.right;
                }
                break;
        }

        return axis.normalized;
    }

    private Vector3 ResolveBobAxis()
    {
        switch (motionPlane)
        {
            case FigureEightCloudPlatform.FigureEightPlane.TopDownXZ:
                return Vector3.up;
            case FigureEightCloudPlatform.FigureEightPlane.SideYZ:
                return Vector3.right;
            default:
                return Vector3.forward;
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
        Vector3 axis = ResolveRadialAxis();

        Gizmos.color = new Color(0.15f, 0.85f, 1f, 1f);
        Gizmos.DrawLine(center + (axis * innerRadius), center + (axis * outerRadius));

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(center, 0.12f);
    }
}

