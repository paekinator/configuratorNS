using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;      // point we orbit around (our CameraTarget)
    public float distance = 10f;  // current distance from target
    public float zoomSpeed = 2f;  // how fast zoom changes
    public float rotateSpeed = 30f; // how fast rotation responds to mouse
    public float minDistance = 3f;
    public float maxDistance = 30f;

    float yaw = 0f;   // rotation around Y axis (left/right)
    float pitch = 30f; // rotation around X axis (up/down)

    void Start()
    {
        if (target == null)
        {
            // If no target assigned, create one at world origin
            GameObject t = new GameObject("CameraTarget_Auto");
            t.transform.position = Vector3.zero;
            target = t.transform;
        }

        // Initialize camera position based on current transform
        Vector3 dir = (transform.position - target.position);
        distance = dir.magnitude;

        // Convert current direction to yaw & pitch
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        // Zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // Rotate when holding right mouse button
        if (Input.GetMouseButton(1)) // 1 = right mouse button
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // Removed Time.deltaTime to make rotation more “direct”
            yaw   += mouseX * rotateSpeed;
            pitch -= mouseY * rotateSpeed;
            pitch = Mathf.Clamp(pitch, 10f, 80f);
        }

        // Calculate new position & rotation
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0, 0, -distance);

        transform.position = target.position + offset;
        transform.rotation = rotation;
    }
}