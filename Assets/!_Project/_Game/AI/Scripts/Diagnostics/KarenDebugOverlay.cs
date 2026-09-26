using System.Linq;
using System.Text;
using Karoshi.Store;
using UnityEngine;

namespace Karoshi.Karen
{
    // Watch her think (karen.md §6.6 use 1).
    //
    //   F1  the live overlay: belief as a heatmap over the floor plan, her position, the
    //       peak, the top goal scores, the plan and the step she is on, the Director's
    //       panic against its setpoint, and the tail of the thought log.
    //   F2  the replay scrubber (ideas.md §4): drag through every decision of the shift and
    //       see the belief map, her position and the truth as they were at that moment.
    //
    // The magenta dot is where the employee really is. It is drawn from the Director's
    // view for debugging and is never available to anything that plans.
    public sealed class KarenDebugOverlay : MonoBehaviour
    {
        public KeyCode overlayKey = KeyCode.F1;
        public KeyCode scrubberKey = KeyCode.F2;
        public bool showOverlay;
        public bool showScrubber;

        KarenBrain brain;
        Texture2D heat;
        Color32[] pixels;
        Texture2D graph;
        GUIStyle text;
        float scrub = 1f;
        float nextPaint;

        void Awake() => brain = GetComponent<KarenBrain>();

        void Update()
        {
            if (Input.GetKeyDown(overlayKey)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(scrubberKey))
            {
                showScrubber = !showScrubber;
                Cursor.lockState = showScrubber ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = showScrubber;
            }
        }

        void EnsureTextures(StoreMap map)
        {
            if (heat != null && heat.width == map.Columns && heat.height == map.Rows) return;
            heat = new Texture2D(map.Columns, map.Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            pixels = new Color32[map.Columns * map.Rows];
            graph = new Texture2D(240, 60, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        }

        // Paint belief per cell (or per region, from a snapshot) on a log scale.
        void Paint(StoreMap map, System.Func<int, float> massOfCell)
        {
            var empty = new Color32(18, 18, 22, 230);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            float max = 1e-6f;
            for (int c = 0; c < map.CellCount; c++) max = Mathf.Max(max, massOfCell(c));

            for (int c = 0; c < map.CellCount; c++)
            {
                map.CellGrid(c, out int col, out int row);
                float m = massOfCell(c);
                float t = m <= 0f ? 0f : Mathf.Clamp01(1f + Mathf.Log10(m / max) / 4f);
                Color colour = t <= 0f ? (Color)empty : Color.Lerp(new Color(0.1f, 0.1f, 0.25f), new Color(1f, 0.85f, 0.2f), t);
                if (map.Regions[map.CellRegion[c]].IsDoor) colour = Color.Lerp(colour, Color.white, 0.25f);
                if (brain.ClosedRegions.Contains(map.CellRegion[c])) colour = Color.Lerp(colour, Color.red, 0.5f);
                pixels[row * map.Columns + col] = colour;
            }
            heat.SetPixels32(pixels);
            heat.Apply();
        }

        void PaintGraph()
        {
            var clear = new Color32(0, 0, 0, 160);
            var px = new Color32[graph.width * graph.height];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            var history = brain.Director.History;
            int n = history.Count;
            for (int x = 0; x < graph.width; x++)
            {
                int i = n - graph.width + x;
                if (i < 0) continue;
                int y = Mathf.Clamp(Mathf.RoundToInt(history[i].panic * (graph.height - 1)), 0, graph.height - 1);
                px[y * graph.width + x] = new Color32(255, 90, 80, 255);
            }
            int sp = Mathf.Clamp(Mathf.RoundToInt(brain.Director.Setpoint * (graph.height - 1)), 0, graph.height - 1);
            for (int x = 0; x < graph.width; x += 2) px[sp * graph.width + x] = new Color32(120, 200, 255, 255);
            graph.SetPixels32(px);
            graph.Apply();
        }

        void OnGUI()
        {
            if (brain == null || brain.Map == null || brain.Belief == null) return;
            if (!showOverlay && !showScrubber) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, wordWrap = true };
                text.normal.textColor = Color.white;
            }

            StoreMap map = brain.Map;
            EnsureTextures(map);

            if (showScrubber) { DrawScrubber(map); return; }

            if (Time.unscaledTime > nextPaint)
            {
                nextPaint = Time.unscaledTime + 0.25f;
                Paint(map, c => brain.Belief[c]);
                PaintGraph();
            }

            float scale = Mathf.Min(4f, (Screen.height * 0.55f) / map.Rows);
            var mapRect = new Rect(10, 10, map.Columns * scale, map.Rows * scale);
            DrawMap(map, mapRect, brain.Body.Position, brain.Belief.PeakPosition, KarenDirector.TruePlayerPosition);

            var sb = new StringBuilder();
            KarenDirector d = brain.Director;
            sb.AppendLine($"<b>K.A.R.E.N.</b>  rung {brain.config.rung}  shift {brain.Stats.Shift}  t={brain.ShiftTime:0}s");
            sb.AppendLine($"phase <b>{d.PhaseName}</b>  panic {d.Panic:0.00} → setpoint {d.Setpoint:0.00}  pressure {d.Pressure:+0.00;-0.00}  tension {d.Tension:0.00}");
            sb.AppendLine($"belief peak <b>{map.RegionName(brain.Belief.PeakRegion)}</b> p={brain.Belief.Confidence:0.00} H={brain.Belief.Entropy:0.00} stale={Mathf.Min(999f, brain.Belief.Staleness):0}s");
            sb.AppendLine($"sight {brain.Body.Sight.Band} ({brain.Body.Sight.Awareness:0.00})  energy belief {brain.Energy.Energy:0.00}");
            if (brain.CurrentPlan != null)
                sb.AppendLine($"plan <b>{brain.CurrentPlan.Name}</b> step {brain.CurrentPlan.Plan.Index + 1}/{brain.CurrentPlan.Plan.Count}: {brain.CurrentPlan.Plan.Current}");
            sb.AppendLine("goals: " + string.Join("  ", brain.Goals.LastOptions.Select(o => $"{o.Name} {o.Utility:0.00}")));
            sb.AppendLine();
            foreach (ThoughtRecord r in brain.Log.Latest(12)) sb.AppendLine("<size=11>" + r.Text + "</size>");

            var textRect = new Rect(mapRect.xMax + 12, 10, Mathf.Max(360f, Screen.width - mapRect.xMax - 24), Screen.height - 20);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(textRect.x - 6, textRect.y - 4, textRect.width, 460), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(textRect, sb.ToString(), text);
            GUI.DrawTexture(new Rect(10, mapRect.yMax + 8, 240, 60), graph);
            GUI.Label(new Rect(254, mapRect.yMax + 8, 300, 40), "<size=11><color=#FF5A50>panic</color> vs <color=#78C8FF>setpoint</color></size>", text);
        }

