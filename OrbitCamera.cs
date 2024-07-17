using Cinemachine;
using UnityEngine;

internal class OrbitCamera : MonoBehaviour
{
    [SerializeField] private float XSensitivity = 5f, YSensitivity = 8f;
    [SerializeField] internal bool canControl;
    [SerializeField] internal bool isFirstPerson { get; private set; }
    [SerializeField] private CinemachineFreeLook virtualCamera;
    internal static OrbitCamera singleton { get; private set; }

    protected override void OnEnabled()
    {
        singleton = this;
    }

    internal void InitializeCursor(bool valueCanControl)
    {
        canControl = valueCanControl;
        Cursor.lockState = canControl ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !canControl ? true : false;
    }

    internal void SetupCam(Transform player, TCKTouchpad _touchpad)
    {
        touchpad = _touchpad;
        virtualCamera.Follow = player;
    }

    private void Update()
    {
        if (!canControl) { return; }

        virtualCamera.m_XAxis.Value += Input.GetAxis("Mouse X") * XSensitivity;
        virtualCamera.m_YAxis.Value -= Input.GetAxis("Mouse Y") * YSensitivity;
    }
}
