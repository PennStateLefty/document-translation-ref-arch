using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;
using Azure.AI.Translation.Document;
using Azure.Core;
using Azure.Core.Diagnostics;
using Azure.Identity;
using Azure.Storage.Blobs;
using DocumentTranslation.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Capture Azure SDK logs and route through ILogger, escalating 4xx/5xx to Error level.
// Must be created before any SDK clients so the listener is active for their lifetime.
using var azureSdkListener = new AzureEventSourceListener((args, message) =>
{
    // ILoggerFactory isn't available yet at this point, so write to console.
    // Once the host is built we switch to ILogger below.
    var isHttpError = Regex.IsMatch(message, @"Status:\s*[45]\d{2}");
    if (isHttpError || args.Level <= EventLevel.Error)
    {
        Console.Error.WriteLine("[Azure SDK ERROR] {0}", message);
    }
    else if (args.Level <= EventLevel.Warning)
    {
        Console.WriteLine("[Azure SDK WARN] {0}", message);
    }
    else
    {
        Console.WriteLine("[Azure SDK] {0}", message);
    }
}, EventLevel.Verbose);

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

        // Document Translation SDK client (managed identity auth)
        var aiServicesEndpoint = Environment.GetEnvironmentVariable("AI_SERVICES_ENDPOINT");
        if (!string.IsNullOrEmpty(aiServicesEndpoint))
        {
            services.AddSingleton(new DocumentTranslationClient(
                new Uri(aiServicesEndpoint), credential));
        }

        services.AddHttpClient<ITranslationService, TranslationService>();
    })
    .Build();

host.Run();
