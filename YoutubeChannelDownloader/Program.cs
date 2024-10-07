using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using YoutubeChannelDownloader;
using YoutubeChannelDownloader.Configurations;
using YoutubeChannelDownloader.Services;
using YoutubeExplode;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile($"appsettings.json", false, true)
    .AddJsonFile($"appsettings.Development.json", false, true)
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
    .AddSingleton<ChannelDownloaderService>()
    .BuildServiceProvider();

ChannelDownloaderService channelDownloaderService = serviceProvider.GetRequiredService<ChannelDownloaderService>();

string channelId = "https://www.youtube.com/@bobito217";
// string channelId = "UCOuW8i824NprPKrM4Pq4R0w";// боксёр

await channelDownloaderService.DownloadVideosAsync(channelId);
//await channelDownloaderService.DownloadPlaylists(channelId);

Log.CloseAndFlush();
