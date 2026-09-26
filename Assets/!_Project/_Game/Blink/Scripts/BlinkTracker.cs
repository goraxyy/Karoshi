using System.Collections.Generic;
using Karoshi.Karen;
using UnityEngine;

namespace Karoshi.Blink
{
    // Turns whichever blink source is live into one clean signal (ideas.md "Architecture"):
    //
    //   IBlinkSource → BlinkTracker (calibration, smoothing, confidence)
    //                       ├─→ Eyelids.Closed01            the player's own eyes, mirrored
    //                       └─→ OnBlinkStart / OnEyesClosedFor(t) → KAREN
    //
    // Non-negotiables, all enforced here:
    //   • Blink is a modifier, never a requirement — with no camera the keyboard stands in,
    //     and with neither the game is simply played with the eyes open.
    //   • Opt-in, local-only, never recorded, and said plainly on screen before the camera
    //     path is ever touched.
    //   • Exploit the window, don't chase the latency: a blink lasts ~300 ms and is learnt of
    //     ~100 ms in, so the tracker predicts when the eyes will reopen and KAREN acts inside
    //     what is left.
    public sealed class BlinkTracker : MonoBehaviour
    {
        public static BlinkTracker Instance { get; private set; }

        [Header("Sources")]
        public KeyCode keyboardKey = KeyCode.B;
        public int udpPort = 5066;
        [Tooltip("Blinks per minute for the synthetic source the simulated players use.")]
        public float syntheticRate = 17f;

        [Header("Keys")]
        public KeyCode consentKey = KeyCode.F8;
        public KeyCode calibrateKey = KeyCode.F9;

        [Header("Signal")]
        [Range(0f, 1f)] public float closeThreshold = 0.6f;
        [Range(0f, 1f)] public float openThreshold = 0.35f;
        [Tooltip("Mirror the player's real eyes onto the on-screen eyelids.")]
        public bool mirrorToEyelids = true;

        // ---- outputs ---------------------------------------------------------------------
        public float Closed01 { get; private set; }
        public bool EyesClosed { get; private set; }
        public float Confidence { get; private set; }
        public bool Live => source != null && source.IsLive;
        public string SourceName => source != null ? source.Name : "none";
        public float LatencyMs => source != null ? source.MeasuredLatencyMs : 0f;
        public float ExpectedBlinkSeconds { get; private set; } = 0.3f;
        public int Blinks { get; private set; }

        // Seconds until the eyes are expected to open again, from the shape of a blink and
        // how long ago this one really began (detection time minus measured latency).
        public float PredictedReopenIn => EyesClosed
            ? Mathf.Max(0f, ExpectedBlinkSeconds - (float)(BlinkClock.Now - blinkStartedAt))
            : 0f;

        public event System.Action<double> OnBlinkStart;
        public event System.Action<float> OnBlinkEnd;
        public event System.Action<float> OnEyesClosedFor;   // fires at 0.5 s, 1 s and 2 s

        IBlinkSource source;
        IBlinkSource keyboard;
        UdpBlinkSource webcam;
        double blinkStartedAt;
        readonly float[] closedForMarks = { 0.5f, 1f, 2f };
        int nextMark;
        Eyelids lids;

        // Calibration (ideas.md "Per-player calibration"): 10 seconds, look at the camera,
        // blink a few times. The player's own open and closed levels normalise everything.
        float openLevel = 0f, closedLevel = 1f;
        bool calibrating;
        float calibrationEnds;
        readonly List<float> calibrationSamples = new List<float>();

        public const string ConsentKey = "karen.blink.consent";
        public static bool Consented => PlayerPrefs.GetInt(ConsentKey, 0) == 1;
        bool askingConsent;

        void Awake()
        {
            Instance = this;
            keyboard = new KeyboardBlinkSource(keyboardKey);
            source = keyboard;
            lids = FindAnyObjectByType<Eyelids>();
            if (Consented) StartWebcam();
        }

        void OnDestroy()
        {
            webcam?.Dispose();
            if (Instance == this) Instance = null;
        }

        // For tests and the simulated players.
        public void UseSource(IBlinkSource replacement)
        {
            source = replacement ?? keyboard;
        }

        public void UseSynthetic(int seed) => UseSource(ReplayBlinkSource.Synthetic(syntheticRate, seed));

        void StartWebcam()
        {
            if (webcam != null) return;
            try { webcam = new UdpBlinkSource(udpPort); }
            catch (System.Exception e) { Debug.LogWarning($"Blink: couldn't listen on udp {udpPort}: {e.Message}"); }
        }

        void Update()
        {
            HandleKeys();

            // Prefer the camera whenever the sidecar is actually sending; fall back to the
            // keyboard the moment it stops. Never a requirement.
            if (webcam != null && webcam.IsLive && !(source is ReplayBlinkSource)) source = webcam;
            else if (source == webcam && (webcam == null || !webcam.IsLive)) source = keyboard;

            if (source.TryRead(out BlinkSample s))
            {
                float normalised = Mathf.InverseLerp(openLevel, closedLevel, s.Closed);
                if (calibrating) calibrationSamples.Add(s.Closed);

                // Light smoothing on the way down, none on the way up: a closing eye should
                // register the instant it's seen.
                Closed01 = normalised > Closed01 ? normalised : Mathf.Lerp(Closed01, normalised, 0.6f);
                Confidence = s.Confidence;
                Step(s);
            }

            if (calibrating && Time.unscaledTime > calibrationEnds) FinishCalibration();

            if (mirrorToEyelids && lids != null && Live && Confidence > 0.3f && !(source is ReplayBlinkSource))
                lids.SetClosed(Closed01);

            PushToKaren();
        }

