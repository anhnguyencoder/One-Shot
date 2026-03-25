using UnityEngine;

[DisallowMultipleComponent]
public class VortexOrbitCloudPlatform : MonoBehaviour
{
    public enum VortexMode
    {
        HelixBurst = 0,
        OrbitLobe = 1,
        StarDrift = 2
    }

    [Header("Vortex Orbit")]
    [SerializeField] private VortexMode vortexMode = VortexMode.StarDrift;
    [SerializeField] private float orbitRadius = 2.8f;
    [SerializeField] private float radialBreath = 0.9f;
    [SerializeField] private float radialBreathFrequency = 0.7f;
    [SerializeField] private float cycleDuration = 5f;
    [SerializeField] private float angularOffsetDegrees = 0f;
    [SerializeField] private float indexPhaseStepDegrees = 34f;
    [SerializeField, Min(0)] private int orbitIndex = 0;
    [SerializeField] private float verticalAmplitude = 1.2f;
    [SerializeField] private float verticalFrequency = 1.35f;
    [SerializeField] private int lobeCount = 3;
    [SerializeField] private bool reverseDirection = false;
    [SerializeField] private float directionFlipInterval = 0f;
    [SerializeField] private bool useGlobalClock = true;

    [Header("Passenger")]
    [SerializeField] private Transform passenger;
    [SerializeField] private bool snapPassengerToTop = false;

    private Vector3 _anchorPosition;
    private Vector3 _lastPosition;
    private double _startTime;
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
        double now = Time.timeAsDouble;
        float elapsed = (float)(useGlobalClock ? now : (now - _startTime));
        float travel = directionFlipInterval > 0f
            ? Mathf.PingPong(elapsed, Mathf.Max(0.05f, directionFlipInterval))
            : elapsed;

        float direction = reverseDirection ? -1f : 1f;
        float baseTheta = ((travel / duration) * Mathf.PI * 2f * direction);
        float indexPhase = orbitIndex * indexPhaseStepDegrees * Mathf.Deg2Rad;
        float theta = baseTheta + (angularOffsetDegrees * Mathf.Deg2Rad) + indexPhase;

        Vector3 offset = EvaluateOffset(theta, baseTheta);
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
        int orbitIndex,
        float orbitRadius,
        float cycleDuration,
        float radialBreath,
        float radialBreathFrequency,
        float verticalAmplitude,
        float verticalFrequency,
        float angularOffsetDegrees,
        Transform assignedPassenger)
    {
        this.orbitIndex = Mathf.Max(0, orbitIndex);
        this.orbitRadius = Mathf.Max(0.2f, orbitRadius);
        this.cycleDuration = Mathf.Max(0.1f, cycleDuration);
        this.radialBreath = Mathf.Max(0f, radialBreath);
        this.radialBreathFrequency = Mathf.Max(0f, radialBreathFrequency);
        this.verticalAmplitude = Mathf.Max(0f, verticalAmplitude);
        this.verticalFrequency = Mathf.Max(0f, verticalFrequency);
        this.angularOffsetDegrees = Mathf.Repeat(angularOffsetDegrees, 360f);
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
        orbitRadius = Mathf.Max(0.2f, orbitRadius);
        radialBreath = Mathf.Max(0f, radialBreath);
        radialBreathFrequency = Mathf.Max(0f, radialBreathFrequency);
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        angularOffsetDegrees = Mathf.Repeat(angularOffsetDegrees, 360f);
        orbitIndex = Mathf.Max(0, orbitIndex);
        verticalAmplitude = Mathf.Max(0f, verticalAmplitude);
        verticalFrequency = Mathf.Max(0f, verticalFrequency);
        lobeCount = Mathf.Clamp(lobeCount, 2, 8);
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
        _startTime = Time.timeAsDouble;
        _initialized = true;
    }

    private Vector3 EvaluateOffset(float theta, float baseTheta)
    {
        float breath = 1f + (Mathf.Sin(baseTheta * radialBreathFrequency) * radialBreath);
        float radius = Mathf.Max(0.1f, orbitRadius * breath);
        float x;
        float z;
        float y;

        switch (vortexMode)
        {
            case VortexMode.HelixBurst:
                x = Mathf.Cos(theta) * radius;
                z = Mathf.Sin(theta) * radius;
                y = Mathf.Sin(theta * verticalFrequency) * verticalAmplitude;
                break;

            case VortexMode.OrbitLobe:
                float lobeTheta = theta * Mathf.Max(2, lobeCount);
                float lobeRadius = radius * (0.55f + (0.45f * Mathf.Cos(lobeTheta)));
                x = Mathf.Cos(theta) * lobeRadius;
                z = Mathf.Sin(theta) * lobeRadius;
                y = Mathf.Sin((theta * verticalFrequency) + 1.2f) * verticalAmplitude;
                break;

            default:
                float starTheta = theta * Mathf.Max(2, lobeCount);
                float starRadius = radius * (0.7f + (0.3f * Mathf.Sin(starTheta + 0.8f)));
                x = Mathf.Cos(theta) * starRadius;
                z = Mathf.Sin(theta) * starRadius;
                y = (Mathf.Sin(theta * verticalFrequency) + Mathf.Sin((theta * 0.5f) + 0.6f)) * (verticalAmplitude * 0.55f);
                break;
        }

        return new Vector3(x, y, z);
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
        Gizmos.color = new Color(0.12f, 0.95f, 0.85f, 1f);
        Gizmos.DrawWireSphere(center, orbitRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(center, 0.1f);
    }
}
