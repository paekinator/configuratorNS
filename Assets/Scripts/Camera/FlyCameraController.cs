using UnityEngine;
using UnityEngine.EventSystems;

public class FlyCameraController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 2.5f;
    public float verticalSpeed = 6f;     // Q/E speed
    public float accel = 12f;            // smoothing
    public bool allowScrollToChangeSpeed = true;
    public float scrollSpeedStep = 1.0f;
    public float minMoveSpeed = 1f;
    public float maxMoveSpeed = 30f;

    [Header("Look (RMB)")]
    public float lookSensitivity = 2.0f;
    public bool invertY = false;
    public float pitchMin = -80f;
    public float pitchMax = 80f;

    [Header("Behavior")]
    public bool requireRmbForMove = false;     // set true if you only want WASD while RMB held
    public bool ignoreInputOverUI = true;

    private Vector3 _vel; // smooth velocity
    private float _yaw;
    private float _pitch;

    void Awake()
    {
        // Initialize yaw/pitch from current rotation
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = NormalizePitch(e.x);
    }

    void Update()
    {
        if (ignoreInputOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        bool rmb = Input.GetMouseButton(1);

        // RMB look
        if (rmb)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            float sy = invertY ? 1f : -1f;

            _yaw += mx * lookSensitivity;
            _pitch += my * lookSensitivity * sy;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Optional: scroll changes base speed
        if (allowScrollToChangeSpeed && !rmb) // change if you want scroll always
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                moveSpeed = Mathf.Clamp(moveSpeed + scroll * scrollSpeedStep, minMoveSpeed, maxMoveSpeed);
            }
        }

        // Movement input (WASD + QE)
        if (requireRmbForMove && !rmb)
        {
            // smooth stop when not moving
            _vel = Vector3.Lerp(_vel, Vector3.zero, 1f - Mathf.Exp(-accel * Time.deltaTime));
            transform.position += _vel * Time.deltaTime;
            return;
        }

        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        float y = 0f;
        if (Input.GetKey(KeyCode.Space)) y += 1f;
        if (Input.GetKey(KeyCode.LeftShift)) y -= 1f;

        Vector3 input = new Vector3(x, y, z);
        if (input.sqrMagnitude > 1f) input.Normalize();

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= sprintMultiplier;

        // Move relative to camera facing (local axes)
        Vector3 desired = (transform.right * input.x + transform.up * input.y + transform.forward * input.z) * speed;

        // Smooth acceleration
        _vel = Vector3.Lerp(_vel, desired, 1f - Mathf.Exp(-accel * Time.deltaTime));
        transform.position += _vel * Time.deltaTime;
    }

    static float NormalizePitch(float pitch)
    {
        if (pitch > 180f) pitch -= 360f;
        return pitch;
    }
}