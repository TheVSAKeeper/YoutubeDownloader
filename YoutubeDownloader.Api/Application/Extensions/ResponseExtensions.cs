using Microsoft.AspNetCore.Http.HttpResults;

namespace YoutubeDownloader.Api.Application.Extensions;

public static class ResponseExtensions
{
    public static BadRequest<string> ToResponse<T>(this Operation<T, string> operation)
    {
        return TypedResults.BadRequest(operation.Error);
    }
}
