﻿using CliWrap.Builders;

namespace YoutubeDownloader.Api.Services;

public class FFmpegConverter(FFmpeg ffmpeg)
{
    public ValueTask ProcessAsync(
        string filePath,
        IEnumerable<string> streamPaths,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentsBuilder arguments = new();

        foreach (string path in streamPaths)
        {
            arguments.Add("-i").Add(path);
        }

        arguments.Add("-c")
            .Add("copy")
            .Add(filePath);

        arguments.Add("-loglevel")
            .Add("info")
            .Add("-stats");

        arguments.Add("-hide_banner")
            .Add("-threads")
            .Add(Environment.ProcessorCount)
            .Add("-nostdin")
            .Add("-y");

        return ffmpeg.ExecuteAsync(arguments.Build(), progress, cancellationToken);
    }
}