using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace TABSClosedAlpha
{
    // Deliberately small JSON reader: manifests are data only and the loader has no JSON dependency.
    internal static class ModJson
    {
        internal static ModMetadata Parse(string text)
        {
            var root = JsonReader.Parse(text) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Manifest root must be an object.");
            var mod = new ModMetadata();
            mod.Id = StringValue(root, "id"); mod.Name = StringValue(root, "name"); mod.Version = StringValue(root, "version");
            mod.Author = StringValue(root, "author"); mod.Description = StringValue(root, "description"); mod.Main = StringValue(root, "main"); mod.MainType = StringValue(root, "mainType");
            object dependencies; if (root.TryGetValue("dependencies", out dependencies) && dependencies is ArrayList) foreach (object value in (ArrayList)dependencies)
            {
                var name = value as string; if (name != null) { mod.Dependencies.Add(new ModDependency { Id = name }); continue; }
                var objectValue = value as Dictionary<string, object>; if (objectValue == null) throw new FormatException("dependencies entries must be strings or objects.");
                mod.Dependencies.Add(new ModDependency { Id = StringValue(objectValue, "id"), Version = StringValue(objectValue, "version") });
            }
            return mod;
        }
        static string StringValue(Dictionary<string, object> objectValue, string name) { object value; return objectValue.TryGetValue(name, out value) && value != null ? value as string : null; }
    }
    internal sealed class JsonReader
    {
        readonly string text; int index;
        JsonReader(string text) { this.text = text; }
        internal static object Parse(string text) { var reader = new JsonReader(text); var result = reader.Value(); reader.White(); if (reader.index != text.Length) throw new FormatException("Unexpected JSON characters."); return result; }
        object Value() { White(); if (index == text.Length) throw new FormatException("Unexpected end of JSON."); char c = text[index]; if (c == '{') return Object(); if (c == '[') return Array(); if (c == '"') return String(); if (c == 't') return Word("true", true); if (c == 'f') return Word("false", false); if (c == 'n') return Word("null", null); return Number(); }
        Dictionary<string, object> Object() { var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); index++; White(); if (Eat('}')) return result; while (true) { White(); if (index == text.Length || text[index] != '"') throw new FormatException("Object key expected."); string key = String(); White(); Need(':'); result[key] = Value(); White(); if (Eat('}')) return result; Need(','); } }
        ArrayList Array() { var result = new ArrayList(); index++; White(); if (Eat(']')) return result; while (true) { result.Add(Value()); White(); if (Eat(']')) return result; Need(','); } }
        string String() { Need('"'); var result = new System.Text.StringBuilder(); while (index < text.Length) { char c = text[index++]; if (c == '"') return result.ToString(); if (c != '\\') { result.Append(c); continue; } if (index == text.Length) break; c = text[index++]; if (c == '"' || c == '\\' || c == '/') result.Append(c); else if (c == 'b') result.Append('\b'); else if (c == 'f') result.Append('\f'); else if (c == 'n') result.Append('\n'); else if (c == 'r') result.Append('\r'); else if (c == 't') result.Append('\t'); else if (c == 'u') { if (index + 4 > text.Length) break; result.Append((char)Int32.Parse(text.Substring(index, 4), NumberStyles.HexNumber)); index += 4; } else throw new FormatException("Invalid JSON escape."); } throw new FormatException("Unterminated JSON string."); }
        object Number() { int start = index; while (index < text.Length && "-+0123456789.eE".IndexOf(text[index]) >= 0) index++; double result; if (!Double.TryParse(text.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out result)) throw new FormatException("Invalid JSON value."); return result; }
        object Word(string word, object value) { if (index + word.Length > text.Length || text.Substring(index, word.Length) != word) throw new FormatException("Invalid JSON value."); index += word.Length; return value; }
        void White() { while (index < text.Length && Char.IsWhiteSpace(text[index])) index++; }
        bool Eat(char expected) { if (index < text.Length && text[index] == expected) { index++; return true; } return false; }
        void Need(char expected) { White(); if (!Eat(expected)) throw new FormatException("Expected '" + expected + "'."); }
    }
}
