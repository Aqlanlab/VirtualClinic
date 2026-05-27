using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class PaintingEyeTrackerRecorder : MonoBehaviour
{
    [Header("Painting Setup")]
    public Collider paintingCollider;

    [Header("Recording Settings")]
    public float sampleRate = 30f;
    public float rayDistance = 50f;

    [Header("Debug")]
    public bool showDebugRay = true;

    private InputAction gazePositionAction;
    private InputAction gazeRotationAction;

    private StreamWriter writer;
    private string filePath;
    private bool isRecording = false;
    private float nextSampleTime = 0f;

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

    private void Update()
    {
        if (!isRecording)
        {
            return;
        }

        if (Time.time < nextSampleTime)
        {
            return;
        }

        nextSampleTime = Time.time + (1f / sampleRate);

        RecordEyeSample();
    }

    private void RecordEyeSample()
    {
        Vector3 gazePosition = gazePositionAction.ReadValue<Vector3>();
        Quaternion gazeRotation = gazeRotationAction.ReadValue<Quaternion>();

        bool gazeValid = !(gazePosition == Vector3.zero && gazeRotation == Quaternion.identity);

        Vector3 gazeDirection = gazeRotation * Vector3.forward;

        bool hitPainting = false;
        Vector2 imageUV = new(-1f, -1f);
        Vector3 hitPoint = Vector3.zero;
        string zoneName = "None";

        if (gazeValid)
        {
            if (showDebugRay)
            {
                Debug.DrawRay(gazePosition, gazeDirection * rayDistance, Color.red);
            }

            Ray ray = new(gazePosition, gazeDirection);

            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                if (hit.collider == paintingCollider)
                {
                    hitPainting = true;
                    hitPoint = hit.point;

                    // For a Quad or Plane with a Mesh Collider,
                    // this gives the position on the image from 0 to 1.
                    imageUV = hit.textureCoord;

                    zoneName = GetZoneName(imageUV);
                }
            }
        }

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Time.time,
            gazeValid,
            hitPainting,
            imageUV.x,
            imageUV.y,
            zoneName,
            hitPoint.x,
            hitPoint.y,
            hitPoint.z,
            gazePosition.x,
            gazePosition.y,
            gazePosition.z
        );

        writer.WriteLine(line);
    }

    private string GetZoneName(Vector2 uv)
    {
        if (uv.x < 0f || uv.y < 0f)
        {
            return "None";
        }

        // Splits painting into 9 zones.
        if (uv.y >= 0.66f)
        {
            if (uv.x < 0.33f) return "Top Left";
            if (uv.x < 0.66f) return "Top Center";
            return "Top Right";
        }

        if (uv.y >= 0.33f)
        {
            if (uv.x < 0.33f) return "Middle Left";
            if (uv.x < 0.66f) return "Middle Center";
            return "Middle Right";
        }

        if (uv.x < 0.33f) return "Bottom Left";
        if (uv.x < 0.66f) return "Bottom Center";
        return "Bottom Right";
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            return;
        }

        string folderPath = Path.Combine(Application.persistentDataPath, "EyeTrackingData");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "painting_eye_tracking_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
        filePath = Path.Combine(folderPath, fileName);

        writer = new StreamWriter(filePath);

        writer.WriteLine(
            "RealTime,UnityTime,GazeValid,HitPainting,ImageX,ImageY,ZoneName," +
            "HitPointX,HitPointY,HitPointZ,GazeOriginX,GazeOriginY,GazeOriginZ"
        );

        isRecording = true;
        nextSampleTime = Time.time;

        Debug.Log("Started eye tracking recording: " + filePath);
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

        Debug.Log("Stopped eye tracking recording. Saved to: " + filePath);
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