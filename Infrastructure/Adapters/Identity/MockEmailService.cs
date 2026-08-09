using Application.Interfaces.IServices;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Identity
{
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string to, string subject, string body, EmailAction? action = null)
        {
            _logger.LogInformation($"[MOCK EMAIL] To: {to} | Subject: {subject}");
            _logger.LogInformation($"[MOCK EMAIL BODY]\n{body}");
            if (action != null)
                _logger.LogInformation($"[MOCK EMAIL BUTTON] {action.Label} -> {action.Url}");
            return Task.CompletedTask;
        }
    }
}
