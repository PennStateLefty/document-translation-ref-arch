using Azure.Storage.Blobs;
using DocumentTranslation.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage") 
            ?? "UseDevelopmentStorage=true";
        
        services.AddSingleton(new BlobServiceClient(storageConnection));
        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<ITranslationService, TranslationService>();
    })
    .Build();

host.Run();
