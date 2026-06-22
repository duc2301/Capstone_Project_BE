using Application.Interfaces.IServices;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string to, string subject, string body)
        {
            _logger.LogInformation($"[MOCK EMAIL] To: {to} | Subject: {subject}");
            _logger.LogInformation($"[MOCK EMAIL BODY]\n{body}");
            return Task.CompletedTask;
        }
    }
}
