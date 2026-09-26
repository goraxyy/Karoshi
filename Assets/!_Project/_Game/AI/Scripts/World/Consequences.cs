using System.Collections;
using UnityEngine;

namespace Karoshi.Karen
{
    // What happens to *you*. Being caught is not death — it's a written warning, a lecture
    // and lost shift time (karen.md §8.6). Karoshi's fail state is the clock, not the claw.
    // And the three ways a career ends (§10.3).
    public static class Consequences
    {
        public static bool LectureRunning { get; private set; }

        static readonly string[] LectureLines =
        {
            "This is a formal conversation about your performance.",
            "Running on the shop floor is a hazard to our customers.",
            "Your wellbeing is a tracked metric. It has been tracked.",
            "I'll be adding this to your file. The file is very long.",
            "We are a family here. Families have rules.",
            "You may return to work. The shift has been extended to accommodate this conversation."
        };

        public static IEnumerator Lecture(KarenBrain brain, int warning, float seconds, float overtime)
        {
            LectureRunning = true;
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null) motor.movementLocked = true;

            KarenScreen screen = KarenScreen.Ensure();
            screen.Banner($"<color=#FF6F61>WRITTEN WARNING #{warning}</color>\n<size=60%>{brain.Ledger.PlayerName}</size>", 4f);
            AddOvertime(overtime);

            float per = seconds / LectureLines.Length;
            foreach (string line in LectureLines)
            {
                if (brain.World != null && !brain.World.Pa.Jammed) brain.World.Pa.Announce(line);
                else screen.Subtitle("<color=#FF6F61>Karen</color>\n" + line, per);
                yield return new WaitForSeconds(per);
            }

            if (motor != null) motor.movementLocked = false;
            LectureRunning = false;
        }

        public static void AddOvertime(float seconds)
        {
            ShiftManager shift = Object.FindAnyObjectByType<ShiftManager>();
            if (shift != null) shift.AddOvertime(seconds);
        }

        // ---- the endings ---------------------------------------------------------------

        // You burn out. The last thing that happens is that she makes you a coffee.
        public static IEnumerator KaroshiEnding(KarenBrain brain)
        {
            brain.Ledger.Data.endingReached = true;
            brain.Ledger.Save();
            Eyelids lids = Object.FindAnyObjectByType<Eyelids>();
            if (lids != null) lids.Close(6f);
            yield return new WaitForSecondsRealtime(6.5f);
            KarenScreen screen = KarenScreen.Ensure();
            screen.SetFade(1f);
            screen.Banner("<size=160%>過労死</size>\nKAROSHI\n\n<size=50%>" + brain.Review.EndingText("burnout") + "</size>", 9999f);
        }

        public static void QuitEnding(KarenBrain brain, string review)
        {
            brain.Ledger.Data.endingReached = true;
            brain.Ledger.Save();
            KarenScreen screen = KarenScreen.Ensure();
            screen.SetFade(0.92f);
            string kind = brain.Review.BrokeHer ? "broke" : "quit";
            screen.Banner("<size=120%>NOTICE ACCEPTED</size>\n\n<size=50%>" + brain.Review.EndingText(kind) + "</size>", 9999f);
        }
    }
}
