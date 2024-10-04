using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using YoutubeChannelDownloader;
using YoutubeChannelDownloader.Configurations;
using YoutubeExplode;

JsonSerializerOptions serializerOptions = new()
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

ServiceProvider serviceProvider = new ServiceCollection()
    .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
    .Configure<DownloadOptions>(configuration.GetSection(nameof(DownloadOptions)))
    .Configure<FFmpegOptions>(configuration.GetSection(nameof(FFmpegOptions)))
    .AddSingleton<Helper>()
    .AddSingleton<YoutubeClient>()
    .AddSingleton<DownloadService>()
    .AddSingleton<YoutubeDownloadService>()
    .AddSingleton<FFmpegConverter>()
    .AddSingleton<FFmpeg>()
    .AddSingleton<HttpClient>()
    .BuildServiceProvider();

ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

Helper helper = serviceProvider.GetRequiredService<Helper>();

string basePath = @"E:\bobgroup\projects\youtubeDownloader\downloadChannel";
//string basePath = @"C:\Downloads";
string chanelId = "UCGNZ41YzeZuLHcEOGt835gA";
string dirPath = Path.Combine(basePath, chanelId);
string videosPath = Path.Combine(dirPath, "videos");
string dataPath = Path.Combine(dirPath, "data.json");

if (!Directory.Exists(dirPath))
{
    Directory.CreateDirectory(dirPath);
}

helper.RefreshDirectories(videosPath);

if (File.Exists(dataPath))
{
    string videoData = await File.ReadAllTextAsync(dataPath);
    List<VideoInfo>? videos = JsonSerializer.Deserialize<List<VideoInfo>>(videoData);

    if (videos != null && videos.Count != 0)
    {
        videos.Reverse();

        foreach (VideoInfo video in videos.Take(3))
        {
            await helper.GetItem(video, videosPath);
        }
    }
    else
    {
        logger.LogWarning("No videos found in data.json");
    }
}
else
{
    List<VideoInfo> videos = await helper.Download(chanelId);
    string videoData = JsonSerializer.Serialize(videos, serializerOptions);
    await File.WriteAllTextAsync(dataPath, videoData, Encoding.UTF8);
}

helper.RefreshDirectories(videosPath);

Log.CloseAndFlush();
