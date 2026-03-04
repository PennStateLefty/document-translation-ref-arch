using System.Net;
using DocumentTranslation.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Functions;

public class LanguagesHttpTrigger
{
    private readonly ITranslationService _translationService;
    private readonly ILogger<LanguagesHttpTrigger> _logger;

    public LanguagesHttpTrigger(ITranslationService translationService, ILogger<LanguagesHttpTrigger> logger)
    {
        _translationService = translationService;
        _logger = logger;
    }

    [Function("GetLanguages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "languages")] HttpRequestData req)
    {
        _logger.LogInformation("Getting supported languages");

        try
        {
            var languages = await _translationService.GetSupportedLanguagesAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { languages });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get supported languages");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = "Failed to load supported languages. Please try again later." });
            return response;
        }
    }
}
