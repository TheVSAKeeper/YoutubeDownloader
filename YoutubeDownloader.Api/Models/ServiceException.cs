namespace YoutubeDownloader.Api.Models;

internal class ServiceException(string error) : Exception(error);