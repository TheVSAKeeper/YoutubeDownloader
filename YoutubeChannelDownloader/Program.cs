// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using YoutubeChannelDownloader;

Console.WriteLine("Hello, World!");


var helper = new Helper();

var basePath = @"E:\bobgroup\projects\youtubeDownloader\downloadChannel";
var chanelId = "UCGNZ41YzeZuLHcEOGt835gA";
var dirPath = Path.Combine(basePath, chanelId);
var dataPath = Path.Combine(dirPath, "data.json");
if (!Directory.Exists(dirPath))
{
    Directory.CreateDirectory(dirPath);
}

if (true)
{
    var videoData = File.ReadAllText(dataPath);
    var videos = JsonSerializer.Deserialize<List<VideoInfo>>(videoData);
    await helper.GetItem(videos.Last(), dirPath);
}
else
{
    var videos = await helper.Download(chanelId); // я
    var videoData = JsonSerializer.Serialize(videos);
    File.WriteAllText(dataPath, videoData);
}
//var fullPath = Path.Combine(basePath, fileName + ".mp4");
// await helper.Download("UCOuW8i824NprPKrM4Pq4R0w"); // боксёр

