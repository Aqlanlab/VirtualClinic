using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class OpenXREyeDataRecorder : MonoBehaviour
{
    private InputAction gazePositionAction;
    private InputAction gazeRotationAction;

    private StreamWriter writer;
    private string filePath;

    [Header("Recording Settings")]
    public bool recordOnStart = true;
    public float sampleRate = 30f;
    public float rayDistance = 50f;

    private float nextSampleTime;

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

    private void Start()
    {
        if (recordOnStart)
        {
            StartRecording();
        }
    }

    private void Update()
    {
        if (writer == null)
        {
            return;
        }

        if (Time.time < nextSampleTime)
        {
            return;
        }

        nextSampleTime = Time.time + (1f / sampleRate);

        RecordEyeData();
    }

    private void RecordEyeData()
    {
        Vector3 gazePosition = gazePositionAction.ReadValue<Vector3>();
        Quaternion gazeRotation = gazeRotationAction.ReadValue<Quaternion>();

        bool gazeValid = !(gazePosition == Vector3.zero && gazeRotation == Quaternion.identity);

        Vector3 gazeDirection = gazeRotation * Vector3.forward;

        string hitObject = "";
        Vector3 hitPoint = Vector3.zero;
        float hitDistance = -1f;

        if (gazeValid)
        {
            Debug.DrawRay(gazePosition, gazeDirection * rayDistance, Color.red);

            if (Physics.Raycast(gazePosition, gazeDirection, out RaycastHit hit, rayDistance))
            {
                hitObject = hit.collider.gameObject.name;
                hitPoint = hit.point;
                hitDistance = hit.distance;
            }
        }

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16}",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Time.time,
            gazeValid,
            gazePosition.x,
            gazePosition.y,
            gazePosition.z,
            gazeDirection.x,
            gazeDirection.y,
            gazeDirection.z,
            hitObject,
            hitPoint.x,
            hitPoint.y,
            hitPoint.z,
            hitDistance,
            Camera.main != null ? Camera.main.transform.position.x : 0f,
            Camera.main != null ? Camera.main.transform.position.y : 0f,
            Camera.main != null ? Camera.main.transform.position.z : 0f
        );

        writer.WriteLine(line);
    }

    public void StartRecording()
    {
        if (writer != null)
        {
            return;
        }

        string folderPath = Path.Combine(Application.persistentDataPath, "EyeTrackingData");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "eye_tracking_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
        filePath = Path.Combine(folderPath, fileName);

        writer = new StreamWriter(filePath);

        writer.WriteLine(
            "RealTime,UnityTime,GazeValid," +
            "GazeOriginX,GazeOriginY,GazeOriginZ," +
            "GazeDirX,GazeDirY,GazeDirZ," +
            "HitObject,HitPointX,HitPointY,HitPointZ,HitDistance," +
            "HeadPosX,HeadPosY,HeadPosZ"
        );

        Debug.Log("Started eye tracking recording: " + filePath);
    }

    public void StopRecording()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Close();
        writer = null;

        Debug.Log("Stopped eye tracking recording. File saved to: " + filePath);
    }

    private void OnDisable()
    {
        StopRecording();

        gazePositionAction.Disable();
        gazeRotationAction.Disable();

        gazePositionAction.Dispose();
        gazeRotationAction.Dispose();
    }

    private void OnApplicationQuit()
    {
        StopRecording();
    }
}