using System.Globalization;
using System.Text;
using Application.Interfaces.IServices;

namespace Application.Services.Loi
{
    public sealed class IfcElementInfo
    {
        public string EntityType { get; init; } = string.Empty;
        public string? Name { get; init; }

        public Dictionary<string, string?> Properties { get; } = new();
    }

    public sealed class IfcLoiModel
    {
        public string? SchemaName { get; init; }
        public List<IfcElementInfo> Elements { get; } = new();
    }

    public sealed class IfcStepPropertyExtractor : IIfcLoiExtractor
    {
        public async Task<IfcLoiModel> ExtractAsync(Stream ifcStream, CancellationToken ct = default)
        {
            using var reader = new StreamReader(ifcStream, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
            var text = await reader.ReadToEndAsync(ct);
            return Parse(text);
        }

        public static IfcLoiModel Parse(string text)
        {
            var schema = ExtractSchema(text);
            var instances = ParseInstances(text);

            var props = new Dictionary<int, (string name, string? value)>();
            var psets = new Dictionary<int, (string name, List<int> propIds)>();
            var rels = new List<(List<int> elems, int psetId)>();

            foreach (var (id, inst) in instances)
            {
                var kw = inst.keyword;
                if (kw == "IFCPROPERTYSET")
                {
                    var p = SplitTopLevel(inst.body);
                    if (p.Count < 5) continue;
                    psets[id] = (IfcFieldText.Decode(p[2]), ParseRefList(p[4]));
                }
                else if (kw == "IFCRELDEFINESBYPROPERTIES")
                {
                    var p = SplitTopLevel(inst.body);
                    if (p.Count < 6) continue;
                    var psetRef = ParseRef(p[5]);
                    if (psetRef is null) continue;
                    rels.Add((ParseRefList(p[4]), psetRef.Value));
                }
                else if (kw.StartsWith("IFCPROPERTY", StringComparison.Ordinal))
                {
                    var p = SplitTopLevel(inst.body);
                    if (p.Count == 0) continue;
                    var name = IfcFieldText.Decode(p[0]);
                    string? value = kw == "IFCPROPERTYSINGLEVALUE" && p.Count > 2 ? ExtractNominalValue(p[2]) : null;
                    props[id] = (name, value);
                }
            }

            var builders = new Dictionary<int, IfcElementInfo>();
            foreach (var (elemIds, psetId) in rels)
            {
                if (!psets.TryGetValue(psetId, out var pset)) continue;
                foreach (var elemId in elemIds)
                {
                    if (!builders.TryGetValue(elemId, out var element))
                    {
                        element = BuildElement(elemId, instances);
                        builders[elemId] = element;
                    }
                    foreach (var pid in pset.propIds)
                    {
                        if (!props.TryGetValue(pid, out var pr)) continue;
                        var key = IfcFieldText.Normalize(pr.name);
                        if (key.Length == 0) continue;
                        element.Properties[key] = pr.value;
                    }
                }
            }

            var model = new IfcLoiModel { SchemaName = schema };
            model.Elements.AddRange(builders.Values);
            return model;
        }

        private static IfcElementInfo BuildElement(int elemId, Dictionary<int, (string keyword, string body)> instances)
        {
            string entityType = "UNKNOWN";
            string? name = null;
            if (instances.TryGetValue(elemId, out var inst))
            {
                entityType = inst.keyword;
                var p = SplitTopLevel(inst.body);
                if (p.Count > 2 && p[2].StartsWith('\'')) name = IfcFieldText.Decode(p[2]);
            }
            return new IfcElementInfo { EntityType = entityType, Name = name };
        }


        private static string? ExtractSchema(string text)
        {
            var idx = text.IndexOf("FILE_SCHEMA", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var open = text.IndexOf('\'', idx);
            if (open < 0) return null;
            var close = text.IndexOf('\'', open + 1);
            if (close < 0) return null;
            return text.Substring(open + 1, close - open - 1);
        }

        private static Dictionary<int, (string keyword, string body)> ParseInstances(string s)
        {
            var result = new Dictionary<int, (string, string)>();
            int n = s.Length;
            int i = 0;
            while (i < n)
            {
                if (s[i] != '#') { i++; continue; }

                int j = i + 1;
                while (j < n && char.IsDigit(s[j])) j++;
                if (j == i + 1) { i++; continue; }

                if (!int.TryParse(s.AsSpan(i + 1, j - i - 1), out int id)) { i = j; continue; }

                int k = j;
                while (k < n && char.IsWhiteSpace(s[k])) k++;
                if (k >= n || s[k] != '=') { i = j; continue; }
                k++;
                while (k < n && char.IsWhiteSpace(s[k])) k++;

                int ks = k;
                while (k < n && (char.IsLetterOrDigit(s[k]) || s[k] == '_')) k++;
                if (k == ks) { i = j; continue; }
                string keyword = s.Substring(ks, k - ks).ToUpperInvariant();

                while (k < n && char.IsWhiteSpace(s[k])) k++;
                if (k >= n || s[k] != '(') { i = k; continue; }

                int bodyStart = k + 1;
                int depth = 1;
                int p = k + 1;
                bool inStr = false;
                while (p < n && depth > 0)
                {
                    char ch = s[p];
                    if (inStr)
                    {
                        if (ch == '\'')
                        {
                            if (p + 1 < n && s[p + 1] == '\'') { p += 2; continue; }
                            inStr = false;
                        }
                        p++;
                    }
                    else
                    {
                        if (ch == '\'') inStr = true;
                        else if (ch == '(') depth++;
                        else if (ch == ')') depth--;
                        p++;
                    }
                }

                result[id] = (keyword, s.Substring(bodyStart, p - 1 - bodyStart));
                i = p;
            }
            return result;
        }

        private static List<string> SplitTopLevel(string body)
        {
            var parts = new List<string>();
            int depth = 0;
            bool inStr = false;
            int start = 0;
            for (int p = 0; p < body.Length; p++)
            {
                char ch = body[p];
                if (inStr)
                {
                    if (ch == '\'')
                    {
                        if (p + 1 < body.Length && body[p + 1] == '\'') { p++; continue; }
                        inStr = false;
                    }
                    continue;
                }
                if (ch == '\'') inStr = true;
                else if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (ch == ',' && depth == 0)
                {
                    parts.Add(body.Substring(start, p - start).Trim());
                    start = p + 1;
                }
            }
            parts.Add(body.Substring(start).Trim());
            return parts;
        }

        private static List<int> ParseRefList(string s)
        {
            var list = new List<int>();
            foreach (var token in SplitTopLevel(s.Trim().TrimStart('(').TrimEnd(')')))
            {
                var r = ParseRef(token);
                if (r is not null) list.Add(r.Value);
            }
            return list;
        }

        private static int? ParseRef(string s)
        {
            s = s.Trim();
            if (s.Length < 2 || s[0] != '#') return null;
            return int.TryParse(s.AsSpan(1), out int id) ? id : null;
        }

        private static string? ExtractNominalValue(string s)
        {
            s = s.Trim();
            if (s.Length == 0 || s == "$" || s == "*") return null;
            int open = s.IndexOf('(');
            if (open >= 0 && s.EndsWith(')'))
            {
                var inner = s.Substring(open + 1, s.Length - open - 2).Trim();
                if (inner.StartsWith('\'')) return IfcFieldText.Decode(inner);
                return inner.Length == 0 ? null : inner;
            }
            if (s.StartsWith('\'')) return IfcFieldText.Decode(s);
            return s;
        }
    }

    public static class IfcFieldText
    {
        public static string Decode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var s = raw;
            if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') s = s.Substring(1, s.Length - 2);

            var sb = new StringBuilder(s.Length);
            int i = 0;
            int n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '\'' && i + 1 < n && s[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                if (c != '\\') { sb.Append(c); i++; continue; }

                if (Match(s, i, "\\X2\\"))
                {
                    i += 4;
                    while (i + 4 <= n && IsHex(s, i, 4))
                    {
                        sb.Append((char)int.Parse(s.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                    }
                    if (Match(s, i, "\\X0\\")) i += 4;
                    continue;
                }
                if (Match(s, i, "\\X4\\"))
                {
                    i += 4;
                    while (i + 8 <= n && IsHex(s, i, 8))
                    {
                        int cp = int.Parse(s.AsSpan(i, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        sb.Append(char.ConvertFromUtf32(cp & 0x10FFFF));
                        i += 8;
                    }
                    if (Match(s, i, "\\X0\\")) i += 4;
                    continue;
                }
                if (Match(s, i, "\\X\\") && i + 5 <= n && IsHex(s, i + 3, 2))
                {
                    sb.Append((char)int.Parse(s.AsSpan(i + 3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 5;
                    continue;
                }
                if (Match(s, i, "\\S\\") && i + 3 < n)
                {
                    sb.Append((char)(s[i + 3] + 128));
                    i += 4;
                    continue;
                }
                i++;
            }
            return sb.ToString();
        }

        public static string Normalize(string decoded)
        {
            if (string.IsNullOrWhiteSpace(decoded)) return string.Empty;
            var s = decoded.Trim();

            int k = 0;
            while (k < s.Length && char.IsDigit(s[k])) k++;
            if (k > 0 && k < s.Length && (s[k] == '-' || s[k] == '.' || s[k] == ')' || s[k] == ' ' || s[k] == ':'))
                s = s[(k + 1)..];

            s = RemoveDiacritics(s).ToLowerInvariant();

            var sb = new StringBuilder(s.Length);
            bool prevSpace = false;
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch)) { sb.Append(ch); prevSpace = false; }
                else if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            return sb.ToString().Trim();
        }

        public static string DecodeAndNormalize(string raw) => Normalize(Decode(raw));

        private static string RemoveDiacritics(string input)
        {
            var normalized = input.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool Match(string s, int i, string token) =>
            i + token.Length <= s.Length && string.CompareOrdinal(s, i, token, 0, token.Length) == 0;

        private static bool IsHex(string s, int i, int len)
        {
            for (int j = 0; j < len; j++)
                if (!Uri.IsHexDigit(s[i + j])) return false;
            return true;
        }
    }
}
