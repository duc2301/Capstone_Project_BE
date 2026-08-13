namespace Application.Interfaces.IServices
{
    public interface IIssueActivityService
    {
        Task MarkInProgressOnActivityAsync(Guid issueId, Guid actorId);
    }
}
