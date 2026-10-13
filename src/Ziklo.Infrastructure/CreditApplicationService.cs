using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class CreditApplicationService(ZikloDbContext db, ILendingService lending) : ICreditApplicationService
{
    public const int MaximumDocumentBytes = 10 * 1024 * 1024;
    public const int MaximumDocuments = 3;

    public async Task<CreditApplicationReview> RespondToInformationAsync(Guid companyId,
        Guid applicationId, Guid actorUserId, string notes, CancellationToken ct)
    {
        var application = await db.CreditApplications.SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == applicationId, ct)
            ?? throw new LendingRuleException("La solicitud no existe.");
        if (application.SubmittedByUserId != actorUserId ||
            application.Status != CreditApplicationStatus.NeedsInformation)
            throw new LendingRuleException("Solo el radicador de esta solicitud puede responder al requerimiento pendiente.");
        string response = notes?.Trim() ?? string.Empty;
        if (response.Length is < 10 or > 2000 || response.Any(ch =>
            char.IsControl(ch) && ch is not ('\r' or '\n' or '\t')))
            throw new LendingRuleException("La respuesta debe tener entre 10 y 2000 caracteres válidos.");
        var review = new CreditApplicationReview
        {
            CompanyId = companyId, ApplicationId = applicationId,
            ActorUserId = actorUserId,
            PreviousStatus = CreditApplicationStatus.NeedsInformation,
            NewStatus = CreditApplicationStatus.InReview,
            Notes = response
        };
        application.Status = CreditApplicationStatus.InReview;
        db.CreditApplicationReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review;
    }

    public async Task<CreditApplicationDocument> AttachSupplementAsync(Guid companyId,
        Guid applicationId, Guid actorUserId, string fileName, byte[] content,
        CancellationToken ct)
    {
        var application = await db.CreditApplications.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == applicationId, ct)
            ?? throw new LendingRuleException("La solicitud no existe.");
        if (application.SubmittedByUserId != actorUserId ||
            application.Status != CreditApplicationStatus.NeedsInformation)
            throw new LendingRuleException("Solo el radicador puede adjuntar información al requerimiento pendiente.");
        if (await db.CreditApplicationDocuments.CountAsync(x => x.CompanyId == companyId &&
            x.ApplicationId == applicationId && x.Kind.StartsWith("Sup-"), ct) >= 10)
            throw new LendingRuleException("Esta solicitud alcanzó el límite de diez soportes adicionales.");
        ValidatePdf(fileName, content);
        var document = new CreditApplicationDocument
        {
            CompanyId = companyId, ApplicationId = applicationId,
            Kind = $"Sup-{Guid.NewGuid():N}", FileName = fileName.Trim(),
            ContentType = "application/pdf", Length = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)), Content = content
        };
        db.CreditApplicationDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return document;
    }

    private static void ValidatePdf(string fileName, byte[] content)
    {
        string safeName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (safeName.Length is < 5 or > 255 || safeName != fileName?.Trim() ||
            safeName.Any(char.IsControl) ||
            !string.Equals(Path.GetExtension(safeName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new LendingRuleException("El soporte debe tener un nombre de archivo PDF válido.");
        if (content.Length is < 5 or > MaximumDocumentBytes ||
            !content.AsSpan().StartsWith("%PDF-"u8))
            throw new LendingRuleException("Adjunta un PDF válido de máximo 10 MB.");
    }

    public async Task<CreditApplicationDocument> AttachSignedContractAsync(Guid companyId,
        Guid applicationId, string fileName, byte[] content, CancellationToken ct)
    {
        var application = await db.CreditApplications.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == applicationId, ct)
            ?? throw new LendingRuleException("La solicitud no existe.");
        if (application.Status != CreditApplicationStatus.Approved || !application.LoanId.HasValue)
            throw new LendingRuleException("El contrato firmado solo puede adjuntarse a un préstamo aprobado.");
        if (await db.CreditApplicationDocuments.AnyAsync(x => x.CompanyId == companyId &&
            x.ApplicationId == applicationId && x.Kind == "SignedContract", ct))
            throw new LendingRuleException("Esta solicitud ya tiene un contrato firmado. No se puede reemplazar.");
        ValidatePdf(fileName, content);
        var document = new CreditApplicationDocument
        {
            CompanyId = companyId, ApplicationId = applicationId,
            Kind = "SignedContract", FileName = fileName.Trim(),
            ContentType = "application/pdf", Length = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)), Content = content
        };
        db.CreditApplicationDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return document;
    }

    public async Task<Loan> ConvertApprovedAsync(Guid companyId, Guid applicationId,
        DateOnly approvalDate, CancellationToken ct)
    {
        var application = await db.CreditApplications.SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == applicationId, ct)
            ?? throw new LendingRuleException("La solicitud no existe.");
        if (application.Status != CreditApplicationStatus.Approved || application.LoanId.HasValue)
            throw new LendingRuleException("Solo una solicitud aprobada y sin préstamo puede convertirse.");
        DateOnly requestedOn = DateOnly.FromDateTime(application.CreatedAtUtc);
        if (approvalDate < requestedOn || approvalDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new LendingRuleException("La fecha de aprobación no es válida.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var client = await db.Clients.SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.IdentificationType == application.IdentificationType &&
            x.IdentificationNumber == application.IdentificationNumber, ct);
        if (client is not null && client.Status != ClientStatus.Active)
            throw new LendingRuleException("El cliente existente está inactivo. Revisa su estado antes de continuar.");
        if (client is not null && !string.Equals(client.Name.Trim(),
            application.ApplicantName.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new LendingRuleException("El documento ya pertenece a un cliente con otro nombre. Verifica la identidad antes de continuar.");
        client ??= await lending.CreateClientAsync(companyId, new CreateClient(
            application.IdentificationType, application.IdentificationNumber,
            application.ApplicantName, null, application.Phone, null, application.Email,
            application.City, null, application.EconomicActivity,
            application.MonthlyIncome, null), ct);
        var loan = await lending.CreateLoanAsync(companyId, new CreateLoan(
            client.Id, application.ProductVersionId, $"CR-{application.Reference}",
            requestedOn, application.RequestedAmount,
            application.RequestedInstallments, false), ct);
        await lending.RequestLoanAsync(companyId, loan.Id, ct);
        await lending.ApproveLoanAsync(companyId, loan.Id,
            new ApproveLoan(approvalDate), ct);
        application.LoanId = loan.Id;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return loan;
    }

    public async Task<CreditApplicationReview> ReviewAsync(Guid companyId,
        Guid applicationId, Guid actorUserId, RecordCreditApplicationReview request,
        CancellationToken ct)
    {
        var application = await db.CreditApplications.SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == applicationId, ct)
            ?? throw new LendingRuleException("La solicitud no existe.");
        if (request.Status is not (CreditApplicationStatus.InReview or
            CreditApplicationStatus.NeedsInformation or CreditApplicationStatus.Approved or
            CreditApplicationStatus.Rejected))
            throw new LendingRuleException("El estado solicitado no corresponde al estudio.");
        bool validTransition = application.Status switch
        {
            CreditApplicationStatus.Submitted => request.Status == CreditApplicationStatus.InReview,
            CreditApplicationStatus.NeedsInformation => request.Status == CreditApplicationStatus.InReview,
            CreditApplicationStatus.InReview => request.Status is
                CreditApplicationStatus.NeedsInformation or CreditApplicationStatus.Approved or
                CreditApplicationStatus.Rejected,
            _ => false
        };
        if (!validTransition)
            throw new LendingRuleException("La solicitud no permite ese cambio de estado.");
        string notes = request.Notes?.Trim() ?? string.Empty;
        if (notes.Length > 2000 || notes.Any(ch => char.IsControl(ch) &&
            ch is not ('\r' or '\n' or '\t')))
            throw new LendingRuleException("Las observaciones no son válidas.");
        if (request.Status is CreditApplicationStatus.NeedsInformation or
            CreditApplicationStatus.Rejected && notes.Length < 10)
            throw new LendingRuleException("Explica el motivo con al menos 10 caracteres.");
        if (request.Status == CreditApplicationStatus.Approved &&
            (request.VerifiedMonthlyIncome is null or <= 0 ||
             request.VerifiedMonthlyObligations is null or < 0))
            throw new LendingRuleException("Verifica ingresos y obligaciones antes de aprobar.");
        if (request.VerifiedMonthlyIncome is < 0 or > 999999999999m ||
            request.VerifiedMonthlyObligations is < 0 or > 999999999999m)
            throw new LendingRuleException("Los valores verificados no son válidos.");
        if (!await db.Users.AnyAsync(x => x.Id == actorUserId && x.IsActive, ct))
            throw new LendingRuleException("El usuario no está activo.");

        var review = new CreditApplicationReview
        {
            CompanyId = companyId, ApplicationId = applicationId,
            ActorUserId = actorUserId, PreviousStatus = application.Status,
            NewStatus = request.Status, Notes = notes,
            VerifiedMonthlyIncome = request.VerifiedMonthlyIncome,
            VerifiedMonthlyObligations = request.VerifiedMonthlyObligations
        };
        application.Status = request.Status;
        db.CreditApplicationReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review;
    }

    public async Task<CreditApplicationReceipt> SubmitAsync(SubmitCreditApplication request,
        CancellationToken ct)
    {
        if (!request.DemoAcknowledged)
            throw new LendingRuleException("Confirma que usarás datos ficticios en las pruebas internas.");
        if (!await db.Companies.AnyAsync(x => x.Id == request.CompanyId && x.IsActive, ct))
            throw new LendingRuleException("La empresa de la solicitud no está disponible.");
        if (request.SubmittedByUserId is Guid submittedBy &&
            (!await db.Users.AnyAsync(x => x.Id == submittedBy && x.IsActive, ct) ||
             !await db.UserCompanies.AnyAsync(x => x.CompanyId == request.CompanyId &&
                 x.UserId == submittedBy && x.IsActive, ct)))
            throw new LendingRuleException("El usuario que radica no está activo en la empresa.");
        var version = await db.ProductVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == request.CompanyId && x.Id == request.ProductVersionId &&
            x.IsPublished && x.ValidFrom <= DateOnly.FromDateTime(DateTime.UtcNow), ct)
            ?? throw new LendingRuleException("El producto seleccionado no está disponible.");
        var product = await db.CreditProducts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == request.CompanyId && x.Id == version.ProductId && x.IsActive, ct)
            ?? throw new LendingRuleException("El producto seleccionado no está activo.");

        static string Field(string? value, string label, int min, int max)
        {
            string text = value?.Trim() ?? string.Empty;
            if (text.Length < min || text.Length > max || text.Any(char.IsControl))
                throw new LendingRuleException($"{label} debe tener entre {min} y {max} caracteres.");
            return text;
        }

        string identificationType = Field(request.IdentificationType, "El tipo de identificación", 2, 30);
        string identificationNumber = Field(request.IdentificationNumber, "La identificación", 5, 60);
        string name = Field(request.ApplicantName, "El nombre", 3, 250);
        string email = Field(request.Email, "El correo electrónico", 5, 250);
        if (!MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email)
            throw new LendingRuleException("El correo electrónico no es válido.");
        string phone = Field(request.Phone, "El teléfono", 7, 60);
        string city = Field(request.City, "La ciudad", 2, 120);
        string activity = Field(request.EconomicActivity, "La actividad económica", 3, 200);
        string purpose = Field(request.Purpose, "El destino del crédito", 10, 1000);
        string formData = request.FormDataJson?.Trim() ?? "{}";
        if (formData.Length > 30000)
            throw new LendingRuleException("El formulario detallado supera el tamaño permitido.");
        try
        {
            using var details = JsonDocument.Parse(formData);
            if (details.RootElement.ValueKind != JsonValueKind.Object ||
                details.RootElement.EnumerateObject().Count() > 120 ||
                details.RootElement.EnumerateObject().Any(field =>
                    field.Name.Length > 80 || field.Value.ValueKind != JsonValueKind.String ||
                    field.Value.GetString()!.Length > 500))
                throw new JsonException();
        }
        catch (JsonException)
        {
            throw new LendingRuleException("El formulario detallado no es válido.");
        }
        if (request.MonthlyIncome <= 0 || request.MonthlyObligations < 0 ||
            request.MonthlyIncome > 999999999999m || request.MonthlyObligations > 999999999999m)
            throw new LendingRuleException("Los ingresos y obligaciones mensuales no son válidos.");
        if (request.RequestedAmount < version.MinimumAmount ||
            request.RequestedAmount > version.MaximumAmount ||
            request.RequestedInstallments < version.MinimumInstallments ||
            request.RequestedInstallments > version.MaximumInstallments ||
            Math.Round(request.RequestedAmount, version.CurrencyDecimals) != request.RequestedAmount)
            throw new LendingRuleException("Monto o plazo fuera de los límites del producto.");
        if (request.Documents.Count > MaximumDocuments ||
            request.Documents.GroupBy(x => x.Kind, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new LendingRuleException("Adjunta como máximo un archivo por tipo de soporte.");

        var application = new CreditApplication
        {
            CompanyId = request.CompanyId,
            Reference = $"SOL-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
            SubmittedByUserId = request.SubmittedByUserId,
            ProductVersionId = version.Id,
            IdentificationType = identificationType,
            IdentificationNumber = identificationNumber,
            ApplicantName = name,
            Email = email,
            Phone = phone,
            City = city,
            EconomicActivity = activity,
            MonthlyIncome = request.MonthlyIncome,
            MonthlyObligations = request.MonthlyObligations,
            RequestedAmount = request.RequestedAmount,
            RequestedInstallments = request.RequestedInstallments,
            Purpose = purpose,
            FormDataJson = formData,
            AcknowledgementVersion = "DEMO-2026-01",
            AcknowledgedAtUtc = DateTime.UtcNow
        };
        var documents = new List<CreditApplicationDocument>();
        foreach (var upload in request.Documents)
        {
            string kind = Field(upload.Kind, "El tipo de soporte", 3, 40);
            if (kind is not ("Identity" or "Income" or "Other"))
                throw new LendingRuleException("El tipo de soporte no es válido.");
            string safeName = Path.GetFileName(upload.FileName?.Trim() ?? string.Empty);
            if (safeName.Length is < 1 or > 255 || safeName != upload.FileName?.Trim() ||
                safeName.Any(char.IsControl))
                throw new LendingRuleException("El nombre del soporte no es válido.");
            if (upload.Content.Length is < 5 or > MaximumDocumentBytes)
                throw new LendingRuleException("Cada soporte debe tener entre 5 bytes y 10 MB.");
            string contentType = upload.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
            string extension = Path.GetExtension(safeName).ToLowerInvariant();
            bool pdf = contentType == "application/pdf" && extension == ".pdf" &&
                upload.Content.AsSpan().StartsWith("%PDF-"u8);
            bool png = contentType == "image/png" && extension == ".png" &&
                upload.Content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            bool jpeg = contentType == "image/jpeg" && extension is ".jpg" or ".jpeg" &&
                upload.Content.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
            if (!pdf && !png && !jpeg)
                throw new LendingRuleException("Solo se admiten PDF, PNG o JPEG válidos.");
            documents.Add(new CreditApplicationDocument
            {
                CompanyId = application.CompanyId,
                ApplicationId = application.Id,
                Kind = kind,
                FileName = safeName,
                ContentType = contentType,
                Length = upload.Content.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(upload.Content)),
                Content = upload.Content
            });
        }
        db.CreditApplications.Add(application);
        db.CreditApplicationDocuments.AddRange(documents);
        await db.SaveChangesAsync(ct);
        return new CreditApplicationReceipt(application.Reference, application.CreatedAtUtc,
            application.Status, product.Name, application.RequestedAmount,
            application.RequestedInstallments, request.Documents.Count);
    }
}
