using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Options;
using VoiceAssistant.Infrastructure.Azure;

namespace VoiceAssistant.Infrastructure.Tests.Azure;

public class AzureSpeechToTextServiceTests
{
    /// <summary>
    /// 最大音频字节数: 60s * 16000Hz * 2 bytes (16-bit mono) = 1,920,000
    /// </summary>
    private const int MaxAudioBytes = 1_920_000;

    private readonly AzureSpeechToTextService _sut;

    public AzureSpeechToTextServiceTests()
    {
        var options = Options.Create(new AzureSpeechOptions
        {
            SubscriptionKey = "test-key",
            Region = "eastasia"
        });

        var logger = new Mock<ILogger<AzureSpeechToTextService>>();

        _sut = new AzureSpeechToTextService(options, logger.Object);
    }

    [Fact]
    public async Task RecognizeAsync_WithStream_ThrowsAudioTooLongException_WhenAudioExceedsMaxBytes()
    {
        // Arrange
        var oversizedData = new byte[MaxAudioBytes + 1];
        using var stream = new MemoryStream(oversizedData);

        // Act
        var act = () => _sut.RecognizeAsync(stream);

        // Assert
        await act.Should().ThrowAsync<AudioTooLongException>();
    }

    [Fact]
    public async Task RecognizeAsync_WithByteArray_ThrowsAudioTooLongException_WhenAudioExceedsMaxBytes()
    {
        // Arrange
        var oversizedData = new byte[MaxAudioBytes + 1];

        // Act
        var act = () => _sut.RecognizeAsync(oversizedData);

        // Assert
        await act.Should().ThrowAsync<AudioTooLongException>();
    }

    [Fact]
    public async Task RecognizeAsync_WithByteArray_AcceptsAudioWithinLimit()
    {
        // Arrange - exactly at the limit, should NOT throw AudioTooLongException
        var validData = new byte[MaxAudioBytes];

        // Act
        // This will fail with a SpeechRecognitionException (wrapping Azure SDK errors)
        // because there is no real Azure connection, but it should NOT throw AudioTooLongException.
        var act = () => _sut.RecognizeAsync(validData);

        // Assert
        await act.Should().NotThrowAsync<AudioTooLongException>();
    }
}
