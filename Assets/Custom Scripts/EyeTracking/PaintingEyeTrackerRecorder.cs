using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
public class PaintingEyeTrackingRecorder : MonoBehaviour
{
    [Header("Painting")]
    [Tooltip("Assign the Mesh Collider on the painting Quad.")]
    public Collider paintingCollider;

    [Header("Eye Gaze")]
    [Tooltip("Assign MainUser/Camera Offset/Gaze Stabilized.")]
    public Transform gazeRayOrigin;

    [Header("Recording Settings")]
    [Min(1f)]
    public float sampleRate = 30f;

    [Min(0.1f)]
    public float rayDistance = 50f;

    [Header("Testing")]
    public Key toggleKey = Key.R;
    public bool recordOnStart = false;

    [Header("Debug")]
    public bool drawDebugRay = true;
    public bool logPaintingHits = true;

    private InputAction gazeTrackedAction;

    private StreamWriter writer;
    private string filePath;

    private bool isRecording;
    private float nextSampleTime;
    private int samplesSinceFlush;
    private InputAction gazeRotationAction;



    private void OnEnable()
    {
        gazeTrackedAction = new InputAction(
            name: "Eye Gaze Tracked",
            type: InputActionType.PassThrough,
            binding: "<EyeGaze>/pose/isTracked"
        );
        gazeRotationAction = new InputAction(
            name: "Eye Gaze Rotation",
            type: InputActionType.PassThrough,
            binding: "<EyeGaze>/pose/rotation"
        );

        gazeRotationAction.Enable();

        gazeTrackedAction.Enable();
        if (gazeRayOrigin == null)
        {
            gazeRayOrigin = Camera.main.transform;
        }
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
        // Press the selected keyboard key to start or stop recording.
        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
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

        float safeSampleRate = Mathf.Max(sampleRate, 1f);
        nextSampleTime = Time.time + (1f / safeSampleRate);

        RecordSample();
    }

    private void RecordSample()
    {
        if (writer == null || gazeRayOrigin == null || paintingCollider == null)
        {
            return;
        }

        // Checks whether OpenXR currently reports valid eye tracking.


        // Gaze Stabilized is already positioned in Unity world space.
        Vector3 gazeOrigin = gazeRayOrigin.position;

        Quaternion gazeRotation = gazeRotationAction.ReadValue<Quaternion>();

        Vector3 gazeDirection = (gazeRotation * Vector3.forward).normalized;

        // Flip only if the ray is backwards
        //gazeDirection = -gazeDirection;

        bool gazeValid =
            Mathf.Abs(gazeRotation.x) > 0.0001f ||
            Mathf.Abs(gazeRotation.y) > 0.0001f ||
            Mathf.Abs(gazeRotation.z) > 0.0001f ||
            Mathf.Abs(gazeDirection.x) > 0.0001f ||
            Mathf.Abs(gazeDirection.y) > 0.0001f;

        bool hitPainting = false;

        // -1 means that the gaze did not hit the painting.
        Vector2 imageUV =
            new Vector2(-1f, -1f);

        Vector3 hitPoint =
            Vector3.zero;

        float hitDistance = -1f;
       


        if (gazeValid)
        {
            if (drawDebugRay)
            {
                Debug.DrawRay(gazeOrigin, Camera.main.transform.forward * 5f, Color.green, 0.1f);
                Debug.DrawRay(gazeOrigin, gazeDirection * 5f, Color.blue, 0.1f);
            }

            Ray gazeRay =
                new Ray(gazeOrigin, gazeDirection);

            // Tests only the painting collider.
            // Other objects such as the wall or floor cannot block this test.
            if (paintingCollider.Raycast(
                gazeRay,
                out RaycastHit hit,
                rayDistance))
            {
                hitPainting = true;

                // UV coordinates range from 0 to 1.
                // These are the values used for the future heat map.
                imageUV = hit.textureCoord;

                hitPoint = hit.point;
                hitDistance = hit.distance;

                if (logPaintingHits)
                {
                    Debug.Log(
                        "Painting hit at UV: " + imageUV
                    );
                }
            }
        }

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2}," +
            "{3},{4},{5}," +
            "{6},{7},{8},{9}," +
            "{10},{11},{12}," +
            "{13},{14},{15}," +
            "{16},{17},{18},{19}",
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

        samplesSinceFlush++;

        // Periodically save data without writing to the drive every sample.
        if (samplesSinceFlush >= sampleRate)
        {
            writer.Flush();
            samplesSinceFlush = 0;
        }
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            return;
        }

        if (paintingCollider == null)
        {
            Debug.LogError(
                "Painting Collider is not assigned."
            );
            return;
        }

        if (gazeRayOrigin == null)
        {
            Debug.LogError(
                "Gaze Ray Origin is not assigned."
            );
            return;
        }

        // Creates EyeTrackingData beside the Assets folder.
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrEmpty(projectRoot))
        {
            Debug.LogError(
                "Could not locate the Unity project folder."
            );
            return;
        }

        string folderPath =
            Path.Combine(projectRoot, "EyeTrackingData");

        Directory.CreateDirectory(folderPath);

        string fileName =
            "painting_eye_tracking_" +
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
            ".csv";

        filePath =
            Path.Combine(folderPath, fileName);

        try
        {
            writer = new StreamWriter(
                filePath,
                append: false
            );

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
            samplesSinceFlush = 0;

            Debug.Log("Eye tracking recording started.");
            Debug.Log("CSV path: " + filePath);
        }
        catch (Exception exception)
        {
            writer = null;
            isRecording = false;

            Debug.LogError(
                "Could not create the CSV file: " +
                exception.Message
            );
        }
    }

    public void StopRecording()
    {
        if (!isRecording && writer == null)
        {
            return;
        }

        isRecording = false;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer.Dispose();
            writer = null;
        }

        Debug.Log("Eye tracking recording stopped.");
        Debug.Log("Saved CSV to: " + filePath);
    }

    // This can also be connected to a Unity UI or VR button.
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

        if (gazeRotationAction != null)
        {
            gazeRotationAction.Disable();
            gazeRotationAction.Dispose();
            gazeRotationAction = null;
        }
    }

    private void OnApplicationQuit()
    {
        StopRecording();
    }
}