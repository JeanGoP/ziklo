using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class ClientDocumentService(ZikloDbContext db) : IClientDocumentService
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    public async Task<ClientDocumentInfo> UploadAsync(Guid companyId, Guid clientId,
        string fileName, string contentType, byte[] content, Guid actorUserId,
        CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(x => x.CompanyId == companyId && x.Id == clientId, ct))
            throw new LendingRuleException("El cliente no existe en la empresa.");
        if (actorUserId == Guid.Empty || content.Length is < 5 or > MaximumBytes)
            throw new LendingRuleException("El documento debe tener entre 5 bytes y 10 MB.");
        string safeName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (safeName.Length is < 1 or > 255 || safeName != fileName?.Trim() ||
            safeName.Any(char.IsControl))
            throw new LendingRuleException("El nombre del documento no es válido.");
        string mediaType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        string extension = Path.GetExtension(safeName).ToLowerInvariant();
        bool pdf = mediaType == "application/pdf" && extension == ".pdf" &&
            content.AsSpan().StartsWith("%PDF-"u8);
        bool png = mediaType == "image/png" && extension == ".png" &&
            content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = mediaType == "image/jpeg" && extension is ".jpg" or ".jpeg" &&
            content.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
        if (!pdf && !png && !jpeg)
            throw new LendingRuleException("Solo se admiten archivos PDF, PNG o JPEG válidos.");
        var document = new ClientDocument
        {
            CompanyId = companyId, ClientId = clientId, FileName = safeName,
            ContentType = mediaType, Length = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)),
            Content = content, UploadedByUserId = actorUserId
        };
        db.ClientDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return Info(document);
    }

    public async Task<IReadOnlyList<ClientDocumentInfo>?> ListAsync(Guid companyId,
        Guid clientId, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(x => x.CompanyId == companyId && x.Id == clientId, ct))
            return null;
        var records = await db.ClientDocuments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.ClientId, x.FileName, x.ContentType,
                x.Length, x.Sha256, x.CreatedAtUtc, x.UploadedByUserId })
            .ToListAsync(ct);
        return records.Select(x => new ClientDocumentInfo(x.Id, x.ClientId,
            x.FileName, x.ContentType, x.Length, x.Sha256, x.CreatedAtUtc,
            x.UploadedByUserId)).ToList();
    }

    public async Task<ClientDocumentDownload?> GetAsync(Guid companyId,
        Guid clientId, Guid documentId, CancellationToken ct)
    {
        var document = await db.ClientDocuments.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.ClientId == clientId && x.Id == documentId, ct);
        return document is null ? null : new ClientDocumentDownload(Info(document), document.Content);
    }

    private static ClientDocumentInfo Info(ClientDocument document) => new(document.Id,
        document.ClientId, document.FileName, document.ContentType, document.Length,
        document.Sha256, document.CreatedAtUtc, document.UploadedByUserId);
}
