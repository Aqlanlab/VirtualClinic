from flask import Flask, request
from vosk import Model, KaldiRecognizer
import wave
import json
import os

app = Flask(__name__)

MODEL_PATH = "vosk-model-small-en-us-0.15"

if not os.path.exists(MODEL_PATH):
    raise Exception(f"Vosk model folder not found: {MODEL_PATH}")

model = Model(MODEL_PATH)


@app.route("/transcribe", methods=["POST"])
def transcribe():
    if "audio" not in request.files:
        return "No audio file received", 400

    audio_file = request.files["audio"]
    audio_path = "recording.wav"
    audio_file.save(audio_path)

    try:
        wf = wave.open(audio_path, "rb")
    except wave.Error:
        return "Could not read WAV file", 400

    if wf.getnchannels() != 1:
        return "Audio must be mono, not stereo", 400

    if wf.getsampwidth() != 2:
        return "Audio must be 16-bit WAV", 400

    recognizer = KaldiRecognizer(model, wf.getframerate())

    transcript_parts = []

    while True:
        data = wf.readframes(4000)

        if len(data) == 0:
            break

        if recognizer.AcceptWaveform(data):
            result = json.loads(recognizer.Result())
            text = result.get("text", "")
            if text:
                transcript_parts.append(text)

    final_result = json.loads(recognizer.FinalResult())
    final_text = final_result.get("text", "")

    if final_text:
        transcript_parts.append(final_text)

    transcript = " ".join(transcript_parts).strip()

    return transcript


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000)