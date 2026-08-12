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

        public int ElementsNotCoveredByStandard { get; set; }
        public List<LoiMissingFieldDTO> Missing { get; set; } = new();
        public List<LoiUnmappedParamDTO> Unmapped { get; set; } = new();
        public List<LoiUncoveredComponentDTO> NotCovered { get; set; } = new();

        public List<LoiSectionDTO> Sections { get; set; } = new();
    }

    public static class LoiEvaluator
    {
        public const int MaxMissingFields = 60;
        public const int MaxUnmappedParams = 20;
        public const int MaxUncoveredComponents = 20;

        public const int MaxInstancesPerRule = 100;

        public const double ConformantThreshold = 90;
        public const double WarningThreshold = 60;

        public static readonly HashSet<string> IgnoredEntityTypes = new(StringComparer.Ordinal)
        {
            "IFCPROJECT", "IFCSITE", "IFCBUILDING", "IFCBUILDINGSTOREY", "IFCSPACE", "IFCSPATIALZONE",
            "IFCGROUP", "IFCZONE", "IFCSYSTEM", "IFCBUILDINGSYSTEM",
            "IFCREINFORCINGBAR", "IFCREINFORCINGMESH", "IFCREINFORCINGELEMENT", "IFCTENDON", "IFCTENDONANCHOR"
        };

        private static readonly string[] ClassificationSeedNames =
        {
            "ma so phan loai", "ma so phan loai omni", "ma dinh danh theo nhom omni",
            "ma phan loai", "phan loai theo omni", "ma omni"
        };

        public static LoiEvalResult Evaluate(
            IfcLoiModel model,
            IReadOnlyList<LoiRequirement> requirements,
            IReadOnlyList<LoiFieldAlias> aliases,
            LoiStage targetStage)
            => Evaluate(model, requirements, aliases, Array.Empty<LoiComponent>(), targetStage);

        public static LoiEvalResult Evaluate(
            IfcLoiModel model,
            IReadOnlyList<LoiRequirement> requirements,
            IReadOnlyList<LoiFieldAlias> aliases,
            IReadOnlyList<LoiComponent> components,
            LoiStage targetStage)
        {
            var result = new LoiEvalResult();

            var aliasesByParam = BuildAliasIndex(aliases);
            var classificationNames = BuildClassificationNames(aliasesByParam);
            var byComponent = BuildComponentIndex(requirements, aliasesByParam, targetStage);

            var knownCodes = requirements
                .Where(r => !string.IsNullOrWhiteSpace(r.ComponentCode))
                .Select(r => NormalizeCode(r.ComponentCode!))
                .ToHashSet(StringComparer.Ordinal);

            var taxonomy = new Dictionary<string, LoiComponent>(StringComparer.Ordinal);
            foreach (var c in components)
                taxonomy.TryAdd(c.CodeNormalized, c);
            var elements = model.Elements
                .Where(e => !IgnoredEntityTypes.Contains(e.EntityType))
                .ToList();

            var collector = new ReportCollector();

            if (byComponent.Count == 0 || elements.Count == 0)
            {
                result.Verdict = LoiVerdict.Unknown;
                result.TotalElements = elements.Count;
                result.Sections = BuildSections(model, collector, scannedElements: false, anythingScored: false);
                return result;
            }

            var missingCounter =
                new Dictionary<(string ComponentCode, string? Variant, string FieldName), MissingAccumulator>();
            var unknownParams = new Dictionary<(LoiDiscipline Discipline, string Name), int>();
            var uncovered = new Dictionary<string, LoiUncoveredComponentDTO>(StringComparer.Ordinal);
            long totalRules = 0, satisfiedRules = 0;

            foreach (var element in elements)
            {
                var code = ReadClassificationCode(element, classificationNames);
                var normalizedCode = code is null ? null : NormalizeCode(code);

                if (normalizedCode is null || !knownCodes.Contains(normalizedCode))
                {
                    if (normalizedCode is not null && taxonomy.TryGetValue(normalizedCode, out var known))
                    {
                        result.ElementsNotCoveredByStandard++;
                        CountUncovered(uncovered, known);
                        collector.Uncovered.Add(new Finding(element, new List<string> { known.Name }));
                        continue;
                    }

                    result.ElementsWithUnknownType++;
                    if (code is null) collector.NoCode.Add(new Finding(element, new List<string>()));
                    else collector.UnknownCode.Add(new Finding(element, new List<string> { code }));
                    continue;
                }
                if (!byComponent.TryGetValue(normalizedCode, out var component))
                    continue;

                var best = PickBestVariant(element, component.Variants);
                if (best is null) continue;

                totalRules += best.Rules.Count;
                satisfiedRules += best.SatisfiedCount;
                if (best.SatisfiedCount == best.Rules.Count) result.ConformantElements++;

                foreach (var rule in best.MissingRules)
                    Accumulate(missingCounter, rule);

                CollectPerElementFindings(collector, element, best);
                CollectUnknownParams(element, component, unknownParams);
            }

            result.TotalElements = elements.Count;
            result.CoveragePercent = totalRules == 0 ? 0 : Math.Round(satisfiedRules * 100.0 / totalRules, 1);
            result.Verdict = totalRules == 0 ? LoiVerdict.Unknown : ToVerdict(result.CoveragePercent);
            result.Missing = missingCounter.Values
                .OrderByDescending(x => x.Count)
                .Take(MaxMissingFields)
                .Select(x => new LoiMissingFieldDTO
                {
                    FieldName = x.FieldName,
                    Variant = x.Variant,
                    Discipline = x.Discipline,
                    ComponentCode = x.ComponentCode,
                    ComponentName = x.ComponentName,
                    Group = x.Group,
                    Stage = x.Stage,
                    MissingCount = x.Count
                })
                .ToList();
            result.Unmapped = BuildSuggestions(unknownParams, requirements);
            result.NotCovered = uncovered.Values
                .OrderByDescending(x => x.ElementCount)
                .Take(MaxUncoveredComponents)
                .ToList();
            result.Sections = BuildSections(
                model, collector, scannedElements: true, anythingScored: totalRules > 0,
                unmappedParams: result.Unmapped, coveragePercent: result.CoveragePercent);
            return result;
        }


        private sealed class CappedList<T>
        {
            private readonly List<T> _items = new();

            public int Count { get; private set; }
            public bool Truncated => Count > _items.Count;
            public IReadOnlyList<T> Items => _items;

            public void Add(T item)
            {
                Count++;
                if (_items.Count < MaxInstancesPerRule) _items.Add(item);
            }
        }

        private readonly record struct Finding(IfcElementInfo Element, List<string> Details);

        private sealed class ReportCollector
        {
            public CappedList<Finding> NoCode { get; } = new();
            public CappedList<Finding> UnknownCode { get; } = new();
            public CappedList<Finding> Uncovered { get; } = new();
            public CappedList<Finding> BlankValues { get; } = new();

            public Dictionary<LoiParamGroup, CappedList<Finding>> MissingByGroup { get; } = new();

            public HashSet<LoiParamGroup> GroupsWithRules { get; } = new();
        }

        private static void CollectPerElementFindings(
            ReportCollector c, IfcElementInfo element, VariantScore best)
        {
            foreach (var rule in best.Rules)
                c.GroupsWithRules.Add(rule.Group);

            foreach (var byGroup in best.MissingRules.GroupBy(r => r.Group))
            {
                if (!c.MissingByGroup.TryGetValue(byGroup.Key, out var list))
                    c.MissingByGroup[byGroup.Key] = list = new CappedList<Finding>();
                list.Add(new Finding(element, byGroup.Select(r => r.FieldName).Distinct(StringComparer.Ordinal).ToList()));
            }

            var missing = best.MissingRules.ToHashSet();
            var blank = best.Rules
                .Where(r => !missing.Contains(r) && IsBlank(r, element))
                .Select(r => r.FieldName)
                .ToList();
            if (blank.Count > 0) c.BlankValues.Add(new Finding(element, blank));
        }

        private static bool IsBlank(RuleGroup rule, IfcElementInfo element)
        {
            var found = false;
            foreach (var name in rule.AcceptableNames)
            {
                if (!element.Properties.TryGetValue(name, out var value)) continue;
                if (!string.IsNullOrWhiteSpace(value)) return false;
                found = true;
            }
            return found;
        }

        private static readonly HashSet<string> SupportedSchemas = new(StringComparer.OrdinalIgnoreCase)
        {
            "IFC2X3", "IFC4", "IFC4X1", "IFC4X3", "IFC4X3_ADD1", "IFC4X3_ADD2"
        };

        private static List<LoiSectionDTO> BuildSections(
            IfcLoiModel model, ReportCollector c, bool scannedElements, bool anythingScored,
            IReadOnlyList<LoiUnmappedParamDTO>? unmappedParams = null, double coveragePercent = 0)
        {
            var unmapped = unmappedParams ?? Array.Empty<LoiUnmappedParamDTO>();

            var syntax = Section(LoiCheckSection.Syntax,
                Rule("STP001", model.IsStepFile ? LoiSeverity.Passed : LoiSeverity.Error));

            var schema = Section(LoiCheckSection.Schema, SchemaRule(model.SchemaName));

            var classification = scannedElements
                ? Section(LoiCheckSection.Classification,
                    InstanceRule("CLS001", c.NoCode, LoiSeverity.Error),
                    InstanceRule("CLS002", c.UnknownCode, LoiSeverity.Error),
                    InstanceRule("CLS003", c.Uncovered, LoiSeverity.Applicable, whenEmpty: LoiSeverity.NotApplicable))
                : NotApplicableSection(LoiCheckSection.Classification, "CLS001", "CLS002", "CLS003");

            var required = Section(
                LoiCheckSection.RequiredFields, BuildFieldRules(c, anythingScored, coveragePercent).ToArray());

            var practice = scannedElements
                ? Section(LoiCheckSection.GoodPractice,
                    UnmappedRule(unmapped),
                    InstanceRule("PRC002", c.BlankValues, LoiSeverity.Warning))
                : NotApplicableSection(LoiCheckSection.GoodPractice, "PRC001", "PRC002");

            return new List<LoiSectionDTO> { syntax, schema, classification, required, practice };
        }

        private static LoiRuleResultDTO UnmappedRule(IReadOnlyList<LoiUnmappedParamDTO> unmapped)
        {
            if (unmapped.Count == 0) return Rule("PRC001", LoiSeverity.Passed);

            var rule = new LoiRuleResultDTO
            {
                Code = "PRC001",
                Severity = LoiSeverity.Warning,
                OccurrenceCount = unmapped.Count
            };
            foreach (var item in unmapped)
                rule.Instances.Add(new LoiInstanceDTO
                {
                    Name = item.ParamNameInModel,
                    Details = { item.SuggestedParamName }
                });
            return rule;
        }

        private static IEnumerable<LoiRuleResultDTO> BuildFieldRules(
            ReportCollector c, bool anythingScored, double coveragePercent)
        {
            var whenMissing = coveragePercent >= ConformantThreshold ? LoiSeverity.Warning : LoiSeverity.Error;

            foreach (LoiParamGroup group in System.Enum.GetValues<LoiParamGroup>())
            {
                var code = $"LOI{(int)group:000}";

                if (!anythingScored || !c.GroupsWithRules.Contains(group))
                {
                    yield return Rule(code, LoiSeverity.NotApplicable);
                    continue;
                }

                if (!c.MissingByGroup.TryGetValue(group, out var found))
                {
                    yield return Rule(code, LoiSeverity.Passed);
                    continue;
                }

                yield return ToRule(code, found, whenMissing);
            }
        }

        private static LoiRuleResultDTO SchemaRule(string? schemaName)
        {
            if (string.IsNullOrWhiteSpace(schemaName))
                return Rule("SCH001", LoiSeverity.Error);

            var severity = SupportedSchemas.Contains(schemaName) ? LoiSeverity.Passed : LoiSeverity.Warning;
            var rule = Rule("SCH001", severity);
            if (severity != LoiSeverity.Passed)
                rule.Instances.Add(new LoiInstanceDTO { EntityType = "IFCPROJECT", Details = { schemaName } });
            return rule;
        }

        private static LoiRuleResultDTO Rule(string code, LoiSeverity severity) =>
            new() { Code = code, Severity = severity };

        private static LoiRuleResultDTO InstanceRule(
            string code, CappedList<Finding> found, LoiSeverity whenAny,
            LoiSeverity whenEmpty = LoiSeverity.Passed)
            => found.Count == 0 ? Rule(code, whenEmpty) : ToRule(code, found, whenAny);

        private static LoiRuleResultDTO ToRule(string code, CappedList<Finding> found, LoiSeverity severity)
        {
            var rule = new LoiRuleResultDTO
            {
                Code = code,
                Severity = severity,
                OccurrenceCount = found.Count,
                Truncated = found.Truncated
            };

            foreach (var (element, details) in found.Items)
                rule.Instances.Add(new LoiInstanceDTO
                {
                    GlobalId = element.GlobalId,
                    Name = element.Name,
                    EntityType = element.EntityType,
                    Details = details
                });
            return rule;
        }

        private static LoiSectionDTO NotApplicableSection(LoiCheckSection section, params string[] codes) =>
            Section(section, codes.Select(code => Rule(code, LoiSeverity.NotApplicable)).ToArray());

        private static LoiSectionDTO Section(LoiCheckSection section, params LoiRuleResultDTO[] rules) =>
            new()
            {
                Section = section,
                Severity = rules.Length == 0 ? LoiSeverity.NotApplicable : rules.Max(r => r.Severity),
                Rules = rules.ToList()
            };

        private static void CountUncovered(
            Dictionary<string, LoiUncoveredComponentDTO> counter, LoiComponent component)
        {
            if (counter.TryGetValue(component.CodeNormalized, out var found))
            {
                found.ElementCount++;
                return;
            }
            counter[component.CodeNormalized] = new LoiUncoveredComponentDTO
            {
                ComponentCode = component.Code,
                ComponentName = component.Name,
                ElementCount = 1
            };
        }

        private static void CollectUnknownParams(
            IfcElementInfo element,
            ComponentRules component,
            Dictionary<(LoiDiscipline Discipline, string Name), int> counter)
        {
            foreach (var name in element.Properties.Keys)
            {
                if (component.AllAcceptableNames.Contains(name)) continue;
                var key = (component.Discipline, name);
                counter[key] = counter.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        private static List<LoiUnmappedParamDTO> BuildSuggestions(
            Dictionary<(LoiDiscipline Discipline, string Name), int> unknownParams,
            IReadOnlyList<LoiRequirement> requirements)
        {
            if (unknownParams.Count == 0) return new List<LoiUnmappedParamDTO>();

            var displayByDiscipline = new Dictionary<LoiDiscipline, Dictionary<string, string>>();
            foreach (var r in requirements)
            {
                if (!displayByDiscipline.TryGetValue(r.Discipline, out var display))
                {
                    display = new Dictionary<string, string>(StringComparer.Ordinal);
                    displayByDiscipline[r.Discipline] = display;
                }
                display.TryAdd(r.ParamNameNormalized, r.ParamName);
            }

            var suggestions = new List<LoiUnmappedParamDTO>();
            foreach (var ((discipline, name), count) in unknownParams)
            {
                if (!displayByDiscipline.TryGetValue(discipline, out var display)) continue;

                var hit = LoiParamSuggester.Suggest(name, display.Keys);
                if (hit is null) continue;
                suggestions.Add(new LoiUnmappedParamDTO
                {
                    ParamNameInModel = name,
                    SuggestedParamName = display[hit.ParamNameNormalized],
                    SuggestedParamNameNormalized = hit.ParamNameNormalized,
                    Confidence = hit.Score,
                    ElementCount = count
                });
            }

            return suggestions
                .OrderByDescending(s => s.ElementCount)
                .ThenByDescending(s => s.Confidence)
                .Take(MaxUnmappedParams)
                .ToList();
        }

        private static LoiVerdict ToVerdict(double coverage) => coverage switch
        {
            >= ConformantThreshold => LoiVerdict.Conformant,
            >= WarningThreshold => LoiVerdict.Warning,
            _ => LoiVerdict.Critical
        };


        private sealed class RuleGroup
        {
            public string FieldName { get; init; } = string.Empty;
            public string? Variant { get; init; }
            public LoiDiscipline Discipline { get; init; }
            public string ComponentCode { get; init; } = string.Empty;
            public string ComponentName { get; init; } = string.Empty;
            public LoiParamGroup Group { get; init; }
            public LoiStage Stage { get; init; }
            public HashSet<string> AcceptableNames { get; } = new(StringComparer.Ordinal);
        }

        private sealed class VariantRules
        {
            public string? Variant { get; init; }
            public List<RuleGroup> Rules { get; } = new();
        }

        private sealed class ComponentRules
        {
            public LoiDiscipline Discipline { get; set; }

            public List<VariantRules> Variants { get; } = new();

            public HashSet<string> AllAcceptableNames { get; } = new(StringComparer.Ordinal);
        }

        private static Dictionary<string, ComponentRules> BuildComponentIndex(
            IReadOnlyList<LoiRequirement> requirements,
            IReadOnlyDictionary<string, List<string>> aliasesByParam,
            LoiStage targetStage)
        {
            var index = new Dictionary<string, ComponentRules>(StringComparer.Ordinal);

            var applicable = requirements.Where(r => r.Stage <= targetStage && !string.IsNullOrWhiteSpace(r.ComponentCode));

            foreach (var byCode in applicable.GroupBy(r => NormalizeCode(r.ComponentCode!)))
            {
                var component = new ComponentRules { Discipline = byCode.First().Discipline };
                foreach (var byVariant in byCode.GroupBy(r => r.Variant))
                {
                    var variant = new VariantRules { Variant = byVariant.Key };
                    foreach (var byField in byVariant.GroupBy(r => r.FieldNameNormalized, StringComparer.Ordinal))
                    {
                        var first = byField.First();
                        var rule = new RuleGroup
                        {
                            FieldName = first.FieldName,
                            Variant = byVariant.Key,
                            Discipline = first.Discipline,
                            ComponentCode = first.ComponentCode ?? string.Empty,
                            ComponentName = first.ComponentName ?? string.Empty,
                            Group = first.ParamGroup,
                            Stage = byField.Min(r => r.Stage)
                        };
                        AddName(rule.AcceptableNames, byField.Key, aliasesByParam);
                        foreach (var r in byField)
                            AddName(rule.AcceptableNames, r.ParamNameNormalized, aliasesByParam);

                        if (rule.AcceptableNames.Count == 0) continue;
                        variant.Rules.Add(rule);
                        component.AllAcceptableNames.UnionWith(rule.AcceptableNames);
                    }
                    if (variant.Rules.Count > 0) component.Variants.Add(variant);
                }
                if (component.Variants.Count > 0) index[byCode.Key] = component;
            }
            return index;
        }

        private static void AddName(
            HashSet<string> target, string normalizedName, IReadOnlyDictionary<string, List<string>> aliasesByParam)
        {
            if (string.IsNullOrEmpty(normalizedName)) return;
            target.Add(normalizedName);
            if (aliasesByParam.TryGetValue(normalizedName, out var alias))
                foreach (var a in alias) target.Add(a);
        }

        private static Dictionary<string, List<string>> BuildAliasIndex(IReadOnlyList<LoiFieldAlias> aliases)
        {
            var classOf = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var a in aliases)
            {
                if (string.IsNullOrEmpty(a.FieldNameNormalized) || string.IsNullOrEmpty(a.AliasNormalized))
                    continue;

                classOf.TryGetValue(a.FieldNameNormalized, out var left);
                classOf.TryGetValue(a.AliasNormalized, out var right);

                if (left is null && right is null)
                {
                    var created = new HashSet<string>(StringComparer.Ordinal)
                        { a.FieldNameNormalized, a.AliasNormalized };
                    classOf[a.FieldNameNormalized] = created;
                    classOf[a.AliasNormalized] = created;
                }
                else if (left is not null && right is null)
                {
                    left.Add(a.AliasNormalized);
                    classOf[a.AliasNormalized] = left;
                }
                else if (left is null)
                {
                    right!.Add(a.FieldNameNormalized);
                    classOf[a.FieldNameNormalized] = right;
                }
                else if (right is not null && !ReferenceEquals(left, right))
                {
                    foreach (var name in right)
                    {
                        left.Add(name);
                        classOf[name] = left;
                    }
                }
            }

            return classOf.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);
        }


        private sealed class VariantScore
        {
            public required IReadOnlyList<RuleGroup> Rules { get; init; }
            public required List<RuleGroup> MissingRules { get; init; }
            public int SatisfiedCount => Rules.Count - MissingRules.Count;
        }

        private static VariantScore? PickBestVariant(IfcElementInfo element, List<VariantRules> variants)
        {
            VariantRules? winner = null;
            double bestRatio = -1;
            var bestSatisfied = -1;

            foreach (var variant in variants)
            {
                var satisfied = variant.Rules.Count(rule => IsSatisfied(rule, element));
                var ratio = satisfied / (double)variant.Rules.Count;

                if (winner is not null
                    && !IsBetterVariant(ratio, satisfied, variant, bestRatio, bestSatisfied, winner))
                    continue;

                bestRatio = ratio;
                bestSatisfied = satisfied;
                winner = variant;
            }

            if (winner is null) return null;

            var missing = winner.Rules.Where(rule => !IsSatisfied(rule, element)).ToList();
            return new VariantScore { Rules = winner.Rules, MissingRules = missing };
        }

        private static bool IsBetterVariant(
            double ratio, int satisfied, VariantRules candidate,
            double bestRatio, int bestSatisfied, VariantRules current)
        {
            if (ratio != bestRatio) return ratio > bestRatio;
            if (satisfied != bestSatisfied) return satisfied > bestSatisfied;
            return CompareVariantNames(candidate.Variant, current.Variant) < 0;
        }

        private static int CompareVariantNames(string? left, string? right)
        {
            if (left is null) return right is null ? 0 : -1;
            if (right is null) return 1;
            return string.CompareOrdinal(left, right);
        }

        private static bool IsSatisfied(RuleGroup rule, IfcElementInfo element) =>
            rule.AcceptableNames.Any(element.Properties.ContainsKey);

        private sealed class MissingAccumulator
        {
            public required string FieldName { get; init; }
            public required string? Variant { get; init; }
            public required LoiDiscipline Discipline { get; init; }
            public required string ComponentCode { get; init; }
            public required string ComponentName { get; init; }
            public required LoiParamGroup Group { get; init; }
            public required LoiStage Stage { get; init; }
            public int Count { get; set; }
        }

        private static void Accumulate(
            Dictionary<(string ComponentCode, string? Variant, string FieldName), MissingAccumulator> counter,
            RuleGroup rule)
        {
            var key = (rule.ComponentCode, rule.Variant, rule.FieldName);
            if (counter.TryGetValue(key, out var acc))
            {
                acc.Count++;
                return;
            }
            counter[key] = new MissingAccumulator
            {
                FieldName = rule.FieldName,
                Variant = rule.Variant,
                Discipline = rule.Discipline,
                ComponentCode = rule.ComponentCode,
                ComponentName = rule.ComponentName,
                Group = rule.Group,
                Stage = rule.Stage,
                Count = 1
            };
        }


        private static List<string> BuildClassificationNames(
            IReadOnlyDictionary<string, List<string>> aliasesByParam)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string name)
            {
                if (name.Length > 0 && seen.Add(name)) names.Add(name);
            }

            foreach (var seed in ClassificationSeedNames)
            {
                Add(seed);
                if (aliasesByParam.TryGetValue(seed, out var alias))
                    foreach (var a in alias) Add(a);
            }
            return names;
        }

        private static string? ReadClassificationCode(IfcElementInfo element, List<string> classificationNames)
        {
            foreach (var name in classificationNames)
                if (element.Properties.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }

        public static string NormalizeCode(string code)
        {
            var sb = new StringBuilder(code.Length);
            foreach (var ch in code)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
