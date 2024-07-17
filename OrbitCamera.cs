using Cinemachine;
using NTC.MonoCache;
using TouchControlsKit;
using UnityEngine;

internal class OrbitCamera : MonoCache
{
    [SerializeField] private float XSensitivity = 5f, YSensitivity = 8f;
    [SerializeField] internal bool canControl;
    [SerializeField] internal bool isFirstPerson { get; private set; }
    [SerializeField] private CinemachineFreeLook virtualCamera;
    private static TCKTouchpad touchpad;
    internal static OrbitCamera singleton { get; private set; }

    protected override void OnEnabled()
    {
        singleton = this;
    }

    internal void InitializeCursor(bool valueCanControl)
    {
        canControl = valueCanControl;
        Cursor.lockState = !Application.isMobilePlatform ? (canControl ? CursorLockMode.Locked : CursorLockMode.None) : CursorLockMode.None;
        Cursor.visible = !canControl ? true : false;
    }

    internal void SetupCam(Transform player, TCKTouchpad _touchpad)
    {
        touchpad = _touchpad;
        virtualCamera.Follow = player;
        virtualCamera.LookAt = player;
    }

    protected override void Run()
    {
        if (!canControl) { return; }

        virtualCamera.m_XAxis.Value += Application.isMobilePlatform ? touchpad.axisX.value : Input.GetAxis("Mouse X") * XSensitivity;
        virtualCamera.m_YAxis.Value -= Application.isMobilePlatform ? touchpad.axisY.value : Input.GetAxis("Mouse Y") * YSensitivity;

        if (Input.GetKeyDown(KeyCode.V))
        {
            ViewSwitch();
        }
    }

    private void ViewSwitch()
    {
        isFirstPerson = !isFirstPerson;
    }
}
