namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiRuleSetDTO
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsDefault { get; set; }

        public bool IsSystem { get; set; }

        public int ComponentCount { get; set; }

        public int RequirementCount { get; set; }

        public int ParameterCount { get; set; }

        public int ProjectCount { get; set; }

        public int InheritingProjectCount { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
