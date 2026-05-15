using UnityEngine;
using UnityEngine.InputSystem;

public class OpenXREyeTrackingManager : MonoBehaviour
{
    private InputAction gazePositionAction;
    private InputAction gazeRotationAction;

    public float rayDistance = 20f;

    private void OnEnable()
    {
        gazePositionAction = new InputAction(
            "Eye Gaze Position",
            InputActionType.PassThrough,
            "<EyeGaze>/pose/position"
        );

        gazeRotationAction = new InputAction(
            "Eye Gaze Rotation",
            InputActionType.PassThrough,
            "<EyeGaze>/pose/rotation"
        );

        gazePositionAction.Enable();
        gazeRotationAction.Enable();
    }

    private void OnDisable()
    {
        gazePositionAction.Disable();
        gazeRotationAction.Disable();

        gazePositionAction.Dispose();
        gazeRotationAction.Dispose();
    }

    private void Update()
    {
        Vector3 gazePosition = gazePositionAction.ReadValue<Vector3>();
        Quaternion gazeRotation = gazeRotationAction.ReadValue<Quaternion>();

        if (gazeRotation == Quaternion.identity && gazePosition == Vector3.zero)
        {
            return;
        }

        Vector3 gazeDirection = gazeRotation * Vector3.forward;

        Debug.DrawRay(gazePosition, gazeDirection * rayDistance, Color.red);

        if (Physics.Raycast(gazePosition, gazeDirection, out RaycastHit hit, rayDistance))
        {
            Debug.Log("Looking at: " + hit.collider.gameObject.name);
        }
    }
}