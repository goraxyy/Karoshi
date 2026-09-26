# Blink sidecar

The webcam half of KAREN's blink channel (see `ideas.md` → *Blink*, and `BlinkTracker.cs`).
A small Python process owns the camera, decides how shut your eyes are, and sends one JSON
packet per frame to the game on `127.0.0.1:5066`. The game does the rest: calibration,
hysteresis, predicting when your eyes will reopen, and what KAREN does with the window.

**Privacy.** Opt-in only (F8 in game shows what is used and asks first). Frames are processed
in memory and thrown away; the sidecar never writes an image, never records, and only ever
sends to localhost. The one tool that saves anything is `record_dataset.py`, which asks you
first and keeps only 24×24 eye crops in a folder you choose.

## Run it

```bash
cd tools/blink
python3 blink_server.py --synthetic            # no camera, no installs: a human-shaped blink trace
pip install -r requirements.txt
python3 blink_server.py --method ear --preview # MediaPipe face mesh + eye aspect ratio
```

In game: **F8** to consent (the webcam path stays off until you do), **F9** to calibrate
(10 s, eyes open then a few blinks), **B** is the keyboard fallback at any time.

| method | needs | notes |
|---|---|---|
| `--synthetic` | nothing | for testing KAREN's reactions; same generator the eval bots use |
| `--method ear` | opencv, mediapipe | robust, adapts to your face automatically |
| `--method blendshapes --model face_landmarker.task` | + the MediaPipe FaceLandmarker model file | most accurate off the shelf; download the `.task` file from the MediaPipe Face Landmarker docs |
| `--method cnn --onnx eye_cnn.onnx` | + onnxruntime, your model | the "own training path" below |

## Own training path (distillation → tiny CNN → ONNX)

```bash
python3 record_dataset.py --out eye_dataset --minutes 3   # asks for consent; MediaPipe labels each crop
python3 train_eye_cnn.py --data eye_dataset --out eye_cnn.onnx
python3 blink_server.py --method cnn --onnx eye_cnn.onnx
```

The model contract — input `eye` `[n,1,24,24]` grayscale 0..1 with rows top-down, output
`closed_logit` `[n,1]` — is the same one `SentisBlinkSource.cs` expects, so the same file can
run in-engine: add `com.unity.ai.inference`, import the `.onnx`, and define `KAREN_SENTIS`.

Datasets and models are personal and stay out of git (`.gitignore` here).

## Packet

```json
{"seq": 1234, "closed": 0.93, "conf": 0.88, "src": "ear",
 "capture": 1727000000.1234, "sent": 1727000000.1391, "fps": 30.0}
```

`capture` → `sent` is the pipeline's own latency; the game subtracts it to date the true start
of each blink, and KAREN acts in whatever is left of the ~300 ms.