        void Step(BlinkSample s)
        {
            // Hysteresis: shut above one threshold, open again below a lower one.
            if (!EyesClosed && Closed01 >= closeThreshold)
            {
                EyesClosed = true;
                blinkStartedAt = s.Captured;
                nextMark = 0;
                Blinks++;
                OnBlinkStart?.Invoke(blinkStartedAt);
                KarenBrain.Instance?.OnBlinkStarted();
            }
            else if (EyesClosed && Closed01 <= openThreshold)
            {
                EyesClosed = false;
                float length = (float)(s.Captured - blinkStartedAt);
                // Learn this player's blink length, for predicting the next reopening.
                if (length > 0.08f && length < 0.8f) ExpectedBlinkSeconds = Mathf.Lerp(ExpectedBlinkSeconds, length, 0.1f);
                OnBlinkEnd?.Invoke(length);
            }

            if (EyesClosed && nextMark < closedForMarks.Length)
            {
                float held = (float)(BlinkClock.Now - blinkStartedAt);
                if (held >= closedForMarks[nextMark])
                {
                    OnEyesClosedFor?.Invoke(closedForMarks[nextMark]);
                    nextMark++;
                }
            }
        }

        void PushToKaren()
        {
            KarenBrain brain = KarenBrain.Instance;
            if (brain == null) return;
            // The keyboard counts as a live channel — it's how the mechanic is played without a
            // camera — but only a real (or recorded) pair of eyes says anything about stress.
            brain.BlinkLive = Live;
            brain.BlinkPhysiological = Live && !(source is KeyboardBlinkSource);
            brain.EyesClosed = EyesClosed;
            brain.PredictedReopenIn = PredictedReopenIn;
        }

        // ---- consent and calibration ----------------------------------------------------------

        void HandleKeys()
        {
            if (Input.GetKeyDown(consentKey))
            {
                if (Consented)
                {
                    PlayerPrefs.SetInt(ConsentKey, 0);
                    webcam?.Dispose();
                    webcam = null;
                    source = keyboard;
                    KarenScreen.Ensure().Subtitle("Webcam blink tracking off.", 3f);
                }
                else askingConsent = true;
            }

            if (askingConsent)
            {
                if (Input.GetKeyDown(KeyCode.Y))
                {
                    askingConsent = false;
                    PlayerPrefs.SetInt(ConsentKey, 1);
                    StartWebcam();
                    KarenScreen.Ensure().Subtitle("Webcam blink tracking on. Start tools/blink/blink_server.py, then press F9 to calibrate.", 6f);
                }
                else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape))
                {
                    askingConsent = false;
                }
            }

            if (Input.GetKeyDown(calibrateKey) && !calibrating) BeginCalibration();
        }

        public void BeginCalibration()
        {
            calibrating = true;
            calibrationEnds = Time.unscaledTime + 10f;
            calibrationSamples.Clear();
            openLevel = 0f;
            closedLevel = 1f;
            KarenScreen.Ensure().Subtitle("Calibrating: look at the camera, and blink normally a few times.", 10f);
        }

        void FinishCalibration()
        {
            calibrating = false;
            if (calibrationSamples.Count < 20)
            {
                KarenScreen.Ensure().Subtitle("Calibration needs a live blink signal — nothing was received.", 4f);
                return;
            }
            calibrationSamples.Sort();
            // Open is what the eyes do most of the time; closed is the top of the blinks.
            openLevel = calibrationSamples[calibrationSamples.Count / 2];
            closedLevel = calibrationSamples[Mathf.Min(calibrationSamples.Count - 1, (int)(calibrationSamples.Count * 0.98f))];
            if (closedLevel - openLevel < 0.15f) closedLevel = Mathf.Min(1f, openLevel + 0.3f);
            KarenScreen.Ensure().Subtitle($"Calibrated: open {openLevel:0.00}, closed {closedLevel:0.00}.", 4f);
        }

        void OnGUI()
        {
            if (!askingConsent) return;
            var style = new GUIStyle(GUI.skin.box) { richText = true, wordWrap = true, fontSize = 15, alignment = TextAnchor.UpperLeft, padding = new RectOffset(20, 20, 16, 16) };
            float w = Mathf.Min(620f, Screen.width - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.25f, w, 260f);
            GUI.color = new Color(0f, 0f, 0f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect,
                "<b>Use your webcam to track blinks?</b>\n\n" +
                "KAREN can react when your real eyes close. The camera is read by a small program on this " +
                "computer (tools/blink/blink_server.py) that sends only one number — how closed your eyes are — to the game.\n\n" +
                "• <b>Local only.</b> Nothing leaves this machine.\n" +
                "• <b>Never recorded.</b> No frames are saved or stored.\n" +
                "• <b>Optional.</b> Everything works without it; you can switch it off with F8.\n\n" +
                "[Y] enable      [N] not now", style);
        }
    }
}
