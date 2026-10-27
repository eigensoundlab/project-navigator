// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Reads the legacy ProjectNavigator.asset files (Unity YAML written by ScriptableSingleton up to
    /// version 0.3.0) without going through Unity's loader. Those files reference the settings script
    /// by GUID; after a reinstall with different .meta GUIDs Unity cannot resolve the script and loads
    /// them as empty. The data itself is still in the file, so this parses the small YAML subset Unity
    /// writes for plain serialized fields and hands it to EditorJsonUtility.
    /// </summary>
    internal static class LegacyAssetImporter
    {
        static readonly HashSet<string> k_BoolFields = new HashSet<string>
        {
            "defaultsCreated", "inheritToChildren", "tintFiles", "enabled", "rulesEnabled",
            "recordParentFolderOfSelectedAssets", "ignorePackages", "trackFiles", "overlayEnabled",
            "showPinBadge", "tintFilesInColoredFolders", "showNotesInProjectWindow", "inspectorStripEnabled",
            "inspectorStripForFiles", "colorScopeShared", "compactMode", "floatingOnTop",
            "reopenFloatingOnStartup", "floatingWasOpen", "floatingCompact"
        };

        // Only these fields are numbers; everything else stays a string (GUIDs can be all digits).
        static readonly HashSet<string> k_NumberFields = new HashSet<string>
        {
            "version", "score", "lastVisitTicks", "cursor", "maxHistory", "halfLifeDays", "pollIntervalSeconds",
            "frequentCount", "recentCount", "recentFilesCount", "folderClickAction",
            "r", "g", "b", "a", "x", "y", "width", "height", "serializedVersion"
        };

        static readonly Regex k_Number = new Regex(@"^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$", RegexOptions.CultureInvariant);

        /// <summary>Overwrites <paramref name="target"/> with the data found in the legacy file. False when there is no usable file.</summary>
        public static bool TryImport(UnityEngine.Object target, string assetPath, out string error)
        {
            error = null;
            if (!File.Exists(assetPath)) return false;
            try
            {
                var json = ToJson(assetPath);
                if (json == null)
                {
                    error = "No MonoBehaviour block found.";
                    return false;
                }
                EditorJsonUtility.FromJsonOverwrite(json, target);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Converts the legacy file to the JSON shape EditorJsonUtility expects, or null.</summary>
        public static string ToJson(string assetPath)
        {
            var lines = File.ReadAllLines(assetPath);
            int start = Array.FindIndex(lines, l => l.StartsWith("MonoBehaviour:", StringComparison.Ordinal));
            if (start < 0) return null;
            var parser = new Parser(lines, start + 1);
            var root = parser.ParseMapping(IndentOf(lines, start + 1));
            var sb = new StringBuilder();
            WriteJson(sb, root, null);
            return sb.ToString();
        }

        static int IndentOf(string[] lines, int index)
        {
            if (index >= lines.Length) return 0;
            var line = lines[index];
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }

        // ------------------------------------------------------------------ JSON writer

        static void WriteJson(StringBuilder sb, object value, string key)
        {
            switch (value)
            {
                case Dictionary<string, object> map:
                    sb.Append('{');
                    bool first = true;
                    foreach (var pair in map)
                    {
                        if (pair.Key.StartsWith("m_", StringComparison.Ordinal)) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('"').Append(Escape(pair.Key)).Append("\":");
                        WriteJson(sb, pair.Value, pair.Key);
                    }
                    sb.Append('}');
                    break;
                case List<object> list:
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteJson(sb, list[i], key);
                    }
                    sb.Append(']');
                    break;
                case string s:
                    if (key != null && k_BoolFields.Contains(key))
                        sb.Append(s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");
                    else if (key != null && k_NumberFields.Contains(key) && k_Number.IsMatch(s))
                        sb.Append(s.StartsWith(".") ? "0" + s : s.StartsWith("-.") ? "-0" + s.Substring(1) : s);
                    else
                        sb.Append('"').Append(Escape(s)).Append('"');
                    break;
                default:
                    sb.Append("null");
                    break;
            }
        }

        static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ YAML subset parser

        /// <summary>
        /// Handles what Unity writes for plain fields: block mappings, block sequences placed at the
        /// parent key's indentation ("key:" followed by "- item"), flow mappings ({r: 1, g: 0}),
        /// empty flow sequences ([]), single/double quoted and plain scalars.
        /// </summary>
        sealed class Parser
        {
            readonly string[] m_Lines;
            int m_Index;

            public Parser(string[] lines, int startIndex)
            {
                m_Lines = lines;
                m_Index = startIndex;
            }

            bool AtEnd => m_Index >= m_Lines.Length || m_Lines[m_Index].StartsWith("---", StringComparison.Ordinal);
            string Line => m_Lines[m_Index];
            int Indent => IndentOf(m_Lines, m_Index);
            string Body => Line.Substring(Indent);

            public Dictionary<string, object> ParseMapping(int indent)
            {
                var map = new Dictionary<string, object>();
                while (!AtEnd && Line.Trim().Length > 0 && Indent == indent && !Body.StartsWith("- ", StringComparison.Ordinal))
                {
                    var body = Body;
                    int colon = FindKeyColon(body);
                    if (colon < 0) break;
                    string key = body.Substring(0, colon).Trim();
                    string rest = body.Substring(colon + 1).Trim();
                    m_Index++;

                    if (rest.Length > 0)
                    {
                        map[key] = ParseScalarOrFlow(rest);
                        continue;
                    }

                    // Value continues on following lines: a sequence at the same indent, a nested mapping, or nothing.
                    if (!AtEnd && Line.Trim().Length > 0 && Indent == indent && Body.StartsWith("- ", StringComparison.Ordinal))
                        map[key] = ParseSequence(indent);
                    else if (!AtEnd && Line.Trim().Length > 0 && Indent > indent)
                        map[key] = ParseMapping(Indent);
                    else
                        map[key] = "";
                }
                return map;
            }

            List<object> ParseSequence(int indent)
            {
                var list = new List<object>();
                while (!AtEnd && Line.Trim().Length > 0 && Indent == indent && Body.StartsWith("- ", StringComparison.Ordinal))
                {
                    string item = Body.Substring(2);
                    if (FindKeyColon(item) >= 0 && !item.StartsWith("{", StringComparison.Ordinal) && !item.StartsWith("'", StringComparison.Ordinal) && !item.StartsWith("\"", StringComparison.Ordinal))
                    {
                        // Mapping item: rewrite "- key: v" as "  key: v" and parse the item as a mapping at indent + 2.
                        m_Lines[m_Index] = new string(' ', indent + 2) + item;
                        list.Add(ParseMapping(indent + 2));
                    }
                    else
                    {
                        list.Add(ParseScalarOrFlow(item));
                        m_Index++;
                    }
                }
                return list;
            }

            static int FindKeyColon(string body)
            {
                // The key ends at the first ": " or a trailing ':'; quoted keys are not produced by Unity.
                int i = body.IndexOf(": ", StringComparison.Ordinal);
                if (i >= 0) return i;
                return body.EndsWith(":", StringComparison.Ordinal) ? body.Length - 1 : -1;
            }

            static object ParseScalarOrFlow(string text)
            {
                text = text.Trim();
                if (text == "[]") return new List<object>();
                if (text == "{}") return new Dictionary<string, object>();
                if (text.StartsWith("{", StringComparison.Ordinal) && text.EndsWith("}", StringComparison.Ordinal))
                {
                    var map = new Dictionary<string, object>();
                    foreach (var part in SplitTopLevel(text.Substring(1, text.Length - 2)))
                    {
                        int colon = part.IndexOf(':');
                        if (colon < 0) continue;
                        map[part.Substring(0, colon).Trim()] = ParseScalarOrFlow(part.Substring(colon + 1));
                    }
                    return map;
                }
                if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
                {
                    var list = new List<object>();
                    foreach (var part in SplitTopLevel(text.Substring(1, text.Length - 2)))
                        if (part.Trim().Length > 0) list.Add(ParseScalarOrFlow(part));
                    return list;
                }
                if (text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'')
                    return text.Substring(1, text.Length - 2).Replace("''", "'");
                if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                    return Regex.Unescape(text.Substring(1, text.Length - 2));
                return text;
            }

            static IEnumerable<string> SplitTopLevel(string text)
            {
                int depth = 0;
                int start = 0;
                bool inSingle = false, inDouble = false;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '\'' && !inDouble) inSingle = !inSingle;
                    else if (c == '"' && !inSingle) inDouble = !inDouble;
                    else if (inSingle || inDouble) continue;
                    else if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        yield return text.Substring(start, i - start);
                        start = i + 1;
                    }
                }
                yield return text.Substring(start);
            }
        }
    }
}
