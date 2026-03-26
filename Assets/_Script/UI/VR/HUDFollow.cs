using UnityEngine;

public class HUDFollow : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private Transform targetCamera;

    [Header("PC/Desktop Mode")]
    [SerializeField][Min(0.5f)] private float pcDistance = 1f;

    [Header("VR Mode")]
    [SerializeField][Min(1.5f)] private float vrDistance = 1.75f;
    [SerializeField][Min(5f)] private float vrFollowSpeed = 10f;

    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            if (Camera.main != null) targetCamera = Camera.main.transform;
            else return;
        }

        Camera cam = targetCamera.GetComponent<Camera>();
        bool isVR = UnityEngine.XR.XRSettings.isDeviceActive;
        float dt = Time.unscaledDeltaTime;

        if (isVR)
        {
            if (cam != null) cam.nearClipPlane = 1.15f;

            Vector3 targetPosition = targetCamera.position + (targetCamera.forward * vrDistance);
            transform.position = Vector3.Lerp(transform.position, targetPosition, dt * vrFollowSpeed);
            transform.LookAt(transform.position + targetCamera.forward);
            transform.localScale = originalScale;
        }
        else
        {
            if (cam != null) cam.nearClipPlane = 0.3f;

            transform.position = targetCamera.position + (targetCamera.forward * pcDistance);
            transform.rotation = targetCamera.rotation;
            transform.localScale = originalScale;
        }
    }
}