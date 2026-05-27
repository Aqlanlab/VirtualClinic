using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class RawEyeDataCSVTest : MonoBehaviour
{
    public float sampleRate = 30f;

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
        Vector3 gazePosition = gazePositionAction.ReadValue<Vector3>();
        Quaternion gazeRotation = gazeRotationAction.ReadValue<Quaternion>();
        Vector3 gazeDirection = gazeRotation * Vector3.forward;

        bool hasEyeData = !(gazePosition == Vector3.zero && gazeRotation == Quaternion.identity);

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Time.time,
            hasEyeData,
            gazePosition.x,
            gazePosition.y,
            gazePosition.z,
            gazeRotation.x,
            gazeRotation.y,
            gazeRotation.z,
            gazeRotation.w,
            gazeDirection.x,
            gazeDirection.y,
            gazeDirection.z
        );

        writer.WriteLine(line);

        Debug.Log(
            "Eye Data: " +
            "Valid=" + hasEyeData +
            " Pos=" + gazePosition +
            " Rot=" + gazeRotation +
            " Dir=" + gazeDirection
        );
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

    public void StartRecording()
    {
        if (isRecording)
        {
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "EyeTrackingData");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "raw_eye_data_test_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
        filePath = Path.Combine(folderPath, fileName);

        writer = new StreamWriter(filePath);

        writer.WriteLine(
            "RealTime,UnityTime,HasEyeData," +
            "GazePosX,GazePosY,GazePosZ," +
            "GazeRotX,GazeRotY,GazeRotZ,GazeRotW," +
            "GazeDirX,GazeDirY,GazeDirZ"
        );

        isRecording = true;
        nextSampleTime = Time.time;

        Debug.Log("Started raw eye data recording.");
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

        Debug.Log("Stopped raw eye data recording.");
        Debug.Log("Saved CSV to: " + filePath);
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