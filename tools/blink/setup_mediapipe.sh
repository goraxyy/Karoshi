#!/bin/sh
# Sets up the MediaPipe camera helper, which reads blinks more accurately than the Apple
# Vision one: a Python environment in tools/blink/.venv with OpenCV and MediaPipe, plus
# MediaPipe's face landmarker model (whose blink scores the helper uses).
#
# It downloads about 100 MB from PyPI and Google's model storage. Nothing else changes on
# your computer, and deleting tools/blink/.venv and face_landmarker.task undoes it.
set -e
cd "$(dirname "$0")"

python3 -m venv .venv
.venv/bin/python3 -m pip install --upgrade pip
.venv/bin/python3 -m pip install "opencv-python>=4.8" "mediapipe>=0.10" "numpy>=1.24"
curl -L --fail -o face_landmarker.task \
  https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task

echo
echo "Done. In the game, press F10: the helper line should say MediaPipe (M switches back to"
echo "Apple Vision). Calibrate again with F9; each helper keeps its own calibration."
