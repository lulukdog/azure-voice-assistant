using FluentAssertions;
using VoiceAssistant.IntegrationTests.Fixtures;

namespace VoiceAssistant.IntegrationTests.HealthCheck;

public class HealthCheckTests(VoiceAssistantWebApplicationFactory factory)
    : IClassFixture<VoiceAssistantWebApplicationFactory>
{
    [Fact]
    public async Task GetHealth_ReturnsHealthy()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }
}
