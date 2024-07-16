using NLog.Web;
using YoutubeDownloader.Logic;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHostedService<BackgroundVideoDownloaderService>();
//builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddSingleton<DownloadManager>();
builder.Host.UseNLog();
WebApplication app = builder.Build();

Globals.Settings = new Settings
{
    VideoFolderPath = builder.Configuration["VideoFolderPath"],
    TelegramBotTokenPath = builder.Configuration["TelegramBotTokenPath"]
};

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();