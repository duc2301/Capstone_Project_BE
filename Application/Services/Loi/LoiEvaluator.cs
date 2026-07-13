using System.Text;
using Application.DTOs.ResponseDTOs.Loi;
using Domain.Entities;
using Domain.Enum.Loi;

namespace Application.Services.Loi
{
    public sealed class LoiEvalResult
    {
        public LoiVerdict Verdict { get; set; } = LoiVerdict.None;
        public double CoveragePercent { get; set; }
        public int TotalElements { get; set; }
        public int ConformantElements { get; set; }
        public int ElementsWithUnknownType { get; set; }
        public List<LoiMissingFieldDTO> Missing { get; set; } = new();
    }

    public static class LoiEvaluator
    {
        public const int MaxMissingFields = 60;

        public static readonly HashSet<string> IgnoredEntityTypes = new(StringComparer.Ordinal)
        {
            "IFCPROJECT", "IFCSITE", "IFCBUILDING", "IFCBUILDINGSTOREY", "IFCSPACE", "IFCSPATIALZONE",
            "IFCGROUP", "IFCZONE", "IFCSYSTEM", "IFCBUILDINGSYSTEM",
            "IFCREINFORCINGBAR", "IFCREINFORCINGMESH", "IFCREINFORCINGELEMENT", "IFCTENDON", "IFCTENDONANCHOR"
        };

        private static readonly string[] ClassificationCodeKeys =
        {
            "ma so phan loai", "ma dinh danh theo nhom omni", "ma phan loai", "phan loai theo omni", "ma cau kien"
        };

        public static LoiEvalResult Evaluate(
            IfcLoiModel model,
            IReadOnlyList<LoiRequirement> requirements,
            IReadOnlyList<LoiFieldAlias> aliases)
        {
            var acceptable = BuildAcceptableNames(requirements, aliases);

            var commonFields = requirements.Where(r => r.IsCommon)
                .GroupBy(r => r.FieldNameNormalized).Select(g => g.First()).ToList();

            var byComponent = requirements
                .Where(r => !r.IsCommon && !string.IsNullOrWhiteSpace(r.ComponentCode))
                .GroupBy(r => NormalizeCode(r.ComponentCode!))
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.FieldNameNormalized).Select(x => x.First()).ToList());

            var elements = model.Elements
                .Where(e => !IgnoredEntityTypes.Contains(e.EntityType))
                .ToList();

            var result = new LoiEvalResult();

            if (commonFields.Count == 0 && byComponent.Count == 0)
            {
                result.Verdict = LoiVerdict.Unknown;
                return result;
            }
            if (elements.Count == 0)
            {
                result.Verdict = LoiVerdict.Unknown;
                return result;
            }

            var missingCounter = new Dictionary<string, (LoiRequirement req, int count)>(StringComparer.Ordinal);
            long totalPairs = 0, satisfiedPairs = 0;

            foreach (var el in elements)
            {
                List<LoiRequirement> typeFields = new();
                var code = ReadClassificationCode(el);
                if (code is not null && byComponent.TryGetValue(NormalizeCode(code), out var tf))
                    typeFields = tf;
                else
                    result.ElementsWithUnknownType++;

                var required = commonFields.Concat(typeFields)
                    .GroupBy(r => r.FieldNameNormalized).Select(g => g.First()).ToList();
                if (required.Count == 0) continue;

                int presentCount = 0;
                foreach (var req in required)
                {
                    var names = acceptable.TryGetValue(req.FieldNameNormalized, out var set)
                        ? set
                        : new HashSet<string>(StringComparer.Ordinal) { req.FieldNameNormalized };

                    if (names.Any(nm => el.Properties.ContainsKey(nm)))
                    {
                        presentCount++;
                    }
                    else
                    {
                        missingCounter[req.FieldNameNormalized] =
                            missingCounter.TryGetValue(req.FieldNameNormalized, out var cur)
                                ? (cur.req, cur.count + 1)
                                : (req, 1);
                    }
                }

                totalPairs += required.Count;
                satisfiedPairs += presentCount;
                if (presentCount == required.Count) result.ConformantElements++;
            }

            result.TotalElements = elements.Count;
            result.CoveragePercent = totalPairs == 0 ? 0 : Math.Round(satisfiedPairs * 100.0 / totalPairs, 1);
            result.Verdict = result.ConformantElements == elements.Count ? LoiVerdict.Conformant : LoiVerdict.Warning;
            result.Missing = missingCounter.Values
                .OrderByDescending(x => x.count)
                .Take(MaxMissingFields)
                .Select(x => new LoiMissingFieldDTO
                {
                    FieldName = x.req.FieldName,
                    Group = x.req.ParamGroup,
                    Stage = x.req.Stage,
                    MissingCount = x.count
                })
                .ToList();
            return result;
        }

        private static Dictionary<string, HashSet<string>> BuildAcceptableNames(
            IReadOnlyList<LoiRequirement> requirements, IReadOnlyList<LoiFieldAlias> aliases)
        {
            var acceptable = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            void Ensure(string field)
            {
                if (!acceptable.ContainsKey(field))
                    acceptable[field] = new HashSet<string>(StringComparer.Ordinal) { field };
            }

            foreach (var r in requirements) Ensure(r.FieldNameNormalized);
            foreach (var a in aliases)
            {
                Ensure(a.FieldNameNormalized);
                acceptable[a.FieldNameNormalized].Add(a.AliasNormalized);
            }
            return acceptable;
        }

        private static string? ReadClassificationCode(IfcElementInfo el)
        {
            foreach (var key in ClassificationCodeKeys)
                if (el.Properties.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }

        private static string NormalizeCode(string code)
        {
            var sb = new StringBuilder(code.Length);
            foreach (var ch in code)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
