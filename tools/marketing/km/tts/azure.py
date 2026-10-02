"""Azure Speech: neural voices through the Speech SDK, word timings from WordBoundary events.

Needs AZURE_SPEECH_KEY and AZURE_SPEECH_REGION (.env). The free tier (F0) allows about 0.5M
characters a month and 20 requests a minute; a 429 is retried after a pause.
"""
from __future__ import annotations

import time
from pathlib import Path
from xml.sax.saxutils import escape

from .. import env
from . import LOCALES, Spoken, TtsError, attach_punctuation, speakable
from .wav import duration

TICKS = 10_000_000          # audio offsets are in 100-nanosecond ticks


def ssml(text: str, lang: str, voice: dict) -> str:
    body = escape(speakable(text))
    rate, pitch = voice.get("rate") or "+0%", voice.get("pitch") or "+0%"
    body = f'<prosody rate="{rate}" pitch="{pitch}">{body}</prosody>'
    if voice.get("style"):
        body = f'<mstts:express-as style="{escape(voice["style"])}">{body}</mstts:express-as>'
    return (f'<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" '
            f'xmlns:mstts="https://www.w3.org/2001/mstts" xml:lang="{LOCALES[lang]}">'
            f'<voice name="{escape(voice["azure"])}">{body}</voice></speak>')


class Azure:
    name = "azure"

    def __init__(self) -> None:
        try:
            import azure.cognitiveservices.speech as speechsdk
        except ImportError as e:   # pragma: no cover
            raise TtsError("the Azure Speech SDK isn't installed (uv sync)") from e
        self.sdk = speechsdk
        self.key = env.need("AZURE_SPEECH_KEY", "the Azure voice")
        self.region = env.need("AZURE_SPEECH_REGION", "the Azure voice")

    def speak(self, text: str, lang: str, voice: dict, out: Path) -> Spoken:
        sdk = self.sdk
        out.parent.mkdir(parents=True, exist_ok=True)
        document = ssml(text, lang, voice)
        for attempt in range(4):
            config = sdk.SpeechConfig(subscription=self.key, region=self.region)
            config.set_speech_synthesis_output_format(sdk.SpeechSynthesisOutputFormat.Riff48Khz16BitMonoPcm)
            synth = sdk.SpeechSynthesizer(speech_config=config, audio_config=sdk.audio.AudioOutputConfig(filename=str(out)))
            words: list[dict] = []

            def on_boundary(evt, words=words):
                if evt.boundary_type in (sdk.SpeechSynthesisBoundaryType.Word, sdk.SpeechSynthesisBoundaryType.Punctuation):
                    start = evt.audio_offset / TICKS
                    words.append({"text": evt.text, "start": round(start, 3),
                                  "end": round(start + evt.duration.total_seconds(), 3)})

            synth.synthesis_word_boundary.connect(on_boundary)
            result = synth.speak_ssml_async(document).get()
            del synth           # closes the file
            if result.reason == sdk.ResultReason.SynthesizingAudioCompleted:
                return Spoken(out, duration(out), attach_punctuation(words), self.name, voice["azure"])
            details = result.cancellation_details
            code = getattr(details, "error_code", None)
            if code == sdk.CancellationErrorCode.TooManyRequests and attempt < 3:
                time.sleep(15 * (attempt + 1))
                continue
            if code == sdk.CancellationErrorCode.AuthenticationFailure:
                raise TtsError("Azure refused the key (AZURE_SPEECH_KEY / AZURE_SPEECH_REGION)")
            raise TtsError(f"Azure couldn't speak the line: {details.reason} {code}: {details.error_details}")
        raise TtsError("Azure kept answering 'too many requests'")
