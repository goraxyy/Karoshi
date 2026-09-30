using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kehai.Aiko
{
    // Puts Aiko into the store when the scene loads. Entirely from code — the scene file
    // isn't in version control, so nothing about her may depend on it.
    //
    // Command line (for builds and the eval harness):
    //   -noaiko              run the store without her
    //   -aiko-rung <A..F>    choose the ablation rung
    //   -aiko-seed <n>       seed every decision (0 = clock)
    public static class AikoBootstrap
    {
        // Set by the eval harness before a scene load to configure the next Aiko.
        public static AikoConfig Override;
        public static bool Disabled;

        // Every later load of the store gets an Aiko too — the eval harness reloads the scene
        // on each reset, and RuntimeInitializeOnLoadMethod only fires for the first one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Install();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            ReadCommandLine();
            if (Disabled) return;
            if (Object.FindAnyObjectByType<ShiftManager>() == null) return;   // not the store
            if (Object.FindAnyObjectByType<AikoBrain>() != null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (player.GetComponent<PlayerPresence>() == null) player.AddComponent<PlayerPresence>();
                if (player.GetComponent<FootprintTrail>() == null) player.AddComponent<FootprintTrail>();
            }

            foreach (CustomerNPC npc in Object.FindObjectsByType<CustomerNPC>())
                if (npc.GetComponent<CustomerMemory>() == null) npc.gameObject.AddComponent<CustomerMemory>();

            var root = new GameObject(GameNames.Antagonist);
            var brain = root.AddComponent<AikoBrain>();
            if (Override != null) brain.config = Override.Clone();
            ApplyCommandLine(brain.config);
            KeepBelowSprint(brain.config);
            root.AddComponent<ShiftRecorder>();
            root.AddComponent<ClipMarkerRecorder>();
            root.AddComponent<Kehai.Replay.ReplayRecorder>();
            root.AddComponent<AikoDebugOverlay>();
            root.AddComponent<ReviewScreen>();
            root.AddComponent<Kehai.Blink.BlinkTracker>();
            root.AddComponent<Kehai.Blink.BlinkTestPanel>();
        }

        static string rungArg, seedArg;

        static void ReadCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-noaiko") Disabled = true;
                if (args[i] == "-aiko-rung" && i + 1 < args.Length) rungArg = args[i + 1];
                if (args[i] == "-aiko-seed" && i + 1 < args.Length) seedArg = args[i + 1];
            }
        }

        // However fast she gets, a sprinting employee can always pull away from her: every pace
        // stays under the player's sprint speed as the scene actually has it.
        static void KeepBelowSprint(AikoConfig config)
        {
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null) KeepBelowSprint(config, motor.sprintSpeed);
        }

        public static void KeepBelowSprint(AikoConfig config, float sprintSpeed)
        {
            float cap = sprintSpeed * 0.94f;
            config.sneakSpeed = Mathf.Min(config.sneakSpeed, cap);
            config.walkSpeed = Mathf.Min(config.walkSpeed, cap);
            config.hurrySpeed = Mathf.Min(config.hurrySpeed, cap);
            config.runSpeed = Mathf.Min(config.runSpeed, cap);
        }

        static void ApplyCommandLine(AikoConfig config)
        {
            if (!string.IsNullOrEmpty(rungArg) && TryParseRung(rungArg, out AikoRung rung)) config.rung = rung;
            if (!string.IsNullOrEmpty(seedArg) && int.TryParse(seedArg, out int seed)) config.seed = seed;
        }

        // "C", "c", "C_BeliefGrid" and "2" all mean rung C.
        public static bool TryParseRung(string text, out AikoRung rung)
        {
            rung = AikoRung.F_Blink;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (int.TryParse(text, out int index) && index >= 0 && index <= 5) { rung = (AikoRung)index; return true; }
            char letter = char.ToUpperInvariant(text[0]);
            if (letter >= 'A' && letter <= 'F') { rung = (AikoRung)(letter - 'A'); return true; }
            return false;
        }
    }
}
