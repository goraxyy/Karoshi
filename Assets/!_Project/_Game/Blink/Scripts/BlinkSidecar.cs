using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Karoshi.Blink
{
    // Starts and stops the camera helper that reads your eyes, so blinking works without
    // opening a terminal. On a Mac it's tools/blink/mac/build/BlinkVision (Apple Vision, no
    // downloads — build it once with tools/blink/mac/build.sh); anywhere, the Python helper
    // in tools/blink/.venv if it has been set up. Its output is kept for the F10 test panel.
    public static class BlinkSidecar
    {
        static Process process;
        static readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        static readonly List<string> lines = new List<string>();

        public static bool Running => process != null && !process.HasExited;
        public static string Which { get; private set; } = "";
        public static string Problem { get; private set; } = "";
        public static IReadOnlyList<string> Output { get { Drain(); return lines; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            Application.quitting -= Stop;
            Application.quitting += Stop;
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        // Which camera the Mac helper opens; C in the F10 panel steps through them.
        public static int CameraIndex
        {
            get => PlayerPrefs.GetInt("Karoshi.BlinkCamera", 0);
            set => PlayerPrefs.SetInt("Karoshi.BlinkCamera", Mathf.Max(0, value));
        }

        static string[] cameras;

        // The cameras the Mac helper can see, as "0: FaceTime HD Camera". Asked once, then kept.
        public static IReadOnlyList<string> Cameras
        {
            get
            {
                if (cameras != null) return cameras;
                string exe = MacHelper;
                if (exe == null) return new string[0];   // not built yet; ask again later
                cameras = new string[0];
                try
                {
                    var info = new ProcessStartInfo(exe, "--list-cameras") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (Process p = Process.Start(info))
                    {
                        string text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                        p.WaitForExit(3000);
                        cameras = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 2 && char.IsDigit(l[0]) && l.Contains(":")).ToArray();
                    }
                }
                catch (System.Exception) { }
                return cameras;
            }
        }

        public static string CameraName
        {
            get
            {
                IReadOnlyList<string> all = Cameras;
                if (all.Count == 0) return "";
                string line = all[Mathf.Clamp(CameraIndex, 0, all.Count - 1)];
                return line.Substring(line.IndexOf(':') + 1).Trim();
            }
        }

        // Next camera, and restart the helper on it.
        public static void NextCamera(int port)
        {
            int count = Cameras.Count;
            CameraIndex = count > 0 ? (CameraIndex + 1) % count : 0;
            Stop();
            Start(port);
        }

        static string MacHelper
        {
            get
            {
                if (Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.OSXPlayer) return null;
                foreach (string path in new[] { Path.Combine(ProjectRoot, "tools/blink/mac/build/BlinkVision"), Path.GetFullPath(Path.Combine(Application.dataPath, "../../BlinkVision")) })
                    if (File.Exists(path)) return path;
                return null;
            }
        }

        // Where a helper might be, best first.
        static IEnumerable<(string exe, string args, string name)> Candidates(int port)
        {
            // On a Mac: the helper in the project, or (in a build) copied next to the .app.
            string mac = MacHelper;
            if (mac != null) yield return (mac, $"--port {port} --camera {CameraIndex}", "BlinkVision (Apple Vision)");
            string venv = Path.Combine(ProjectRoot, "tools/blink/.venv");
            string python = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer
                ? Path.Combine(venv, "Scripts/python.exe") : Path.Combine(venv, "bin/python3");
            string script = Path.Combine(ProjectRoot, "tools/blink/blink_server.py");
            string model = Path.Combine(ProjectRoot, "tools/blink/face_landmarker.task");
            string method = File.Exists(model) ? $"--method blendshapes --model \"{model}\"" : "--method ear";
            yield return (python, $"\"{script}\" {method} --port {port}", "MediaPipe (Python)");
        }

        public static bool Start(int port)
        {
            if (Running) return true;
            Problem = "";
            foreach (var (exe, args, name) in Candidates(port))
            {
                if (!File.Exists(exe)) continue;
                try
                {
                    var info = new ProcessStartInfo(exe, args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Path.GetDirectoryName(exe)
                    };
                    process = new Process { StartInfo = info, EnableRaisingEvents = true };
                    process.OutputDataReceived += (_, e) => { if (e.Data != null) incoming.Enqueue(e.Data); };
                    process.ErrorDataReceived += (_, e) => { if (e.Data != null) incoming.Enqueue(e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    Which = name;
                    incoming.Enqueue($"started {name}");
                    return true;
                }
                catch (System.Exception e)
                {
                    Problem = $"couldn't start {name}: {e.Message}";
                    process = null;
                }
            }
            if (string.IsNullOrEmpty(Problem))
                Problem = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer
                    ? "The camera helper isn't built yet. In a terminal, run:  tools/blink/mac/build.sh"
                    : "No camera helper found. Set up tools/blink (see tools/blink/README.md).";
            return false;
        }

        public static void Stop()
        {
            if (process == null) return;
            try { if (!process.HasExited) process.Kill(); } catch { }
            process.Dispose();
            process = null;
        }

        static void Drain()
        {
            while (incoming.TryDequeue(out string line))
            {
                lines.Add(line);
                if (line.Contains("refused") || line.Contains("couldn't") || line.Contains("no camera")) Debug.LogWarning("Blink helper: " + line);
            }
            if (lines.Count > 40) lines.RemoveRange(0, lines.Count - 40);
            if (process != null && process.HasExited && string.IsNullOrEmpty(Problem))
                Problem = $"the camera helper stopped (exit code {process.ExitCode}) — see its last lines below";
        }
    }
}
