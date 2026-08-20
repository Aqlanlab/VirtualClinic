using UnityEngine;

public class StartPaintingExperience : MonoBehaviour
{
    [Header("Recorders")]
    [SerializeField]
    private PaintingEyeTrackingRecorder eyeTrackingRecorder;

    [SerializeField]
    private SpeechRecorder speechRecorder;

    [Header("Painting")]
    [SerializeField]
    private GameObject painting;

    private bool hasStarted = false;

    private void Start()
    {
        if (painting != null)
        {
            painting.SetActive(false);
        }
    }

    public void StartExperience()
    {
        Debug.Log("Experience button pressed.");

        // SECOND PRESS = STOP
        if (hasStarted)
        {
            StopExperience();
            return;
        }

        // FIRST PRESS = START
        hasStarted = true;

        if (painting != null)
        {
            painting.SetActive(true);
        }

        if (eyeTrackingRecorder != null)
        {
            eyeTrackingRecorder.StartRecording();
        }
        else
        {
            Debug.LogError("Eye Tracking Recorder is not assigned.");
        }

        if (speechRecorder != null)
        {
            speechRecorder.StartRecording();
        }
        else
        {
            Debug.LogError("Speech Recorder is not assigned.");
        }

        Debug.Log("Experience started.");
    }

    public void StopExperience()
    {
        if (!hasStarted)
        {
            return;
        }

        hasStarted = false;

        if (eyeTrackingRecorder != null)
        {
            eyeTrackingRecorder.StopRecording();
        }

        if (speechRecorder != null)
        {
            speechRecorder.StopRecording();
        }

        Debug.Log("Experience stopped.");
    }
}