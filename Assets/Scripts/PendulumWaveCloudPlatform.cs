using UnityEngine;

[DisallowMultipleComponent]
public class PendulumWaveCloudPlatform : MonoBehaviour
{
    public enum WaveformMode
    {
        PureSine = 0,
        SoftPulse = 1,
        CrestHold = 2,
        Interference = 3
    }

    [Header("Pendulum Wave")]
    [SerializeField] private WaveformMode waveformMode = WaveformMode.Interference;
    [SerializeField] private float amplitude = 2.8f;
    [SerializeField] private float baseCycleDuration = 4.2f;
    [SerializeField] private float cycleStepPerIndex = 0.2f;
    [SerializeField, Min(0)] private int blockIndex = 0;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField, Range(0f, 1f)] private float harmonicBlend = 0.35f;
    [SerializeField, Range(0f, 0.95f)] private float envelopeAmount = 0.24f;
    [SerializeField] private float envelopeCycleDuration = 11f;
    [SerializeField] private bool useGlobalClock = true;
    [SerializeField] private bool reverseDirection = false;
    [SerializeField] private float directionFlipInterval = 0f;

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

        double now = Time.timeAsDouble;
        double elapsed = useGlobalClock ? now : (now - _startTime);
        float elapsedSeconds = (float)elapsed;
        float travelSeconds = directionFlipInterval > 0f
            ? Mathf.PingPong(elapsedSeconds, Mathf.Max(0.05f, directionFlipInterval))
            : elapsedSeconds;

        float direction = reverseDirection ? -1f : 1f;
        float cycleDuration = Mathf.Max(0.15f, baseCycleDuration + (cycleStepPerIndex * blockIndex));
        float theta = ((travelSeconds / cycleDuration) * Mathf.PI * 2f * direction)
            + (phaseOffsetDegrees * Mathf.Deg2Rad);

        float wave = EvaluateWave(theta);
        float envelopeDuration = Mathf.Max(0.2f, envelopeCycleDuration);
        float envelopeTheta = (elapsedSeconds / envelopeDuration) * Mathf.PI * 2f;
        float envelope = 1f + (Mathf.Sin(envelopeTheta + (blockIndex * 0.45f)) * envelopeAmount);
        float verticalOffset = wave * amplitude * envelope;

        Vector3 targetPosition = _anchorPosition + (Vector3.up * verticalOffset);
        Vector3 delta = targetPosition - _lastPosition;

        transform.position = targetPosition;
        if (passenger != null)
        {
            passenger.position += delta;
        }

        _lastPosition = targetPosition;
    }

    public void Configure(
        int blockIndex,
        float amplitude,
        float baseCycleDuration,
        float cycleStepPerIndex,
        float phaseDegrees,
        float harmonicBlend,
        float envelopeAmount,
        float envelopeCycleDuration,
        Transform assignedPassenger)
    {
        this.blockIndex = Mathf.Max(0, blockIndex);
        this.amplitude = Mathf.Max(0.05f, amplitude);
        this.baseCycleDuration = Mathf.Max(0.15f, baseCycleDuration);
        this.cycleStepPerIndex = Mathf.Max(0f, cycleStepPerIndex);
        phaseOffsetDegrees = Mathf.Repeat(phaseDegrees, 360f);
        this.harmonicBlend = Mathf.Clamp01(harmonicBlend);
        this.envelopeAmount = Mathf.Clamp(envelopeAmount, 0f, 0.95f);
        this.envelopeCycleDuration = Mathf.Max(0.2f, envelopeCycleDuration);
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
        amplitude = Mathf.Max(0.05f, amplitude);
        baseCycleDuration = Mathf.Max(0.15f, baseCycleDuration);
        cycleStepPerIndex = Mathf.Max(0f, cycleStepPerIndex);
        blockIndex = Mathf.Max(0, blockIndex);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        harmonicBlend = Mathf.Clamp01(harmonicBlend);
        envelopeAmount = Mathf.Clamp(envelopeAmount, 0f, 0.95f);
        envelopeCycleDuration = Mathf.Max(0.2f, envelopeCycleDuration);
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

    private float EvaluateWave(float theta)
    {
        switch (waveformMode)
        {
            case WaveformMode.PureSine:
                return Mathf.Sin(theta);

            case WaveformMode.SoftPulse:
                return (Mathf.Sin(theta) * 0.75f) + (Mathf.Sin((theta * 2f) + 0.8f) * 0.25f);

            case WaveformMode.CrestHold:
                float sine = Mathf.Sin(theta);
                return Mathf.Sign(sine) * Mathf.Pow(Mathf.Abs(sine), 0.55f);

            default:
                float interference = (Mathf.Sin(theta) + Mathf.Sin((theta * 1.618f) + 1.1f)) * 0.5f;
                float harmonic = Mathf.Sin((theta * 3f) + (blockIndex * 0.7f)) * 0.45f;
                return interference + (harmonic * harmonicBlend);
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
        Gizmos.color = new Color(0.95f, 0.3f, 0.1f, 1f);
        Gizmos.DrawLine(center + (Vector3.up * amplitude), center - (Vector3.up * amplitude));
        Gizmos.DrawSphere(center, 0.1f);
    }
}
