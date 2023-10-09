using YoutubeDownloader.Logic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHostedService<TimedHostedService>();
var app = builder.Build();

Globals.Settings = new Settings { VideoFolderPath = builder.Configuration["VideoFolderPath"] };

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
