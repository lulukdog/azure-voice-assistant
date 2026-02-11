using VoiceAssistant.Core;
using VoiceAssistant.Infrastructure;
using VoiceAssistant.Api.Hubs;
using VoiceAssistant.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

// Register application services
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS for development (compatible with SignalR credentials)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHealthChecks("/health");

app.Run();
