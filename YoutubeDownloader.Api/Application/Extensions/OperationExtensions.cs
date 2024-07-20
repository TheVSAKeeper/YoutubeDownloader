namespace YoutubeDownloader.Api.Application.Extensions;

public static class OperationExtensions
{
    public static IEnumerable<T> GetSuccessfulResults<T>(this IEnumerable<Operation<T>> items)
    {
        return items.Where(operation => operation.Ok)
            .Select(operation => operation.Result);
    }
}