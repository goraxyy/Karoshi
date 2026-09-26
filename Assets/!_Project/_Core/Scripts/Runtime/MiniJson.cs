using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Karoshi
{
    // Just enough JSON for the eval harness, the thought log and the map export.
    //
    // JsonUtility can't hold a dictionary or an array of mixed things, and the eval
    // protocol is exactly that, so this reads into plain Dictionary / List / double /
    // bool / string / null and writes from a small builder. No reflection, no allocation
    // surprises, and nothing to install.
    public static class MiniJson
    {
        // ---- reading ----------------------------------------------------------

        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var reader = new Reader(text);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw new System.FormatException($"Unexpected text after JSON value at {reader.Position}.");
            return value;
        }

        public static Dictionary<string, object> ParseObject(string text) => Parse(text) as Dictionary<string, object>;

        sealed class Reader
        {
            readonly string s;
            int i;

            public Reader(string text) { s = text; }
            public bool AtEnd => i >= s.Length;
            public int Position => i;

            public void SkipWhitespace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new System.FormatException("Unexpected end of JSON.");

                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                    throw new System.FormatException($"Expected '{word}' at {i}.");
                i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>();
                i++; // {
                SkipWhitespace();
                if (i < s.Length && s[i] == '}') { i++; return result; }

                while (true)
                {
                    SkipWhitespace();
                    string key = ReadString();
                    SkipWhitespace();
                    if (i >= s.Length || s[i] != ':') throw new System.FormatException($"Expected ':' at {i}.");
                    i++;
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (i >= s.Length) throw new System.FormatException("Unterminated object.");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return result; }
                    throw new System.FormatException($"Expected ',' or '}}' at {i}.");
                }
            }

            List<object> ReadArray()
            {
                var result = new List<object>();
                i++; // [
                SkipWhitespace();
                if (i < s.Length && s[i] == ']') { i++; return result; }

                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (i >= s.Length) throw new System.FormatException("Unterminated array.");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return result; }
                    throw new System.FormatException($"Expected ',' or ']' at {i}.");
                }
            }

            string ReadString()
            {
                if (i >= s.Length || s[i] != '"') throw new System.FormatException($"Expected string at {i}.");
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new System.FormatException("Bad unicode escape.");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                throw new System.FormatException("Unterminated string.");
            }

            object ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new System.FormatException($"Unexpected character '{s[i]}' at {i}.");
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        // ---- convenience accessors for parsed objects ---------------------------

        public static string GetString(this Dictionary<string, object> o, string key, string fallback = null)
        {
            return o != null && o.TryGetValue(key, out object v) && v != null ? v.ToString() : fallback;
        }

        public static double GetNumber(this Dictionary<string, object> o, string key, double fallback = 0)
        {
            if (o == null || !o.TryGetValue(key, out object v) || v == null) return fallback;
            if (v is double d) return d;
            if (v is bool b) return b ? 1 : 0;
            return double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
        }

        public static bool GetBool(this Dictionary<string, object> o, string key, bool fallback = false)
        {
            if (o == null || !o.TryGetValue(key, out object v) || v == null) return fallback;
            if (v is bool b) return b;
            if (v is double d) return d != 0;
            return bool.TryParse(v.ToString(), out bool parsed) ? parsed : fallback;
        }

        public static Dictionary<string, object> GetObject(this Dictionary<string, object> o, string key)
        {
            return o != null && o.TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;
        }

        // ---- writing -------------------------------------------------------------

        public static string Escape(string text)
        {
            if (text == null) return "null";
            var sb = new StringBuilder(text.Length + 2);
            sb.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // The escaped text without the surrounding quotes, for writers that add their own.
        public static string EscapeInner(string text)
        {
            string quoted = Escape(text ?? string.Empty);
            return quoted.Substring(1, quoted.Length - 2);
        }

        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "null";
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        // Serialise plain values: dictionaries, lists, arrays, strings, numbers, bools, null,
        // and anything implementing IJsonWritable.
        public static string Serialize(object value)
        {
            var w = new JsonWriter();
            w.Value(value);
            return w.ToString();
        }
    }

    public interface IJsonWritable
    {
        void WriteJson(JsonWriter writer);
    }

    // A forward-only writer. Commas are handled for you: every container remembers whether
    // it has written anything yet, and a value straight after a key never takes a comma.
    public sealed class JsonWriter
    {
        readonly StringBuilder sb = new StringBuilder(256);
        readonly Stack<bool> hasContent = new Stack<bool>();
        bool afterKey;

        public override string ToString() => sb.ToString();

        void Prefix()
        {
            if (afterKey) { afterKey = false; return; }
            if (hasContent.Count == 0) return;
            if (hasContent.Peek()) sb.Append(',');
            else { hasContent.Pop(); hasContent.Push(true); }
        }

        public JsonWriter BeginObject() { Prefix(); sb.Append('{'); hasContent.Push(false); return this; }
        public JsonWriter EndObject() { hasContent.Pop(); sb.Append('}'); return this; }
        public JsonWriter BeginArray() { Prefix(); sb.Append('['); hasContent.Push(false); return this; }
        public JsonWriter EndArray() { hasContent.Pop(); sb.Append(']'); return this; }

        public JsonWriter Key(string name)
        {
            Prefix();
            sb.Append(MiniJson.Escape(name)).Append(':');
            afterKey = true;
            return this;
        }

        public JsonWriter Field(string name, object value) => Key(name).Value(value);

        public JsonWriter Value(object value)
        {
            switch (value)
            {
                case null: Prefix(); sb.Append("null"); break;
                case string s: Prefix(); sb.Append(MiniJson.Escape(s)); break;
                case bool b: Prefix(); sb.Append(b ? "true" : "false"); break;
                case double d: Prefix(); sb.Append(MiniJson.Number(d)); break;
                case float f: Prefix(); sb.Append(MiniJson.Number(f)); break;
                case int n: Prefix(); sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long l: Prefix(); sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case IJsonWritable writable: writable.WriteJson(this); break;
                case System.Collections.IDictionary dict:
                    BeginObject();
                    foreach (System.Collections.DictionaryEntry entry in dict)
                        Field(entry.Key.ToString(), entry.Value);
                    EndObject();
                    break;
                case System.Collections.IEnumerable list:
                    BeginArray();
                    foreach (object item in list) Value(item);
                    EndArray();
                    break;
                default: Prefix(); sb.Append(MiniJson.Escape(value.ToString())); break;
            }
            return this;
        }
    }
}
