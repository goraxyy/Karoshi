using UnityEngine;
using UnityEngine.SceneManagement;

namespace Karoshi.Karen
{
    // Puts Karen into the store when the scene loads. Entirely from code — the scene file
    // isn't in version control, so nothing about her may depend on it.
    //
    // Command line (for builds and the eval harness):
    //   -nokaren              run the store without her
    //   -karen-rung <A..F>    choose the ablation rung
    //   -karen-seed <n>       seed every decision (0 = clock)
    public static class KarenBootstrap
    {
        // Set by the eval harness before a scene load to configure the next Karen.
        public static KarenConfig Override;
        public static bool Disabled;

        // Every later load of the store gets a Karen too — the eval harness reloads the scene
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
            if (Object.FindAnyObjectByType<KarenBrain>() != null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (player.GetComponent<PlayerPresence>() == null) player.AddComponent<PlayerPresence>();
                if (player.GetComponent<FootprintTrail>() == null) player.AddComponent<FootprintTrail>();
            }

            foreach (CustomerNPC npc in Object.FindObjectsByType<CustomerNPC>())
                if (npc.GetComponent<CustomerMemory>() == null) npc.gameObject.AddComponent<CustomerMemory>();

            var root = new GameObject("Karen");
            var brain = root.AddComponent<KarenBrain>();
            if (Override != null) brain.config = Override.Clone();
            ApplyCommandLine(brain.config);
            root.AddComponent<ShiftRecorder>();
            root.AddComponent<KarenDebugOverlay>();
            root.AddComponent<ReviewScreen>();
            root.AddComponent<Karoshi.Blink.BlinkTracker>();
            root.AddComponent<Karoshi.Blink.BlinkTestPanel>();
        }

        static string rungArg, seedArg;

        static void ReadCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-nokaren") Disabled = true;
                if (args[i] == "-karen-rung" && i + 1 < args.Length) rungArg = args[i + 1];
                if (args[i] == "-karen-seed" && i + 1 < args.Length) seedArg = args[i + 1];
            }
        }

        static void ApplyCommandLine(KarenConfig config)
        {
            if (!string.IsNullOrEmpty(rungArg) && TryParseRung(rungArg, out KarenRung rung)) config.rung = rung;
            if (!string.IsNullOrEmpty(seedArg) && int.TryParse(seedArg, out int seed)) config.seed = seed;
        }

        // "C", "c", "C_BeliefGrid" and "2" all mean rung C.
        public static bool TryParseRung(string text, out KarenRung rung)
        {
            rung = KarenRung.F_Blink;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (int.TryParse(text, out int index) && index >= 0 && index <= 5) { rung = (KarenRung)index; return true; }
            char letter = char.ToUpperInvariant(text[0]);
            if (letter >= 'A' && letter <= 'F') { rung = (KarenRung)(letter - 'A'); return true; }
            return false;
        }
    }
}
