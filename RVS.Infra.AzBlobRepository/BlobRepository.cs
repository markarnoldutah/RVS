using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using RVS.Domain.Shared;
using RVS.Infra.AzCredentials;
using Microsoft.Extensions.Configuration;

namespace RVS.Infra.AzBlobRepository;

public class BlobRepository : IBlobRepository
{
    BlobServiceClient _blobServiceClient;
    BlobContainerClient _blobContainerClient;
    int _sasLifeInMinutes;

    public BlobRepository(IConfiguration config, CredentialService credentialService)
    {
        string storageEndPoint = config["BlobClient:EndPoint"];
        string storageContainer = config["BlobClient:Container"];
        string sasLifeInMinutes = config["BlobClient:SASLifeInMinutes"];
        Uri storageEndPointUri = new Uri(storageEndPoint);
        var defaultAzCredential = credentialService.GetDefaultAzCredential();

        _blobServiceClient = new(storageEndPointUri, defaultAzCredential);
        _blobContainerClient = _blobServiceClient.GetBlobContainerClient(storageContainer);
        _sasLifeInMinutes = int.Parse(sasLifeInMinutes);
    }

    //public async Task<List<Exam>> GetSasForExamsAsync(List<Exam> exams)
    //{
    //    UserDelegationKey userDelegationKey = await GetUserDelegationKey();

    //    foreach (Exam exam in exams)
    //    {
    //        BlobClient blobClient = _blobContainerClient.GetBlobClient(exam.BlobName);
    //        Uri SAS = GetSasForBlob(blobClient, userDelegationKey, BlobSasPermissions.Read);
    //        exam.BlobSas = SAS;

    //        blobClient = _blobContainerClient.GetBlobClient(exam.ThumbBlobName);
    //        SAS = GetSasForBlob(blobClient, userDelegationKey, BlobSasPermissions.Read);
    //        exam.ThumbBlobSas = SAS;
    //    }

    //    return exams;
    //}

    public async Task<string> UploadBlobAsync(string blobName)
    {
        throw new NotImplementedException();
    }

    // UTILITIES

    private async Task<UserDelegationKey> GetUserDelegationKey()
    {
        // Get a user delegation key for the Blob service, valid for 5 minutes
        UserDelegationKey userDelegationKey =
            await _blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5));

        return userDelegationKey;
    }

    private Uri GetSasForBlob(
        BlobClient blobClient,
        UserDelegationKey userDelegationKey,
        BlobSasPermissions blobSasPermissions)
    {
        // Create a SAS token for blob
        BlobSasBuilder sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_sasLifeInMinutes)
        };

        // Set permissions to blob
        // sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);
        sasBuilder.SetPermissions(blobSasPermissions);

        // Add SAS token to blob URI
        BlobUriBuilder uriBuilder = new BlobUriBuilder(_blobServiceClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(
                userDelegationKey,
                blobClient
                .GetParentBlobContainerClient()
                .GetParentBlobServiceClient().AccountName)
        };

        return uriBuilder.ToUri();
    }


}
