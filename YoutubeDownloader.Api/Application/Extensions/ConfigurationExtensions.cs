namespace YoutubeDownloader.Api.Application.Extensions;

public static class ConfigurationExtensions
{
    public static WebApplicationBuilder AddConfiguration<T>(this WebApplicationBuilder builder) where T : class
    {
        builder.Services.Configure<T>(builder.Configuration.GetSection(typeof(T).Name));
        return builder;
    }
}