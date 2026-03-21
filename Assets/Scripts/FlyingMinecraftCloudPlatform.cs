using UnityEngine;

/// <summary>
/// Block đi qua đi lại (ping-pong) theo một quỹ đạo.
/// Đặt ở đâu thì bắt đầu từ đó, di chuyển thêm travelOffset rồi quay lại.
/// Có thể gán một GameObject (passenger) để di chuyển cùng.
/// </summary>
[DisallowMultipleComponent]
public class FlyingMinecraftCloudPlatform : MonoBehaviour
{
    [Header("Quỹ đạo")]
    [Tooltip("Di chuyển thêm bao nhiêu so với vị trí hiện tại. VD: (10,0,0) = đi ngang 10 đơn vị.")]
    [SerializeField] private Vector3 travelOffset = new Vector3(10f, 0f, 0f);

    [Tooltip("Thời gian đi hết 1 chiều (giây).")]
    [SerializeField] private float duration = 4f;

    [Tooltip("Dừng bao lâu ở mỗi đầu trước khi quay lại.")]
    [SerializeField] private float holdTime = 0.3f;

    [Tooltip("Mượt đầu cuối (ease in/out).")]
    [SerializeField] private bool smooth = true;

    [Header("Passenger - Object di chuyển theo")]
    [Tooltip("Kéo GameObject vào đây, nó sẽ di chuyển theo cùng quỹ đạo.")]
    [SerializeField] private Transform passenger;

    [Tooltip("Tích vào để tự động đặt passenger lên đỉnh mây (trong Editor).")]
    [SerializeField] private bool snapPassengerToTop = false;

    // Runtime
    private Vector3 _startPos;       // Vị trí gốc (world) khi bắt đầu Play
    private Vector3 _passengerStart; // Vị trí gốc của passenger khi bắt đầu Play
    private float _progress;         // 0 → 1
    private int _direction = 1;      // 1 = đi, -1 = về
    private float _holdTimer;
    private bool _initialized;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // Ghi nhớ vị trí hiện tại làm gốc
        _startPos = transform.position;

        if (passenger != null)
        {
            _passengerStart = passenger.position;
        }

        _progress = 0f;
        _direction = 1;
        _holdTimer = 0f;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        Init(); // Đảm bảo đã khởi tạo

        // Đang dừng ở đầu/cuối
        if (_holdTimer > 0f)
        {
            _holdTimer -= Time.deltaTime;
            return;
        }

        // Tính progress
        float dur = Mathf.Max(0.1f, duration);
        _progress += (Time.deltaTime / dur) * _direction;

        // Đến đầu hoặc cuối → đổi chiều
        if (_progress >= 1f)
        {
            _progress = 1f;
            _direction = -1;
            _holdTimer = holdTime;
        }
        else if (_progress <= 0f)
        {
            _progress = 0f;
            _direction = 1;
            _holdTimer = holdTime;
        }

        // Áp dụng vị trí
        float t = smooth ? Mathf.SmoothStep(0f, 1f, _progress) : _progress;
        Vector3 offset = travelOffset * t;

        transform.position = _startPos + offset;

        if (passenger != null)
        {
            passenger.position = _passengerStart + offset;
        }
    }

    // ─── Snap Passenger: tự động đặt passenger lên đỉnh mây ───

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (snapPassengerToTop && passenger != null && !Application.isPlaying)
        {
            // Delay 1 frame vì OnValidate không cho phép sửa transform trực tiếp
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || passenger == null) return;
                SnapPassengerOnTop();
            };
        }
    }
#endif

    [ContextMenu("Snap Passenger To Top")]
    private void SnapPassengerOnTop()
    {
        if (passenger == null) return;

        // Tính đỉnh của cloud
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

        // Tính đáy chân của passenger
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

        // Đặt passenger lên đỉnh cloud
        passenger.position = new Vector3(topPos.x, topPos.y + feetOffset, topPos.z);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(passenger);
#endif
    }

    // ─── Gizmo: vẽ quỹ đạo trong Editor ───
    private void OnDrawGizmosSelected()
    {
        Vector3 from = Application.isPlaying ? _startPos : transform.position;
        Vector3 to = from + travelOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawSphere(from, 0.15f);
        Gizmos.DrawSphere(to, 0.15f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(Vector3.Lerp(from, to, 0.5f), 0.1f);
    }
}
