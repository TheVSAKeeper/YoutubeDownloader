namespace YoutubeDownloader.Api.Configurations;

public class TelegramBotOptions
{
    private string? _tokenPath;

    public required string TokenPath { get; init; }

    public string FullTokenPath => _tokenPath ??= IsRelativePath
        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TokenPath)
        : TokenPath;

    public required bool IsRelativePath { get; init; }
}
