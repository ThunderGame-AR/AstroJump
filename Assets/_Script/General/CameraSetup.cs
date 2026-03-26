using UnityEngine;

public class CameraSetup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform xrOrigin;

    [Header("First Person Settings")]
    [SerializeField] private Vector3 cameraPivotPositionFP;
    [SerializeField] private Vector3 xrOriginPositionFP;

    [Header("Third Person Settings")]
    [SerializeField] private Vector3 cameraPivotPositionTP;
    [SerializeField] private Vector3 xrOriginPositionTP;

    void Start()
    {
        ApplyCameraSettings();
    }

    public void ApplyCameraSettings()
    {
        if (CameraModeManager.Instance == null) return;

        if (CameraModeManager.Instance.currentViewMode == CameraModeManager.ViewMode.FirstPerson)
        {
            cameraPivot.localPosition = cameraPivotPositionFP;
            xrOrigin.localPosition = xrOriginPositionFP;
        }
        else
        {
            cameraPivot.localPosition = cameraPivotPositionTP;
            xrOrigin.localPosition = xrOriginPositionTP;
        }
    }
}