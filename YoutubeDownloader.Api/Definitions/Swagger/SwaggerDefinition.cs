using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YoutubeDownloader.Api.Application;

namespace YoutubeDownloader.Api.Definitions.Swagger;

public class SwaggerDefinition : AppDefinition
{
    private const string SwaggerConfig = "/swagger/v1/swagger.json";

    public override void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ApiBehaviorOptions>(options => { options.SuppressModelStateInvalidFilter = true; });
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = AppData.ServiceName,
                Version = AppData.AppVersion
            });

            options.ResolveConflictingActions(descriptions => descriptions.First());
        });
    }

    public override void ConfigureApplication(WebApplication app)
    {
        if (app.Environment.IsDevelopment() == false)
        {
            return;
        }

        app.UseSwagger();

        app.UseSwaggerUI(settings =>
        {
            settings.SwaggerEndpoint(SwaggerConfig, $"{AppData.ServiceName} v.{AppData.AppVersion}");

            settings.DocumentTitle = $"{AppData.ServiceName}";
            settings.DefaultModelExpandDepth(0);
            settings.DefaultModelRendering(ModelRendering.Model);
            settings.DefaultModelsExpandDepth(0);
            settings.DocExpansion(DocExpansion.None);
            settings.DisplayRequestDuration();
        });
    }
}
