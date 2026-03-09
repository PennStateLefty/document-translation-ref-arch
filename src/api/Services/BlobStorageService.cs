using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DocumentTranslation.Api.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task UploadFileAsync(string containerName, string blobPath, Stream content, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });
    }

    public async Task<List<string>> ListBlobsAsync(string containerName, string prefix)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobs = new List<string>();
        await foreach (var blobItem in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
        {
            blobs.Add(blobItem.Name);
        }
        return blobs;
    }

    public async Task<Stream> DownloadBlobAsync(string containerName, string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public Uri GenerateContainerSasUri(string containerName, string prefix, bool readOnly = true)
    {
        // With shared key access disabled, return the plain container URI.
        // The Translator service authenticates via its own managed identity + RBAC.
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        return containerClient.Uri;
    }
}
