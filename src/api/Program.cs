using Azure.Core;
using Azure.Identity;
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

        // Shared credential for both storage and translation service
        var credential = new DefaultAzureCredential();
        services.AddSingleton<TokenCredential>(credential);

        var storageAccountName = Environment.GetEnvironmentVariable("AzureWebJobsStorage__accountName");
        if (!string.IsNullOrEmpty(storageAccountName))
        {
            var blobUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
            services.AddSingleton(new BlobServiceClient(blobUri, credential));
        }
        else
        {
            // Local development with Azurite
            services.AddSingleton(new BlobServiceClient("UseDevelopmentStorage=true"));
        }

        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddHttpClient<ITranslationService, TranslationService>();
    })
    .Build();

host.Run();
