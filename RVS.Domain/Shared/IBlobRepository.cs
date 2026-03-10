namespace RVS.Domain.Shared;

public interface IBlobRepository
{
    //Task<List<Exam>> GetSasForExamsAsync(List<Exam> exams);

    Task<string> UploadBlobAsync(string blobName);
}