using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SpeechRecorder : MonoBehaviour
{
    public string serverUrl = "http://127.0.0.1:5000/transcribe";
    public int sampleRate = 16000;
    public int maxRecordSeconds = 30;

    private AudioClip recordedClip;
    private string micDevice;

    public void StartRecording()
    {
        micDevice = null; // default microphone
        recordedClip = Microphone.Start(micDevice, false, maxRecordSeconds, sampleRate);
        Debug.Log("Recording started...");
    }

    public void StopRecording()
    {
        int position = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);

        if (recordedClip == null || position <= 0)
        {
            Debug.LogError("No audio recorded.");
            return;
        }

        AudioClip trimmedClip = TrimClip(recordedClip, position);
        byte[] wavData = ConvertAudioClipToWav(trimmedClip);

        string audioPath = Path.Combine(Application.persistentDataPath, "recording.wav");
        File.WriteAllBytes(audioPath, wavData);

        Debug.Log("Saved audio to: " + audioPath);

        StartCoroutine(SendAudioToServer(wavData));
    }

    private IEnumerator SendAudioToServer(byte[] wavData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", wavData, "recording.wav", "audio/wav");

        using UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Transcription failed: " + request.error);
        }
        else
        {
            string transcript = request.downloadHandler.text;
            Debug.Log("Transcript: " + transcript);

            SaveTranscriptToCSV(transcript);
        }
    }

    private void SaveTranscriptToCSV(string transcript)
    {
        string csvPath = Path.Combine(Application.persistentDataPath, "speech_transcripts.csv");

        if (!File.Exists(csvPath))
        {
            File.WriteAllText(csvPath, "Time,Transcript\n");
        }

        string cleanTranscript = transcript.Replace("\"", "\"\"");
        string line = $"\"{DateTime.Now}\",\"{cleanTranscript}\"\n";

        File.AppendAllText(csvPath, line);

        Debug.Log("Saved transcript to: " + csvPath);
    }

    private AudioClip TrimClip(AudioClip clip, int samples)
    {
        float[] data = new float[samples * clip.channels];
        clip.GetData(data, 0);

        AudioClip trimmedClip = AudioClip.Create(
            "TrimmedRecording",
            samples,
            clip.channels,
            clip.frequency,
            false
        );

        trimmedClip.SetData(data, 0);
        return trimmedClip;
    }

    private byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * short.MaxValue);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        using MemoryStream stream = new MemoryStream();

        int hz = clip.frequency;
        int channels = clip.channels;
        int sampleCount = samples.Length;

        stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(36 + bytesData.Length), 0, 4);
        stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);
        stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4);
        stream.Write(BitConverter.GetBytes((short)1), 0, 2);
        stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(hz), 0, 4);
        stream.Write(BitConverter.GetBytes(hz * channels * 2), 0, 4);
        stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2);
        stream.Write(BitConverter.GetBytes((short)16), 0, 2);
        stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
        stream.Write(bytesData, 0, bytesData.Length);

        return stream.ToArray();
    }
}