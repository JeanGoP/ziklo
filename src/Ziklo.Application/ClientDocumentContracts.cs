namespace Ziklo.Application;

public sealed record ClientDocumentInfo(Guid Id, Guid ClientId, string FileName,
    string ContentType, long Length, string Sha256, DateTime CreatedAtUtc,
    Guid UploadedByUserId);

public sealed record ClientDocumentDownload(ClientDocumentInfo Info, byte[] Content);

public interface IClientDocumentService
{
    Task<ClientDocumentInfo> UploadAsync(Guid companyId, Guid clientId,
        string fileName, string contentType, byte[] content, Guid actorUserId,
        CancellationToken ct);
    Task<IReadOnlyList<ClientDocumentInfo>?> ListAsync(Guid companyId, Guid clientId,
        CancellationToken ct);
    Task<ClientDocumentDownload?> GetAsync(Guid companyId, Guid clientId,
        Guid documentId, CancellationToken ct);
}
