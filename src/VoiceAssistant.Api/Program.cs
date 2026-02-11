using VoiceAssistant.Core;
using VoiceAssistant.Infrastructure;
using VoiceAssistant.Api.Hubs;
using VoiceAssistant.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    // 60s of 16kHz 16-bit mono PCM ≈ 1.92MB raw → ~2.56MB base64
    options.MaximumReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
});
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
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHealthChecks("/health");

app.Run();
