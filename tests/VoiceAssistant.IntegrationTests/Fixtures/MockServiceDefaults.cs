using Moq;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.IntegrationTests.Fixtures;

/// <summary>
/// Static helper that configures happy-path mock returns for all Azure services.
/// </summary>
public static class MockServiceDefaults
{
    public static void SetupHappyPath(
        Mock<ISpeechToTextService> sttMock,
        Mock<IChatService> chatMock,
        Mock<ITextToSpeechService> ttsMock)
    {
        SetupStt(sttMock);
        SetupChat(chatMock);
        SetupTts(ttsMock);
    }

    public static void SetupStt(Mock<ISpeechToTextService> sttMock)
    {
        sttMock.Setup(s => s.RecognizeAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                IsSuccess = true,
                Text = "你好",
                Confidence = 0.95
            });

        sttMock.Setup(s => s.RecognizeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechRecognitionResult
            {
                IsSuccess = true,
                Text = "你好",
                Confidence = 0.95
            });
    }

    public static void SetupChat(Mock<IChatService> chatMock)
    {
        chatMock.Setup(s => s.ChatAsync(
                It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("你好！有什么可以帮您？");
    }

    public static void SetupTts(Mock<ITextToSpeechService> ttsMock)
    {
        ttsMock.Setup(s => s.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioData
            {
                Data = [0x01, 0x02, 0x03, 0x04],
                ContentType = "audio/mp3"
            });
    }
}
