using UnityEngine;

[DisallowMultipleComponent]
public class KnotOrbitCloudPlatform : MonoBehaviour
{
    public enum KnotPattern
    {
        TorusKnot = 0,
        Hypotrochoid = 1,
        RoseHelix = 2
    }

    [Header("Knot Orbit")]
    [SerializeField] private KnotPattern pattern = KnotPattern.TorusKnot;
    [SerializeField] private float majorRadius = 3f;
    [SerializeField] private float minorRadius = 1.1f;
    [SerializeField, Min(1)] private int pTurns = 2;
    [SerializeField, Min(1)] private int qTurns = 3;
    [SerializeField] private float cycleDuration = 6f;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField] private float indexPhaseStepDegrees = 36f;
    [SerializeField, Min(0)] private int platformIndex = 0;
    [SerializeField] private float verticalScale = 1f;
    [SerializeField] private float bobAmplitude = 0.22f;
    [SerializeField] private float bobFrequency = 1.35f;
    [SerializeField, Range(0f, 0.9f)] private float radialPulse = 0.28f;
    [SerializeField] private float radialPulseFrequency = 0.8f;
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
        float phaseRadians = (phaseOffsetDegrees + (platformIndex * indexPhaseStepDegrees)) * Mathf.Deg2Rad;
        float theta = ((travel / duration) * Mathf.PI * 2f * direction) + phaseRadians;

        Vector3 offset = EvaluateOffset(theta);
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
        int platformIndex,
        float majorRadius,
        float minorRadius,
        int pTurns,
        int qTurns,
        float cycleDuration,
        float phaseDegrees,
        Transform assignedPassenger)
    {
        this.platformIndex = Mathf.Max(0, platformIndex);
        this.majorRadius = Mathf.Max(0.2f, majorRadius);
        this.minorRadius = Mathf.Max(0.05f, minorRadius);
        this.pTurns = Mathf.Max(1, pTurns);
        this.qTurns = Mathf.Max(1, qTurns);
        this.cycleDuration = Mathf.Max(0.1f, cycleDuration);
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
        majorRadius = Mathf.Max(0.2f, majorRadius);
        minorRadius = Mathf.Max(0.05f, minorRadius);
        pTurns = Mathf.Max(1, pTurns);
        qTurns = Mathf.Max(1, qTurns);
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        platformIndex = Mathf.Max(0, platformIndex);
        verticalScale = Mathf.Max(0f, verticalScale);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        radialPulse = Mathf.Clamp(radialPulse, 0f, 0.9f);
        radialPulseFrequency = Mathf.Max(0f, radialPulseFrequency);
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

    private Vector3 EvaluateOffset(float theta)
    {
        float pulse = 1f + (Mathf.Sin((theta * radialPulseFrequency) + (platformIndex * 0.45f)) * radialPulse);
        float adjustedMinor = minorRadius * pulse;

        float x;
        float y;
        float z;
        switch (pattern)
        {
            case KnotPattern.Hypotrochoid:
                float a = majorRadius + adjustedMinor;
                float b = Mathf.Max(0.05f, adjustedMinor * 0.7f);
                float ratio = (a - b) / b;
                x = ((a - b) * Mathf.Cos(theta)) + (b * Mathf.Cos(ratio * theta));
                z = ((a - b) * Mathf.Sin(theta)) - (b * Mathf.Sin(ratio * theta));
                y = Mathf.Sin((theta * qTurns) + 0.6f) * (adjustedMinor * 0.9f * verticalScale);
                break;

            case KnotPattern.RoseHelix:
                float petals = Mathf.Max(2, qTurns);
                float rose = majorRadius * (0.56f + (0.44f * Mathf.Cos(theta * petals)));
                x = Mathf.Cos(theta * pTurns) * rose;
                z = Mathf.Sin(theta * pTurns) * rose;
                y = Mathf.Sin((theta * qTurns) + 1.1f) * (adjustedMinor * 1.1f * verticalScale);
                break;

            default:
                x = (majorRadius + (adjustedMinor * Mathf.Cos(theta * qTurns))) * Mathf.Cos(theta * pTurns);
                z = (majorRadius + (adjustedMinor * Mathf.Cos(theta * qTurns))) * Mathf.Sin(theta * pTurns);
                y = Mathf.Sin(theta * qTurns) * (adjustedMinor * verticalScale);
                break;
        }

        y += Mathf.Sin((theta * bobFrequency) + (platformIndex * 0.55f)) * bobAmplitude;
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
        Gizmos.color = new Color(0.95f, 0.18f, 0.62f, 1f);
        Gizmos.DrawWireSphere(center, majorRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(center, 0.1f);
    }
}
