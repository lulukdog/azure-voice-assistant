using VoiceAssistant.Core;
using VoiceAssistant.Infrastructure;
using VoiceAssistant.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Register application services
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<VoiceHub>("/hubs/voice");

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.Run();
