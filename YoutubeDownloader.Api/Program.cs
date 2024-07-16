using YoutubeDownloader.Api.Endpoints;
using YoutubeDownloader.Api.Logic;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<BackgroundVideoDownloaderService>();
//builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddSingleton<DownloadManager>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapMainEndpoints();

app.Run();