using Application.BackgroundServices;
using Application.Interfaces.IServices;
using Capstone_Project.DataHandler.Exceptions;
using Capstone_Project.Extensions;
using Capstone_Project.SignalR;
using Infrastructure.Configurations;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion Community license (convert Office -> PDF)
builder.RegisterSyncfusionLicense();

// Controllers + Exception Filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ExceptionFilter>();
});

// Infrastructure (DB, AutoMapper, Repositories, Services)
builder.Services.AddInfrastructureService(builder.Configuration);

// Worker dịch model nền (tiêu thụ IModelTranslationQueue) — đăng ký ở host vì cần Microsoft.Extensions.Hosting.
builder.Services.AddHostedService<ModelTranslationWorker>();

// Validation
builder.Services.AddGlobalValidation(builder.Configuration);

// Authentication + Swagger (JWT cũng được nhận trên SignalR query ?access_token)
builder.Services.AuthenticationServices(builder);
builder.Services.SwaggerServices(builder);

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();
builder.Services.AddScoped<INotificationPusher, SignalRNotificationPusher>();
builder.Services.AddScoped<IMarkupBroadcaster, SignalRMarkupBroadcaster>();

// CORS — withCredentials cần cho SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var feLocalBaseUrl = builder.Configuration["FrontendLocalBaseUrl"];
        var feDeployURL = builder.Configuration["FrontendDeployBaseUrl"];
        var allowedOrigins = new[] { feLocalBaseUrl, feDeployURL }
                            .Where(url => !string.IsNullOrEmpty(url))
                            .ToArray();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});


builder.Services.AddOpenApi();

var app = builder.Build();

app.ApplyMigrations();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Capstone Project API v1");
    });
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<MarkupHub>("/hubs/markup");

app.Run();
