using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.ExceptionMiddleware;
using Domain.Entities;
using Domain.Enum.Loi;

namespace Application.Services.Loi
{
    public sealed record LoiStandardRuleTable(
        string Name,
        string? Description,
        List<LoiParameter> Parameters,
        List<LoiComponent> Components,
        List<LoiRequirement> Requirements)
    {
        private const string ResourceName = "Application.Resources.loi-standard-bxd347.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private static readonly Lazy<LoiStandardRuleTable> Cached = new(Read, isThreadSafe: true);

        public static LoiStandardRuleTable Load() => Cached.Value;

        private static LoiStandardRuleTable Read()
        {
            using var stream = typeof(LoiStandardRuleTable).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new ApiExceptionResponse(
                    "Thiếu tài nguyên bảng chuẩn BXD 347 trong bản build nên không sinh được file mẫu.", 500);

            var payload = JsonSerializer.Deserialize<StandardTablePayload>(stream, JsonOptions)
                ?? throw new ApiExceptionResponse("Không đọc được bảng chuẩn BXD 347 nhúng trong bản build.", 500);

            return new LoiStandardRuleTable(
                payload.Name,
                payload.Description,
                payload.Parameters.Select(p => new LoiParameter
                {
                    Discipline = p.Discipline,
                    Name = p.Name,
                    NameNormalized = p.NameNormalized,
                    ParamGroup = p.ParamGroup,
                    OrderIndex = p.OrderIndex
                }).ToList(),
                payload.Components.Select(c => new LoiComponent
                {
                    Discipline = c.Discipline,
                    Code = c.Code,
                    CodeNormalized = c.CodeNormalized,
                    Name = c.Name
                }).ToList(),
                payload.Requirements.Select(r => new LoiRequirement
                {
                    Discipline = r.Discipline,
                    ComponentCode = r.ComponentCode,
                    ComponentName = r.ComponentName,
                    Variant = r.Variant,
                    FieldOrder = r.FieldOrder,
                    FieldName = r.FieldName,
                    FieldNameNormalized = r.FieldNameNormalized,
                    ParamNameNormalized = r.ParamNameNormalized,
                    Stage = r.Stage
                }).ToList());
        }

        private sealed class StandardTablePayload
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public List<ParameterPayload> Parameters { get; set; } = new();
            public List<ComponentPayload> Components { get; set; } = new();
            public List<RequirementPayload> Requirements { get; set; } = new();
        }

        private sealed class ParameterPayload
        {
            public LoiDiscipline Discipline { get; set; }
            public string Name { get; set; } = string.Empty;
            public string NameNormalized { get; set; } = string.Empty;
            public LoiParamGroup ParamGroup { get; set; }
            public int OrderIndex { get; set; }
        }

        private sealed class ComponentPayload
        {
            public LoiDiscipline Discipline { get; set; }
            public string Code { get; set; } = string.Empty;
            public string CodeNormalized { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed class RequirementPayload
        {
            public LoiDiscipline Discipline { get; set; }
            public string? ComponentCode { get; set; }
            public string? ComponentName { get; set; }
            public string? Variant { get; set; }
            public int FieldOrder { get; set; }
            public string FieldName { get; set; } = string.Empty;
            public string FieldNameNormalized { get; set; } = string.Empty;
            public string ParamNameNormalized { get; set; } = string.Empty;
            public LoiStage Stage { get; set; }
        }
    }
}