        void DrawMap(StoreMap map, Rect rect, Vector3 body, Vector3 peak, Vector3 truth)
        {
            // Rows grow north, screen y grows down: flip vertically.
            GUI.DrawTextureWithTexCoords(rect, heat, new Rect(0, 1, 1, -1));
            Dot(map, rect, peak, new Color(1f, 0.9f, 0.2f), 6f);
            Dot(map, rect, body, new Color(0.3f, 0.95f, 1f), 7f);
            if (truth != Vector3.zero) Dot(map, rect, truth, new Color(1f, 0.2f, 1f), 6f);
        }

        static void Dot(StoreMap map, Rect rect, Vector3 world, Color colour, float size)
        {
            float u = (world.x - map.Origin.x) / (map.Columns * StoreMap.CellSize);
            float v = (world.z - map.Origin.z) / (map.Rows * StoreMap.CellSize);
            var r = new Rect(rect.x + u * rect.width - size * 0.5f, rect.y + (1f - v) * rect.height - size * 0.5f, size, size);
            GUI.color = colour;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawScrubber(StoreMap map)
        {
            var records = brain.Log.Records.Where(r => r.BeliefSnapshot != null).ToList();
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (records.Count == 0)
            {
                GUI.Label(new Rect(20, 20, 600, 30), "No decisions recorded this shift yet. [F2] close", text);
                return;
            }

            scrub = GUI.HorizontalSlider(new Rect(20, Screen.height - 40, Screen.width - 40, 20), scrub, 0f, 1f);
            int index = Mathf.Clamp(Mathf.RoundToInt(scrub * (records.Count - 1)), 0, records.Count - 1);
            ThoughtRecord rec = records[index];

            float[] snapshot = rec.BeliefSnapshot;
            Paint(map, c =>
            {
                Region r = map.Regions[map.CellRegion[c]];
                return r.Id < snapshot.Length ? snapshot[r.Id] / Mathf.Max(1, r.Cells.Count) : 0f;
            });

            float scale = Mathf.Min(5f, (Screen.height - 120f) / map.Rows);
            var mapRect = new Rect(20, 20, map.Columns * scale, map.Rows * scale);
            DrawMap(map, mapRect, rec.BodyPosition, Vector3.zero, rec.PlayerPosition);

            var sb = new StringBuilder();
            sb.AppendLine($"<b>REPLAY</b>  decision {index + 1}/{records.Count}   t={rec.T:0.0}s   [F2] close");
            sb.AppendLine();
            sb.AppendLine(rec.Text);
            if (rec.Peak != null) sb.AppendLine($"belief: {rec.Peak}  p={rec.Confidence:0.00}  H={rec.Entropy:0.00}  stale={rec.Stale:0}s");
            if (rec.RuledOut != null && rec.RuledOut.Count > 0) sb.AppendLine("ruled out: " + string.Join(", ", rec.RuledOut));
            if (rec.Options != null)
                foreach (ThoughtOption o in rec.Options) sb.AppendLine($"  {o.Name,-22} {o.Utility:0.00}   {o.Why}");
            if (rec.Chose != null) sb.AppendLine($"chose: <b>{rec.Chose}</b>");
            if (rec.Because != null) sb.AppendLine($"because: {rec.Because}");
            sb.AppendLine($"panic {rec.Panic:0.00} / target {rec.Target:0.00}   tension {rec.Tension:0.00}   pressure {rec.Pressure:+0.00;-0.00}");
            sb.AppendLine();
            sb.AppendLine("<color=#4DF2FF>■</color> KAREN   <color=#FF33FF>■</color> the employee (truth)   heat = belief");
            GUI.Label(new Rect(mapRect.xMax + 20, 20, Screen.width - mapRect.xMax - 40, Screen.height - 80), sb.ToString(), text);
        }
    }
}
