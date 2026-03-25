using UnityEngine;

[DisallowMultipleComponent]
public class MobiusRibbonCloudPlatform : MonoBehaviour
{
    [Header("Mobius Ribbon")]
    [SerializeField] private float majorRadius = 3.2f;
    [SerializeField] private float laneOffset = 0f;
    [SerializeField] private float cycleDuration = 7.2f;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField] private float twistMultiplier = 1f;
    [SerializeField] private float verticalScale = 1.4f;
    [SerializeField] private float bobAmplitude = 0.22f;
    [SerializeField] private float bobFrequency = 1.15f;
    [SerializeField] private bool reverseDirection = false;
    [SerializeField] private float directionFlipInterval = 0f;
    [SerializeField] private bool useGlobalClock = true;
    [SerializeField, Min(0)] private int platformIndex = 0;

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
        float theta = ((travel / duration) * Mathf.PI * 2f * direction)
            + (phaseOffsetDegrees * Mathf.Deg2Rad)
            + (platformIndex * 0.2f);

        float twistTheta = theta * 0.5f * Mathf.Max(0.2f, twistMultiplier);
        float ribbonRadial = majorRadius + (laneOffset * Mathf.Cos(twistTheta));
        float x = Mathf.Cos(theta) * ribbonRadial;
        float z = Mathf.Sin(theta) * ribbonRadial;
        float y = (laneOffset * Mathf.Sin(twistTheta) * verticalScale)
            + (Mathf.Sin((theta * bobFrequency) + (platformIndex * 0.35f)) * bobAmplitude);

        Vector3 targetPosition = _anchorPosition + new Vector3(x, y, z);
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
        float laneOffset,
        float cycleDuration,
        float phaseDegrees,
        float twistMultiplier,
        float verticalScale,
        float bobAmplitude,
        float bobFrequency,
        bool reverseDirection,
        float directionFlipInterval,
        Transform assignedPassenger)
    {
        this.platformIndex = Mathf.Max(0, platformIndex);
        this.majorRadius = Mathf.Max(0.2f, majorRadius);
        this.laneOffset = laneOffset;
        this.cycleDuration = Mathf.Max(0.1f, cycleDuration);
        phaseOffsetDegrees = Mathf.Repeat(phaseDegrees, 360f);
        this.twistMultiplier = Mathf.Max(0.2f, twistMultiplier);
        this.verticalScale = Mathf.Max(0f, verticalScale);
        this.bobAmplitude = Mathf.Max(0f, bobAmplitude);
        this.bobFrequency = Mathf.Max(0f, bobFrequency);
        this.reverseDirection = reverseDirection;
        this.directionFlipInterval = Mathf.Max(0f, directionFlipInterval);
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
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        phaseOffsetDegrees = Mathf.Repeat(phaseOffsetDegrees, 360f);
        twistMultiplier = Mathf.Max(0.2f, twistMultiplier);
        verticalScale = Mathf.Max(0f, verticalScale);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        directionFlipInterval = Mathf.Max(0f, directionFlipInterval);
        platformIndex = Mathf.Max(0, platformIndex);

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
