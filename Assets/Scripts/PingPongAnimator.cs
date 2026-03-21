using UnityEngine;

/// <summary>
/// Script Ping-Pong siêu ổn định, tự động đồng bộ hóa với Animator gốc.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PingPongAnimator : MonoBehaviour
{
    [Header("Cấu hình")]
    [Tooltip("Tốc độ chạy.")]
    [Range(0.01f, 10f)]
    [SerializeField] private float playbackSpeed = 1.0f;

    private Animator _animator;
    private int _stateHash = 0;
    private float _normalizedTime = 0f;
    private bool _isMovingForward = true;
    private float _clipLength = 1f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (_animator.runtimeAnimatorController == null) return;

        // 1. TỰ ĐỘNG KHỞI TẠO: Lấy thông tin Animation khi sẵn sàng
        if (_stateHash == 0)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            
            // Nếu Hash = 0, nghĩa là Animator chưa kịp khởi động, đợi frame sau
            if (stateInfo.fullPathHash == 0) return;

            _stateHash = stateInfo.fullPathHash;
            _clipLength = stateInfo.length;

            // Dùng Play ngay lập tức để ép Animator vào trạng thái này
            _animator.Play(_stateHash, 0, 0f);
            
            // Tắt tốc độ mặc định
            _animator.speed = 0f;
        }

        // 2. LOGIC PING-PONG
        // Sử dụng _clipLength đã lưu để tính toán bước nhảy mượt mà
        float duration = _clipLength > 0 ? _clipLength : 1f;
        float step = (playbackSpeed * Time.deltaTime) / duration;

        if (_isMovingForward)
        {
            _normalizedTime += step;
            if (_normalizedTime >= 1.0f)
            {
                _normalizedTime = 1.0f;
                _isMovingForward = false;
            }
        }
        else
        {
            _normalizedTime -= step;
            if (_normalizedTime <= 0f)
            {
                _normalizedTime = 0f;
                _isMovingForward = true;
            }
        }

        // 3. CẬP NHẬT: Dùng Update(0) để ép Animator tính toán lại frame mà không cần speed > 0
        // Đây là cách an toàn nhất cho Mobile và tránh lỗi "State not found"
        _animator.Play(_stateHash, 0, _normalizedTime);
        _animator.Update(0f); 
    }

    public void ResetToStart()
    {
        _normalizedTime = 0f;
        _isMovingForward = true;
    }
}
