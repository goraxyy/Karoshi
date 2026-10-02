using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kehai.Replay
{
    // The batch-mode entry for rendering a shot (ReplayRender has the flags):
    //
    //   Unity -batchmode -projectPath <project> -executeMethod Kehai.Replay.ReplayRenderBatch.Run \
    //         -krec <file.krec> [-moment 1] [-shot chase] … -out <file.mp4> [-render-timeout-min 60]
    //
    // Opens the scene the shift was recorded in and enters play mode; the replay renders and
    // quits. It needs graphics, so no -nographics. tools/marketing/render_shot.sh is the way to
    // call it: that checks the editor isn't open on the project and takes the heavy-job lock.
    [InitializeOnLoad]
    public static class ReplayRenderBatch
    {
        static ReplayRenderBatch()
        {
            if (!Application.isBatchMode || ReplayMode.Arg("-krec") == null) return;
            double minutes = double.TryParse(ReplayMode.Arg("-render-timeout-min"), out double m) && m > 0 ? m : 60.0;
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < minutes * 60.0) return;
                Debug.LogError($"ReplayRender: timed out after {minutes:0} min");
                EditorApplication.Exit(2);
            };
        }

        public static void Run()
        {
            string krec = ReplayMode.Arg("-krec");
            if (string.IsNullOrEmpty(krec) || !File.Exists(krec))
            {
                Debug.LogError("ReplayRender: -krec <file.krec> is needed, and has to exist: " + krec);
                EditorApplication.Exit(1);
                return;
            }
            string scene = ReplayMode.SceneOf(krec);
            if (string.IsNullOrEmpty(scene) || !File.Exists(scene)) scene = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled)?.path;
            if (string.IsNullOrEmpty(scene) || !File.Exists(scene))
            {
                Debug.LogError("ReplayRender: can't find the store's scene to replay in");
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Debug.Log($"ReplayRender: entering play mode in {scene} for {krec}");
            EditorApplication.EnterPlaymode();
        }
    }
}
