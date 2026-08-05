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

    private bool hasStarted;

    private void Start()
    {
        // Hide the painting when the scene begins.
        if (painting != null)
        {
            painting.SetActive(false);
        }
    }

    public void StartExperience()
    {
        Debug.Log("StartExperience called.");

        if (hasStarted)
        {
            return;
        }

        hasStarted = true;

        // Show the painting first.
        if (painting != null)
        {
            painting.SetActive(true);
        }
        else
        {
            Debug.LogError("Painting is not assigned.");
        }

        // Start eye-tracking recording.
        if (eyeTrackingRecorder != null)
        {
            eyeTrackingRecorder.enabled = true;
            eyeTrackingRecorder.StartRecording();
        }
        else
        {
            Debug.LogError("Eye Tracking Recorder is not assigned.");
        }

        // Start microphone recording.
        if (speechRecorder != null)
        {
            speechRecorder.enabled = true;
            speechRecorder.StartRecording();
        }
        else
        {
            Debug.LogError("Speech Recorder is not assigned.");
        }
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