using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VoiceAssistant.Core.Options;
using VoiceAssistant.Infrastructure.Azure;

namespace VoiceAssistant.Infrastructure.Tests.Azure;

public class AzureTextToSpeechServiceTests
{
    private readonly AzureTextToSpeechService _sut;

    public AzureTextToSpeechServiceTests()
    {
        var options = Options.Create(new AzureSpeechOptions
        {
            SubscriptionKey = "test-key",
            Region = "eastasia"
        });

        var logger = new Mock<ILogger<AzureTextToSpeechService>>();

        _sut = new AzureTextToSpeechService(options, logger.Object);
    }

    [Fact]
    public async Task SynthesizeAsync_ThrowsArgumentException_WhenTextIsEmpty()
    {
        // Act
        var act = () => _sut.SynthesizeAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text");
    }

    [Fact]
    public async Task SynthesizeAsync_ThrowsArgumentException_WhenTextIsNull()
    {
        // Act
        var act = () => _sut.SynthesizeAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text");
    }

    [Fact]
    public async Task SynthesizeAsync_ThrowsArgumentException_WhenTextExceedsMaxLength()
    {
        // Arrange
        var longText = new string('a', 5001);

        // Act
        var act = () => _sut.SynthesizeAsync(longText);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text")
            .WithMessage("*5000*");
    }

    [Fact]
    public async Task SynthesizeToStreamAsync_ThrowsArgumentException_WhenTextIsEmpty()
    {
        // Arrange
        using var outputStream = new MemoryStream();

        // Act
        var act = () => _sut.SynthesizeToStreamAsync(string.Empty, outputStream);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text");
    }

    [Fact]
    public async Task SynthesizeToStreamAsync_ThrowsArgumentException_WhenTextExceedsMaxLength()
    {
        // Arrange
        var longText = new string('a', 5001);
        using var outputStream = new MemoryStream();

        // Act
        var act = () => _sut.SynthesizeToStreamAsync(longText, outputStream);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text")
            .WithMessage("*5000*");
    }
}
