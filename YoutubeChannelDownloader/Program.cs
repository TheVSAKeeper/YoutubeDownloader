using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using YoutubeChannelDownloader;
using YoutubeChannelDownloader.Configurations;
using YoutubeExplode;

bool isDevelopment = File.Exists(Path.Combine(Environment.CurrentDirectory, "appsettings.Development.json"));

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile($"appsettings{(isDevelopment ? ".Development" : "")}.json", false, true)
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

string channelId = "UCGNZ41YzeZuLHcEOGt835gA";
await channelDownloaderService.DownloadVideosAsync(channelId);
//await channelDownloaderService.DownloadPlaylists(channelId);

Log.CloseAndFlush();
