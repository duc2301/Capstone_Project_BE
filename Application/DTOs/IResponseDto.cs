namespace Application.DTOs
{
    // Response DTO luôn có Id -> phục vụ CreatedAtAction (201 + Location)
    public interface IResponseDto
    {
        Guid Id { get; set; }
    }
}
