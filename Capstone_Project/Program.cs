using Capstone_Project.DataHandler.Exceptions;
using Capstone_Project.Extensions;
using Infrastructure.Configurations;

var builder = WebApplication.CreateBuilder(args);

// Controllers + Exception Filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ExceptionFilter>();
});

// Infrastructure (DB, AutoMapper, Repositories, Services)
builder.Services.AddInfrastructureService(builder.Configuration);

// Validation
builder.Services.AddGlobalValidation(builder.Configuration);

// Authentication + Swagger
builder.Services.AuthenticationServices(builder);
builder.Services.SwaggerServices(builder);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Capstone Project API v1");
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
