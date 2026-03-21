using UnityEngine;

[DisallowMultipleComponent]
public class VerticalWaveCloudPlatform : MonoBehaviour
{
    [Header("Vertical Wave")]
    [SerializeField] private float amplitude = 2.2f;
    [SerializeField] private float cycleDuration = 4.8f;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField] private bool useGlobalClock = true;

    [Header("Passenger")]
    [SerializeField] private Transform passenger;

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
        double elapsed = useGlobalClock ? now : (now - _startTime);
        float theta = ((float)(elapsed / duration) * Mathf.PI * 2f) + (phaseOffsetDegrees * Mathf.Deg2Rad);
        float verticalOffset = Mathf.Sin(theta) * amplitude;
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
        float amplitude,
        float cycleDuration,
        float phaseDegrees,
        Transform assignedPassenger)
    {
        this.amplitude = Mathf.Max(0.05f, amplitude);
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
        amplitude = Mathf.Max(0.05f, amplitude);
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
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
}
