namespace Domain.Entities
{
    public class LoiFieldAlias
    {
        public Guid Id { get; set; }

        public string FieldNameNormalized { get; set; } = null!;

        public string AliasNormalized { get; set; } = null!;
    }
}
