from vosk import Model, KaldiRecognizer
import wave
import json
import os

MODEL_PATH = "vosk-model-small-en-us-0.15"
AUDIO_PATH = "recording.wav"

if not os.path.exists(MODEL_PATH):
    raise Exception("Model folder not found")

if not os.path.exists(AUDIO_PATH):
    raise Exception("recording.wav not found")

model = Model(MODEL_PATH)
wf = wave.open(AUDIO_PATH, "rb")

recognizer = KaldiRecognizer(model, wf.getframerate())

parts = []

while True:
    data = wf.readframes(4000)

    if len(data) == 0:
        break

    if recognizer.AcceptWaveform(data):
        result = json.loads(recognizer.Result())
        text = result.get("text", "")
        if text:
            parts.append(text)

final_result = json.loads(recognizer.FinalResult())
final_text = final_result.get("text", "")

if final_text:
    parts.append(final_text)

print("Transcript:")
print(" ".join(parts))