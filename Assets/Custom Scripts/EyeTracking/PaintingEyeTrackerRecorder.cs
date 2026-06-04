using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class PaintingEyeTrackingRecorder : MonoBehaviour
{
    [Header("Painting")]
    public Collider paintingCollider;

    [Header("Recording Settings")]
    public float sampleRate = 30f;
    public float rayDistance = 50f;

    [Header("Testing")]
    public KeyCode toggleKey = KeyCode.R;
    public bool recordOnStart = false;

    [Header("Debug")]
    public bool drawDebugRay = true;

    private InputAction gazePositionAction;
    private InputAction gazeRotationAction;

    private StreamWriter writer;
    private string filePath;
    private bool isRecording;
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
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ToggleRecording();
        }

        if (!isRecording)
        {
            return;
        }

        if (Time.time < nextSampleTime)
        {
            return;
        }

        nextSampleTime = Time.time + (1f / sampleRate);

        RecordSample();
    }

    private void RecordSample()
    {
        Vector3 gazeOrigin = gazePositionAction.ReadValue<Vector3>();
        Quaternion gazeRotation = gazeRotationAction.ReadValue<Quaternion>();

        bool gazeValid = !(gazeOrigin == Vector3.zero && gazeRotation == Quaternion.identity);

        Vector3 gazeDirection = gazeRotation * Vector3.forward;

        bool hitPainting = false;
        Vector2 imageUV = new Vector2(-1f, -1f);
        Vector3 hitPoint = Vector3.zero;
        float hitDistance = -1f;

        if (gazeValid)
        {
            if (drawDebugRay)
            {
                Debug.DrawRay(gazeOrigin, gazeDirection * rayDistance, Color.red);
            }

            Ray gazeRay = new Ray(gazeOrigin, gazeDirection);

            if (Physics.Raycast(gazeRay, out RaycastHit hit, rayDistance))
            {
                if (hit.collider == paintingCollider)
                {
                    hitPainting = true;
                    imageUV = hit.textureCoord;
                    hitPoint = hit.point;
                    hitDistance = hit.distance;
                }
            }
        }

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Time.time,
            gazeValid,
            gazeOrigin.x,
            gazeOrigin.y,
            gazeOrigin.z,
            gazeRotation.x,
            gazeRotation.y,
            gazeRotation.z,
            gazeRotation.w,
            gazeDirection.x,
            gazeDirection.y,
            gazeDirection.z,
            hitPainting,
            imageUV.x,
            imageUV.y,
            hitPoint.x,
            hitPoint.y,
            hitPoint.z,
            hitDistance
        );

        writer.WriteLine(line);
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            return;
        }

        if (paintingCollider == null)
        {
            Debug.LogError("Painting Collider is not assigned.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "EyeTrackingData");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "painting_eye_tracking_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
        filePath = Path.Combine(folderPath, fileName);

        writer = new StreamWriter(filePath);

        writer.WriteLine(
            "RealTime,UnityTime,GazeValid," +
            "GazeOriginX,GazeOriginY,GazeOriginZ," +
            "GazeRotX,GazeRotY,GazeRotZ,GazeRotW," +
            "GazeDirX,GazeDirY,GazeDirZ," +
            "HitPainting,ImageX,ImageY," +
            "HitPointX,HitPointY,HitPointZ,HitDistance"
        );

        isRecording = true;
        nextSampleTime = Time.time;

        Debug.Log("Eye tracking recording started.");
        Debug.Log("CSV path: " + filePath);
    }

    public void StopRecording()
    {
        if (!isRecording)
        {
            return;
        }

        isRecording = false;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }

        Debug.Log("Eye tracking recording stopped.");
        Debug.Log("Saved CSV to: " + filePath);
    }

    public void ToggleRecording()
    {
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void OnDisable()
    {
        StopRecording();

        if (gazePositionAction != null)
        {
            gazePositionAction.Disable();
            gazePositionAction.Dispose();
        }

        if (gazeRotationAction != null)
        {
            gazeRotationAction.Disable();
            gazeRotationAction.Dispose();
        }
    }

    private void OnApplicationQuit()
    {
        StopRecording();
    }
}