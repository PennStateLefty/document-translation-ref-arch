namespace DocumentTranslation.Api.Services;

using DocumentTranslation.Api.Models;

public interface IBlobStorageService
{
    Task UploadFileAsync(string containerName, string blobPath, Stream content, string contentType);
    Task<List<string>> ListBlobsAsync(string containerName, string prefix);
    Task<Stream> DownloadBlobAsync(string containerName, string blobPath);
    Uri GenerateContainerSasUri(string containerName, string prefix, bool readOnly = true);
}
