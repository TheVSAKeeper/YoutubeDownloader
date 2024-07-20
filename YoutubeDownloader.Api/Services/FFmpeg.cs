using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Exceptions;
using Microsoft.Extensions.Options;
using YoutubeDownloader.Api.Application;
using YoutubeDownloader.Api.Configurations;

namespace YoutubeDownloader.Api.Services;

public partial class FFmpeg(IOptions<FFmpegOptions> options)
{
    public async ValueTask ExecuteAsync(
        string arguments,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default
    )
    {
        StringBuilder stdErrBuffer = new();

        PipeTarget stdErrPipe = PipeTarget.Merge(PipeTarget.ToStringBuilder(stdErrBuffer),
            progress?.Pipe(CreateProgressRouter) ?? PipeTarget.Null);

        try
        {
            Command command = Cli.Wrap(options.Value.Path)
                .WithArguments(arguments)
                .WithStandardErrorPipe(stdErrPipe);

            await command.ExecuteAsync(cancellationToken);
        }
        catch (CommandExecutionException exception)
        {
            string message = $"""
                              FFmpeg command-line tool failed with an error.

                              Standard error:
                              {stdErrBuffer}
                              """;

            throw new InvalidOperationException(message, exception);
        }
    }

    private static PipeTarget CreateProgressRouter(IProgress<double> progress)
    {
        return PipeTarget.ToDelegate(line =>
        {
            TimeSpan? totalDuration = GetTotalDuration(line);

            if (totalDuration is null || totalDuration == TimeSpan.Zero)
            {
                return;
            }

            TimeSpan? processedDuration = GetProcessedDuration(line);

            if (processedDuration is null || totalDuration == TimeSpan.Zero)
            {
                return;
            }

            progress.Report((
                processedDuration.Value.TotalMilliseconds / totalDuration.Value.TotalMilliseconds
            ).Clamp(0, 1));
        });
    }

    private static TimeSpan? GetTotalDuration(string line)
    {
        TimeSpan? totalDuration = default;

        Match totalDurationMatch = TotalDurationRegex().Match(line);

        if (totalDurationMatch.Success == false)
        {
            return totalDuration;
        }

        int hours = int.Parse(totalDurationMatch.Groups[1].Value,
            CultureInfo.InvariantCulture);

        int minutes = int.Parse(totalDurationMatch.Groups[2].Value,
            CultureInfo.InvariantCulture);

        double seconds = double.Parse(totalDurationMatch.Groups[3].Value,
            CultureInfo.InvariantCulture);

        totalDuration = TimeSpan.FromHours(hours)
                        + TimeSpan.FromMinutes(minutes)
                        + TimeSpan.FromSeconds(seconds);

        return totalDuration;
    }

    private static TimeSpan? GetProcessedDuration(string line)
    {
        TimeSpan? processedDuration = default;

        Match processedDurationMatch = ProcessedDurationRegex().Match(line);

        if (processedDurationMatch.Success == false)
        {
            return processedDuration;
        }

        int hours = int.Parse(processedDurationMatch.Groups[1].Value,
            CultureInfo.InvariantCulture);

        int minutes = int.Parse(processedDurationMatch.Groups[2].Value,
            CultureInfo.InvariantCulture);

        double seconds = double.Parse(processedDurationMatch.Groups[3].Value,
            CultureInfo.InvariantCulture);

        processedDuration = TimeSpan.FromHours(hours)
                            + TimeSpan.FromMinutes(minutes)
                            + TimeSpan.FromSeconds(seconds);

        return processedDuration;
    }

    [GeneratedRegex(@"Duration:\s(\d+):(\d+):(\d+\.\d+)")]
    private static partial Regex TotalDurationRegex();

    [GeneratedRegex(@"time=(\d+):(\d+):(\d+\.\d+)")]
    private static partial Regex ProcessedDurationRegex();
}