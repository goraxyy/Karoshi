using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kehai.Replay
{
    // A camera move set by hand: keyframes (shift time, where the camera is, where it looks,
    // its field of view and focus) saved with K in the replay, and played back as one smooth
    // path. Positions, field of view and focus follow a Hermite curve through the keys; the
    // rotation a squad (a smooth spherical spline). The path is still at its first and last key.
    //
    // Saved as <stem>.path.json next to the recording; schema in
    // tools/marketing/schemas/shot-path.schema.json. ReplayRender plays it with -shot <file>.
    public sealed class ShotPath
    {
        [System.Serializable]
        public sealed class Key
        {
            public float t;
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
            public float fov = 60f;
            public float focus = 5f;       // metres, for depth of field
        }

        [System.Serializable]
        internal sealed class File
        {
            public int version = 1;
            public string krec = "";
            public List<Key> keys = new List<Key>();
        }

        public const float SameKey = 0.05f;   // a key this close in time to another replaces it

        public string Krec = "";
        public readonly List<Key> Keys = new List<Key>();

        public static string PathFor(string krecPath) =>
            Path.Combine(Path.GetDirectoryName(krecPath) ?? "", Path.GetFileNameWithoutExtension(krecPath) + ".path.json");

        public static ShotPath Load(string path)
        {
            var file = JsonUtility.FromJson<File>(System.IO.File.ReadAllText(path));
            var p = new ShotPath { Krec = file.krec ?? "" };
            if (file.keys != null) foreach (Key k in file.keys) p.Add(k);
            return p;
        }

        public void Save(string path)
        {
            var file = new File { krec = Krec, keys = new List<Key>(Keys) };
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(file, true));
        }

        public void Add(Key key)
        {
            key.rotation = Normalise(key.rotation);
            Keys.RemoveAll(k => Mathf.Abs(k.t - key.t) < SameKey);
            int i = Keys.FindIndex(k => k.t > key.t);
            if (i < 0) Keys.Add(key); else Keys.Insert(i, key);
        }

        public void Add(float t, Vector3 position, Quaternion rotation, float fov, float focus) =>
            Add(new Key { t = t, position = position, rotation = rotation, fov = fov, focus = focus });

        public bool RemoveLast()
        {
            if (Keys.Count == 0) return false;
            Keys.RemoveAt(Keys.Count - 1);
            return true;
        }

        public float Start => Keys.Count > 0 ? Keys[0].t : 0f;
        public float End => Keys.Count > 0 ? Keys[Keys.Count - 1].t : 0f;

        // Where the camera is at t: at a key exactly, still before the first and after the last.
        public bool Evaluate(float t, out Key at)
        {
            at = null;
            if (Keys.Count == 0) return false;
            if (Keys.Count == 1 || t <= Keys[0].t) { at = Copy(Keys[0]); return true; }
            if (t >= End) { at = Copy(Keys[Keys.Count - 1]); return true; }

            int i = 0;
            while (i + 1 < Keys.Count - 1 && Keys[i + 1].t <= t) i++;
            Key a = Keys[i], b = Keys[i + 1];
            float span = Mathf.Max(1e-4f, b.t - a.t);
            float u = (t - a.t) / span;

            at = new Key
            {
                t = t,
                position = Hermite(a.position, b.position, Slope(i, k => k.position) * span, Slope(i + 1, k => k.position) * span, u),
                fov = Hermite(a.fov, b.fov, Slope(i, k => k.fov) * span, Slope(i + 1, k => k.fov) * span, u),
                focus = Mathf.Max(0.1f, Hermite(a.focus, b.focus, Slope(i, k => k.focus) * span, Slope(i + 1, k => k.focus) * span, u)),
                rotation = Squad(i, u)
            };
            return true;
        }

        // Rate of change at key i (per second), from its neighbours; none at the ends.
        Vector3 Slope(int i, System.Func<Key, Vector3> of)
        {
            if (i <= 0 || i >= Keys.Count - 1) return Vector3.zero;
            return (of(Keys[i + 1]) - of(Keys[i - 1])) / Mathf.Max(1e-4f, Keys[i + 1].t - Keys[i - 1].t);
        }

        float Slope(int i, System.Func<Key, float> of)
        {
            if (i <= 0 || i >= Keys.Count - 1) return 0f;
            return (of(Keys[i + 1]) - of(Keys[i - 1])) / Mathf.Max(1e-4f, Keys[i + 1].t - Keys[i - 1].t);
        }

        static Vector3 Hermite(Vector3 p0, Vector3 p1, Vector3 m0, Vector3 m1, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * m1;
        }

        static float Hermite(float p0, float p1, float m0, float m1, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * m1;
        }

        // ---- rotation: squad through the keys --------------------------------------------------

        Quaternion Squad(int i, float u)
        {
            Quaternion q0 = Keys[i].rotation, q1 = Near(q0, Keys[i + 1].rotation);
            Quaternion s0 = Inner(i), s1 = Inner(i + 1);
            s0 = Near(q0, s0);
            s1 = Near(q1, s1);
            return Normalise(Quaternion.Slerp(Quaternion.Slerp(q0, q1, u), Quaternion.Slerp(s0, s1, u), 2f * u * (1f - u)));
        }

        // The squad control point at key i; the key itself at the ends, so the path eases there.
        Quaternion Inner(int i)
        {
            Quaternion q = Keys[i].rotation;
            if (i <= 0 || i >= Keys.Count - 1) return q;
            Quaternion inv = Quaternion.Inverse(q);
            Quaternion next = Near(q, Keys[i + 1].rotation), prev = Near(q, Keys[i - 1].rotation);
            Vector3 a = Log(inv * next), b = Log(inv * prev);
            return Normalise(q * Exp(-(a + b) * 0.25f));
        }

        static Quaternion Near(Quaternion reference, Quaternion q) =>
            Quaternion.Dot(reference, q) < 0f ? new Quaternion(-q.x, -q.y, -q.z, -q.w) : q;

        static Vector3 Log(Quaternion q)
        {
            q = Normalise(q);
            var v = new Vector3(q.x, q.y, q.z);
            float s = v.magnitude;
            if (s < 1e-6f) return Vector3.zero;
            float angle = Mathf.Atan2(s, q.w);
            return v / s * angle;
        }

        static Quaternion Exp(Vector3 v)
        {
            float angle = v.magnitude;
            if (angle < 1e-6f) return Quaternion.identity;
            Vector3 axis = v / angle * Mathf.Sin(angle);
            return new Quaternion(axis.x, axis.y, axis.z, Mathf.Cos(angle));
        }

        static Quaternion Normalise(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        static Key Copy(Key k) => new Key { t = k.t, position = k.position, rotation = k.rotation, fov = k.fov, focus = k.focus };
    }
}
