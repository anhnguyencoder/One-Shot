using UnityEngine;

[DisallowMultipleComponent]
public class PhaseGateCloudPlatform : MonoBehaviour
{
    public enum GatePattern
    {
        DiamondGate = 0,
        SkewedCross = 1,
        SpiralBox = 2
    }

    [Header("Phase Gate Path")]
    [SerializeField] private GatePattern gatePattern = GatePattern.SpiralBox;
    [SerializeField] private float radiusX = 3f;
    [SerializeField] private float radiusZ = 2.5f;
    [SerializeField] private float cycleDuration = 5.6f;
    [SerializeField] private float gateHoldTime = 0.14f;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField] private float indexPhaseStepDegrees = 34f;
    [SerializeField, Min(0)] private int platformIndex = 0;
    [SerializeField] private float liftPerGate = 0.25f;
    [SerializeField] private float verticalWaveAmplitude = 0.6f;
    [SerializeField] private float verticalWaveFrequency = 1.25f;
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

        float duration = Mathf.Max(0.5f, cycleDuration);
        float segmentDuration = duration * 0.25f;
        float holdTime = Mathf.Clamp(gateHoldTime, 0f, segmentDuration * 0.7f);
        float travelTime = Mathf.Max(0.05f, segmentDuration - holdTime);

        double now = Time.timeAsDouble;
        float elapsed = (float)(useGlobalClock ? now : (now - _startTime));
        float travel = directionFlipInterval > 0f
            ? Mathf.PingPong(elapsed, Mathf.Max(0.05f, directionFlipInterval))
            : elapsed;

        float phase = Mathf.Repeat((travel / duration) + ((phaseOffsetDegrees + (platformIndex * indexPhaseStepDegrees)) / 360f), 1f);
        if (reverseDirection)
        {
            phase = 1f - phase;
        }

        Vector3[] gates = BuildGates();
        float timeInCycle = phase * duration;
        int gateIndex = Mathf.Clamp(Mathf.FloorToInt(timeInCycle / segmentDuration), 0, 3);
        int nextIndex = (gateIndex + 1) % 4;
        float timeInSegment = timeInCycle - (gateIndex * segmentDuration);

        Vector3 localOffset;
        if (timeInSegment <= holdTime)
        {
            localOffset = gates[gateIndex];
        }
        else
        {
            float t = (timeInSegment - holdTime) / travelTime;
            t = Mathf.SmoothStep(0f, 1f, t);
            localOffset = Vector3.Lerp(gates[gateIndex], gates[nextIndex], t);
        }

        float waveTheta = (phase * Mathf.PI * 2f * verticalWaveFrequency) + (platformIndex * 0.55f);
        localOffset.y += Mathf.Sin(waveTheta) * verticalWaveAmplitude;

        Vector3 targetPosition = _anchorPosition + localOffset;
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
        float radiusX,
        float radiusZ,
        float cycleDuration,
        float gateHoldTime,
        float phaseDegrees,
        Transform assignedPassenger)
    {
        this.platformIndex = Mathf.Max(0, platformIndex);
        this.radiusX = Mathf.Max(0.2f, radiusX);
        this.radiusZ = Mathf.Max(0.2f, radiusZ);
        this.cycleDuration = Mathf.Max(0.5f, cycleDuration);
        this.gateHoldTime = Mathf.Max(0f, gateHoldTime);
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
        radiusX = Mathf.Max(0.2f, radiusX);
        radiusZ = Mathf.Max(0.2f, radiusZ);
        cycleDuration = Mathf.Max(0.5f, cycleDuration);
        gateHoldTime = Mathf.Max(0f, gateHoldTime);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        indexPhaseStepDegrees = Mathf.Repeat(indexPhaseStepDegrees, 360f);
        platformIndex = Mathf.Max(0, platformIndex);
        liftPerGate = Mathf.Clamp(liftPerGate, 0f, 2f);
        verticalWaveAmplitude = Mathf.Max(0f, verticalWaveAmplitude);
        verticalWaveFrequency = Mathf.Max(0f, verticalWaveFrequency);
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

    private Vector3[] BuildGates()
    {
        switch (gatePattern)
        {
            case GatePattern.DiamondGate:
                return new[]
                {
                    new Vector3(0f, 0f, radiusZ),
                    new Vector3(radiusX, liftPerGate, 0f),
                    new Vector3(0f, liftPerGate * 2f, -radiusZ),
                    new Vector3(-radiusX, liftPerGate, 0f)
                };

            case GatePattern.SkewedCross:
                return new[]
                {
                    new Vector3(radiusX * 0.85f, 0f, radiusZ * 0.2f),
                    new Vector3(radiusX * 0.1f, liftPerGate * 2f, radiusZ),
                    new Vector3(-radiusX, 0f, -radiusZ * 0.15f),
                    new Vector3(-radiusX * 0.2f, liftPerGate * 2f, -radiusZ)
                };

            default:
                return new[]
                {
                    new Vector3(radiusX * 0.75f, 0f, radiusZ * 0.65f),
                    new Vector3(radiusX * 0.2f, liftPerGate * 1.4f, -radiusZ),
                    new Vector3(-radiusX, liftPerGate * 2.1f, -radiusZ * 0.2f),
                    new Vector3(-radiusX * 0.35f, liftPerGate * 3f, radiusZ)
                };
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
}
