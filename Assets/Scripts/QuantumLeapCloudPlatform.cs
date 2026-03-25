using UnityEngine;

[DisallowMultipleComponent]
public class QuantumLeapCloudPlatform : MonoBehaviour
{
    [Header("Leap Ring")]
    [SerializeField] private float radiusX = 2.8f;
    [SerializeField] private float radiusZ = 2.3f;
    [SerializeField] private float jumpDuration = 0.65f;
    [SerializeField] private float holdDuration = 0.2f;
    [SerializeField] private float arcHeight = 1.8f;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField, Min(0)] private int platformIndex = 0;
    [SerializeField, Range(3, 8)] private int nodeCount = 5;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private float jitterAmplitude = 0.1f;
    [SerializeField] private float jitterFrequency = 2.2f;
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

        float jump = Mathf.Max(0.08f, jumpDuration);
        float hold = Mathf.Max(0f, holdDuration);
        float stepDuration = jump + hold;
        int nodes = Mathf.Clamp(nodeCount, 3, 8);
        float totalDuration = stepDuration * nodes;

        double now = Time.timeAsDouble;
        float elapsed = (float)(useGlobalClock ? now : (now - _startTime));
        float phaseOffset = (phaseOffsetDegrees + (platformIndex * 29f)) / 360f;
        float loopTime = Mathf.Repeat((elapsed + (phaseOffset * totalDuration)), totalDuration);

        int stepIndex = Mathf.Clamp(Mathf.FloorToInt(loopTime / stepDuration), 0, nodes - 1);
        float timeInStep = loopTime - (stepIndex * stepDuration);
        int dir = clockwise ? 1 : -1;
        int fromIndex = stepIndex;
        int toIndex = (fromIndex + dir + nodes) % nodes;

        Vector3 from = EvaluateNode(fromIndex, nodes);
        Vector3 to = EvaluateNode(toIndex, nodes);
        Vector3 localOffset;

        if (timeInStep <= hold)
        {
            localOffset = from;
        }
        else
        {
            float t = (timeInStep - hold) / jump;
            t = Mathf.SmoothStep(0f, 1f, t);
            localOffset = Vector3.Lerp(from, to, t);
            localOffset.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
        }

        float jitterTheta = (elapsed * jitterFrequency) + (platformIndex * 0.75f);
        localOffset.x += Mathf.Sin(jitterTheta) * jitterAmplitude;
        localOffset.z += Mathf.Cos(jitterTheta * 0.8f) * jitterAmplitude;

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
        float jumpDuration,
        float holdDuration,
        float arcHeight,
        float phaseDegrees,
        Transform assignedPassenger)
    {
        this.platformIndex = Mathf.Max(0, platformIndex);
        this.radiusX = Mathf.Max(0.2f, radiusX);
        this.radiusZ = Mathf.Max(0.2f, radiusZ);
        this.jumpDuration = Mathf.Max(0.08f, jumpDuration);
        this.holdDuration = Mathf.Max(0f, holdDuration);
        this.arcHeight = Mathf.Max(0f, arcHeight);
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
        jumpDuration = Mathf.Max(0.08f, jumpDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        arcHeight = Mathf.Max(0f, arcHeight);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        platformIndex = Mathf.Max(0, platformIndex);
        nodeCount = Mathf.Clamp(nodeCount, 3, 8);
        jitterAmplitude = Mathf.Max(0f, jitterAmplitude);
        jitterFrequency = Mathf.Max(0f, jitterFrequency);

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

    private Vector3 EvaluateNode(int index, int nodes)
    {
        float angle = (index / (float)nodes) * Mathf.PI * 2f;
        float radial = (index % 2 == 0) ? 1f : 0.58f;
        return new Vector3(
            Mathf.Cos(angle) * radiusX * radial,
            0f,
            Mathf.Sin(angle) * radiusZ * radial);
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
