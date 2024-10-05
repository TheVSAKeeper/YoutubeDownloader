using YoutubeExplode.Playlists;

namespace YoutubeChannelDownloader.Models;

public record PlaylistInfo(PlaylistId Id, string Title, string Description, string? ThumbnailUrl);
