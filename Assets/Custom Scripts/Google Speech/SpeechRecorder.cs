using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SpeechRecorder : MonoBehaviour
{
    [Header("Server")]
    public string serverUrl = "http://127.0.0.1:5000/transcribe";

    [Header("Recording Settings")]
    public int sampleRate = 16000;
    public int maxRecordSeconds = 30;

    private AudioClip recordedClip;
    private string micDevice;

    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone found.");
            return;
        }

        micDevice = Microphone.devices[0];

        recordedClip = Microphone.Start(
            micDevice,
            false,
            maxRecordSeconds,
            sampleRate
        );

        Debug.Log("Recording started using mic: " + micDevice);
    }

    public void StopRecording()
    {
        if (recordedClip == null)
        {
            Debug.LogError("No recording found. Did you press StartRecording first?");
            return;
        }

        int position = Microphone.GetPosition(micDevice);

        Microphone.End(micDevice);

        if (position <= 0)
        {
            Debug.LogError("No audio was recorded.");
            recordedClip = null;
            return;
        }

        AudioClip trimmedClip = TrimClip(recordedClip, position);

        byte[] wavData = ConvertAudioClipToWav(trimmedClip);

        // Create recordings folder
        string recordingsFolder =
            Path.Combine(Application.persistentDataPath, "recordings");

        if (!Directory.Exists(recordingsFolder))
        {
            Directory.CreateDirectory(recordingsFolder);
        }

        // Milliseconds make every recording filename unique
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

        string audioFileName =
            "recording_" + timestamp + ".wav";

        string audioPath =
            Path.Combine(recordingsFolder, audioFileName);

        File.WriteAllBytes(audioPath, wavData);

        Debug.Log("Saved WAV file to: " + audioPath);

        // Send timestamp with the recording
        StartCoroutine(
            SendAudioToServer(
                wavData,
                audioFileName,
                timestamp
            )
        );

        // Reset for next recording
        recordedClip = null;
    }

    private IEnumerator SendAudioToServer(
        byte[] wavData,
        string audioFileName,
        string timestamp)
    {
        WWWForm form = new WWWForm();

        form.AddBinaryData(
            "audio",
            wavData,
            audioFileName,
            "audio/wav"
        );

        using (UnityWebRequest request =
               UnityWebRequest.Post(serverUrl, form))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "Transcription failed: " + request.error
                );

                Debug.LogError(
                    "Server response: " +
                    request.downloadHandler.text
                );
            }
            else
            {
                string transcript =
                    request.downloadHandler.text;

                Debug.Log("Transcript: " + transcript);

                SaveTranscript(
                    transcript,
                    audioFileName,
                    timestamp
                );
            }
        }
    }

    private void SaveTranscript(
        string transcript,
        string audioFileName,
        string timestamp)
    {
        string transcriptsFolder =
            Path.Combine(
                Application.persistentDataPath,
                "transcripts"
            );

        if (!Directory.Exists(transcriptsFolder))
        {
            Directory.CreateDirectory(transcriptsFolder);
        }

        // Unique transcript for this recording
        string transcriptFileName =
            "transcript_" + timestamp + ".csv";

        string transcriptPath =
            Path.Combine(
                transcriptsFolder,
                transcriptFileName
            );

        try
        {
            string cleanTranscript =
                transcript.Replace("\"", "\"\"");

            string csv =
                "Time,AudioFile,Transcript\n" +
                $"\"{DateTime.Now}\",\"{audioFileName}\",\"{cleanTranscript}\"\n";

            File.WriteAllText(transcriptPath, csv);

            Debug.Log(
                "Saved transcript to: " +
                transcriptPath
            );
        }
        catch (IOException e)
        {
            Debug.LogError(
                "Could not save transcript."
            );

            Debug.LogError(e.Message);
        }
    }

    private AudioClip TrimClip(
        AudioClip clip,
        int samples)
    {
        float[] data =
            new float[samples * clip.channels];

        clip.GetData(data, 0);

        AudioClip trimmedClip =
            AudioClip.Create(
                "TrimmedRecording",
                samples,
                clip.channels,
                clip.frequency,
                false
            );

        trimmedClip.SetData(data, 0);

        return trimmedClip;
    }

    private byte[] ConvertAudioClipToWav(
        AudioClip clip)
    {
        float[] samples =
            new float[
                clip.samples * clip.channels
            ];

        clip.GetData(samples, 0);

        float[] monoSamples;

        if (clip.channels == 1)
        {
            monoSamples = samples;
        }
        else
        {
            int monoLength =
                samples.Length / clip.channels;

            monoSamples =
                new float[monoLength];

            for (int i = 0; i < monoLength; i++)
            {
                float sum = 0f;

                for (
                    int channel = 0;
                    channel < clip.channels;
                    channel++)
                {
                    sum += samples[
                        i * clip.channels + channel
                    ];
                }

                monoSamples[i] =
                    sum / clip.channels;
            }
        }

        byte[] bytesData =
            new byte[monoSamples.Length * 2];

        for (int i = 0; i < monoSamples.Length; i++)
        {
            short intSample =
                (short)(
                    Mathf.Clamp(
                        monoSamples[i],
                        -1f,
                        1f
                    )
                    * short.MaxValue
                );

            byte[] byteArr =
                BitConverter.GetBytes(intSample);

            byteArr.CopyTo(
                bytesData,
                i * 2
            );
        }

        using (MemoryStream stream =
               new MemoryStream())
        {
            int hz = clip.frequency;
            int channels = 1;
            int byteRate =
                hz * channels * 2;

            stream.Write(
                Encoding.ASCII.GetBytes("RIFF"),
                0,
                4
            );

            stream.Write(
                BitConverter.GetBytes(
                    36 + bytesData.Length
                ),
                0,
                4
            );

            stream.Write(
                Encoding.ASCII.GetBytes("WAVE"),
                0,
                4
            );

            stream.Write(
                Encoding.ASCII.GetBytes("fmt "),
                0,
                4
            );

            stream.Write(
                BitConverter.GetBytes(16),
                0,
                4
            );

            stream.Write(
                BitConverter.GetBytes((short)1),
                0,
                2
            );

            stream.Write(
                BitConverter.GetBytes(
                    (short)channels
                ),
                0,
                2
            );

            stream.Write(
                BitConverter.GetBytes(hz),
                0,
                4
            );

            stream.Write(
                BitConverter.GetBytes(byteRate),
                0,
                4
            );

            stream.Write(
                BitConverter.GetBytes(
                    (short)(channels * 2)
                ),
                0,
                2
            );

            stream.Write(
                BitConverter.GetBytes((short)16),
                0,
                2
            );

            stream.Write(
                Encoding.ASCII.GetBytes("data"),
                0,
                4
            );

            stream.Write(
                BitConverter.GetBytes(
                    bytesData.Length
                ),
                0,
                4
            );

            stream.Write(
                bytesData,
                0,
                bytesData.Length
            );

            return stream.ToArray();
        }
    }
}