using System.Security.Claims;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Net.Mail;
using System.Data;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.Infrastructure;

internal static class LendingEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}")
            .RequireAuthorization()
            .WithTags("Operación crediticia")
            .AddEndpointFilter(async (context, next) =>
            {
                try
                {
                    var http = context.HttpContext;
                    if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId) ||
                        !Guid.TryParse(http.Request.RouteValues["companyId"]?.ToString(), out Guid companyId))
                        return Results.Forbid();
                    var db = http.RequestServices.GetRequiredService<ZikloDbContext>();
                    db.AuditActorUserId = userId;
                    db.AuditIpAddress = http.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                    var membership = await (
                        from entry in db.UserCompanies.AsNoTracking()
                        join company in db.Companies.AsNoTracking()
                            on entry.CompanyId equals company.Id
                        join user in db.Users.AsNoTracking()
                            on entry.UserId equals user.Id
                        where entry.CompanyId == companyId && entry.UserId == userId &&
                            entry.IsActive && company.IsActive && user.IsActive
                        select entry).SingleOrDefaultAsync();
                    if (membership is null) return Results.Forbid();
                    if (membership.ProfileId is Guid profileId)
                    {
                        var profile = await db.AccessProfiles.AsNoTracking()
                            .SingleOrDefaultAsync(x => x.Id == profileId &&
                                x.CompanyId == companyId && x.IsActive);
                        if (profile is null) return Results.Forbid();
                        http.Items["profilePermissions"] = profile.PermissionMask;
                    }
                    else
                        http.Items["companyPermissions"] = await db.CompanyRolePermissions.AsNoTracking()
                            .Where(x => x.CompanyId == companyId && x.Role == membership.Role)
                            .ToDictionaryAsync(x => x.Permission, x => x.Allowed);
                    http.Items["companyRole"] = membership.Role;
                    return await next(context);
                }
                catch (LendingRuleException error)
                {
                    return Results.Problem(error.Message, statusCode: 400, title: "Regla de negocio");
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Results.Problem("El registro cambió durante la operación. Vuelve a consultar antes de intentar de nuevo.",
                        statusCode: 409, title: "Conflicto de concurrencia");
                }
                catch (DbUpdateException)
                {
                    return Results.Problem("La operación entra en conflicto con una restricción de datos.",
                        statusCode: 409, title: "Conflicto de datos");
                }
            });

        group.MapPost("/clients", async (Guid companyId, CreateClient command,
            ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManageClients))
                return Results.Forbid();
            var client = await service.CreateClientAsync(companyId, command, ct);
            return Results.Created($"/api/companies/{companyId}/clients/{client.Id}",
                new { client.Id, client.Name, client.IdentificationType, client.IdentificationNumber });
        });

        group.MapGet("/clients", async (Guid companyId, string? search,
            int? skip, int? take, ZikloDbContext db, HttpContext http,
            CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            int offset = Math.Max(0, skip ?? 0);
            int limit = Math.Clamp(take ?? 25, 1, 100);
            var query = db.Clients.AsNoTracking().Where(x => x.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(x => x.Name.Contains(term) ||
                    x.IdentificationNumber.Contains(term));
            }
            int total = await query.CountAsync(ct);
            var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Skip(offset).Take(limit)
                .Select(x => new { x.Id, x.Name, x.IdentificationType,
                    x.IdentificationNumber, x.Phone, x.City, x.Status })
                .ToListAsync(ct);
            return Results.Ok(new { total, skip = offset, take = limit, items });
        });

        group.MapGet("/clients/{clientId:guid}", async (Guid companyId, Guid clientId,
            ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            var client = await service.GetClientAsync(companyId, clientId, ct);
            return client is null ? Results.NotFound() : Results.Ok(new
            {
                client.Id, client.IdentificationType, client.IdentificationNumber,
                client.Name, client.Address, client.Phone, client.WhatsApp,
                client.Email, client.City, client.BirthDate, client.EconomicActivity,
                client.MonthlyIncome, client.Employer, client.Status
            });
        });

        group.MapGet("/clients/{clientId:guid}/summary", async (Guid companyId,
            Guid clientId, DateOnly asOf, IPortfolioService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            var summary = await service.GetClientAsync(companyId, clientId, asOf, ct);
            return summary is null ? Results.NotFound() : Results.Ok(summary);
        });

        group.MapPost("/clients/{clientId:guid}/references", async (Guid companyId,
            Guid clientId, CreateClientReference command, ILendingService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManageClients)) return Results.Forbid();
            var reference = await service.CreateClientReferenceAsync(companyId, clientId, command, ct);
            return Results.Created($"/api/companies/{companyId}/clients/{clientId}/references/{reference.Id}",
                new { reference.Id, reference.ClientId, reference.Kind, reference.Name,
                    reference.Relationship, reference.Phone, reference.Email, reference.Notes });
        });

        group.MapGet("/clients/{clientId:guid}/references", async (Guid companyId,
            Guid clientId, ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            if (!await db.Clients.AnyAsync(x => x.CompanyId == companyId && x.Id == clientId, ct))
                return Results.NotFound();
            var references = await db.ClientReferences.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.ClientId == clientId)
                .OrderBy(x => x.Kind).ThenBy(x => x.Name)
                .Select(x => new { x.Id, x.Kind, x.Name, x.Relationship,
                    x.Phone, x.Email, x.Notes, x.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(references);
        });

        group.MapGet("/clients/{clientId:guid}/references/{referenceId:guid}", async (
            Guid companyId, Guid clientId, Guid referenceId, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            var reference = await db.ClientReferences.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.ClientId == clientId && x.Id == referenceId)
                .Select(x => new { x.Id, x.ClientId, x.Kind, x.Name,
                    x.Relationship, x.Phone, x.Email, x.Notes, x.CreatedAtUtc })
                .SingleOrDefaultAsync(ct);
            return reference is null ? Results.NotFound() : Results.Ok(reference);
        });

        group.MapPost("/clients/{clientId:guid}/documents", async (Guid companyId,
            Guid clientId, string fileName, HttpContext http,
            IClientDocumentService service, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManageClients)) return Results.Forbid();
            if (http.Request.ContentLength > ClientDocumentService.MaximumBytes)
                return Results.Problem("El documento supera 10 MB.", statusCode: 413);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > ClientDocumentService.MaximumBytes)
                    return Results.Problem("El documento supera 10 MB.", statusCode: 413);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var document = await service.UploadAsync(companyId, clientId, fileName,
                http.Request.ContentType ?? string.Empty, buffer.ToArray(), actorUserId, ct);
            return Results.Created(
                $"/api/companies/{companyId}/clients/{clientId}/documents/{document.Id}", document);
        });

        group.MapGet("/clients/{clientId:guid}/documents", async (Guid companyId,
            Guid clientId, IClientDocumentService service, HttpContext http,
            CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            var documents = await service.ListAsync(companyId, clientId, ct);
            return documents is null ? Results.NotFound() : Results.Ok(documents);
        });

        group.MapGet("/clients/{clientId:guid}/documents/{documentId:guid}", async (
            Guid companyId, Guid clientId, Guid documentId,
            IClientDocumentService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewClients)) return Results.Forbid();
            var document = await service.GetAsync(companyId, clientId, documentId, ct);
            return document is null ? Results.NotFound() : Results.File(document.Content,
                "application/octet-stream", fileDownloadName: document.Info.FileName);
        });

        group.MapPost("/products", async (Guid companyId, CreateProduct command,
            ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManageProducts)) return Results.Forbid();
            var product = await service.CreateProductAsync(companyId, command, ct);
            return Results.Created($"/api/companies/{companyId}/products/{product.Id}",
                new { product.Id, product.Code, product.Name });
        });

        group.MapGet("/products", async (Guid companyId, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans) &&
                !HasPermission(http, BusinessPermission.ManageProducts)) return Results.Forbid();
            var products = await db.CreditProducts.AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Code)
                .Select(x => new { x.Id, x.Code, x.Name, x.Description, x.IsActive })
                .ToListAsync(ct);
            return Results.Ok(products);
        });

        group.MapPut("/products/{productId:guid}", async (Guid companyId,
            Guid productId, UpdateProduct command, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManageProducts)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 200 ||
                command.Description?.Trim().Length > 1000)
                return Results.Problem("Revisa el nombre y la descripción del producto.", statusCode: 400);
            var product = await db.CreditProducts.SingleOrDefaultAsync(x =>
                x.CompanyId == companyId && x.Id == productId, ct);
            if (product is null) return Results.NotFound();
            product.Name = command.Name.Trim();
            product.Description = command.Description?.Trim();
            product.IsActive = command.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { product.Id, product.Code, product.Name,
                product.Description, product.IsActive });
        });

        group.MapGet("/products/{productId:guid}/versions", async (Guid companyId,
            Guid productId, ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans) &&
                !HasPermission(http, BusinessPermission.ManageProducts)) return Results.Forbid();
            if (!await db.CreditProducts.AnyAsync(x => x.CompanyId == companyId &&
                x.Id == productId, ct)) return Results.NotFound();
            var versions = await db.ProductVersions.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.ProductId == productId)
                .OrderByDescending(x => x.Number)
                .ToListAsync(ct);
            return Results.Ok(versions);
        });

        group.MapPost("/products/{productId:guid}/versions", async (Guid companyId, Guid productId,
            CreateProductVersion command, ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManageProducts)) return Results.Forbid();
            var version = await service.CreateProductVersionAsync(companyId, productId, command, ct);
            return Results.Created($"/api/companies/{companyId}/products/{productId}/versions/{version.Id}",
                new { version.Id, version.ProductId, version.Number, version.ValidFrom });
        });

        group.MapPost("/credit-applications/submit", async (Guid companyId,
            HttpContext http, ICreditApplicationService service, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.CreateLoan)) return Results.Forbid();
            if (!http.Request.HasFormContentType)
                return Results.Problem("Envía el formulario con multipart/form-data.", statusCode: 415);
            if (http.Request.ContentLength > 31 * 1024 * 1024)
                return Results.Problem("La solicitud supera el tamaño permitido.", statusCode: 413);
            try
            {
                var form = await http.Request.ReadFormAsync(ct);
                string Value(string key) => form[key].ToString();
                if (!Guid.TryParse(Value("productVersionId"), out Guid versionId) ||
                    !decimal.TryParse(Value("monthlyIncome"), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out decimal income) ||
                    !decimal.TryParse(Value("monthlyObligations"), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out decimal obligations) ||
                    !decimal.TryParse(Value("requestedAmount"), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out decimal amount) ||
                    !int.TryParse(Value("requestedInstallments"), out int installments))
                    return Results.Problem("Revisa los valores numéricos de la solicitud.", statusCode: 400);
                if (form.Files.Count > CreditApplicationService.MaximumDocuments)
                    return Results.Problem("Adjunta máximo tres soportes.", statusCode: 400);
                var uploads = new List<CreditApplicationUpload>();
                foreach (var file in form.Files)
                {
                    if (file.Length > CreditApplicationService.MaximumDocumentBytes)
                        return Results.Problem("Cada soporte puede tener máximo 10 MB.", statusCode: 413);
                    using var buffer = new MemoryStream();
                    await file.CopyToAsync(buffer, ct);
                    uploads.Add(new CreditApplicationUpload(file.Name, file.FileName,
                        file.ContentType, buffer.ToArray()));
                }
                Guid userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var request = new SubmitCreditApplication(companyId, versionId,
                    Value("identificationType"), Value("identificationNumber"),
                    Value("applicantName"), Value("email"), Value("phone"),
                    Value("city"), Value("economicActivity"), income, obligations,
                    amount, installments, Value("purpose"), Value("formDataJson"),
                    Value("demoAcknowledged") == "true", uploads, userId);
                var receipt = await service.SubmitAsync(request, ct);
                return Results.Created("/solicitar.html", receipt);
            }
            catch (LendingRuleException error)
            {
                return Results.Problem(error.Message, statusCode: 400, title: "Solicitud no válida");
            }
            catch (InvalidDataException)
            {
                return Results.Problem("El formulario supera el tamaño permitido.", statusCode: 413);
            }
            catch (DbUpdateException)
            {
                return Results.Problem("No se pudo radicar la solicitud. Intenta nuevamente.", statusCode: 409);
            }
        }).RequireRateLimiting("credit-application");

        group.MapGet("/credit-applications/by-reference/{reference}/print", async (
            Guid companyId, string reference, ZikloDbContext db, HttpContext http,
            CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var application = await db.CreditApplications.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Reference == reference, ct);
            if (application is null) return Results.NotFound();
            var company = await db.Companies.AsNoTracking()
                .SingleAsync(x => x.Id == companyId, ct);
            return Results.Ok(new { application.Reference, application.CreatedAtUtc,
                application.IdentificationType, application.IdentificationNumber,
                application.ApplicantName, application.Email, application.Phone,
                application.City, application.EconomicActivity, application.MonthlyIncome,
                application.MonthlyObligations, application.RequestedAmount,
                application.RequestedInstallments, application.Purpose,
                application.FormDataJson, company.Name, company.TaxIdentifier });
        });

        group.MapGet("/credit-applications", async (Guid companyId, string? search,
            CreditApplicationStatus? status, int? skip, int? take,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            int offset = Math.Max(0, skip ?? 0);
            int limit = Math.Clamp(take ?? 25, 1, 100);
            var query = db.CreditApplications.AsNoTracking()
                .Where(x => x.CompanyId == companyId);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                if (term.Length > 100) return Results.Problem("La búsqueda es demasiado larga.", statusCode: 400);
                query = query.Where(x => x.Reference.Contains(term) ||
                    x.ApplicantName.Contains(term) || x.IdentificationNumber.Contains(term));
            }
            int total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id).Skip(offset).Take(limit)
                .Select(x => new { x.Id, x.Reference, x.ApplicantName,
                    x.IdentificationType, x.IdentificationNumber,
                    x.RequestedAmount, x.RequestedInstallments,
                    x.Status, x.CreatedAtUtc }).ToListAsync(ct);
            return Results.Ok(new { total, skip = offset, take = limit, items });
        });

        group.MapGet("/my-application-alerts", async (Guid companyId,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
                return Results.Unauthorized();
            var pending = db.CreditApplications.AsNoTracking().Where(x =>
                x.CompanyId == companyId && x.SubmittedByUserId == userId &&
                x.Status == CreditApplicationStatus.NeedsInformation);
            int total = await pending.CountAsync(ct);
            var items = await pending.OrderByDescending(x => x.CreatedAtUtc).Take(30)
                .Select(x => new
                {
                    applicationId = x.Id, x.Reference, x.ApplicantName,
                    note = db.CreditApplicationReviews.AsNoTracking()
                        .Where(review => review.CompanyId == companyId &&
                            review.ApplicationId == x.Id &&
                            review.NewStatus == CreditApplicationStatus.NeedsInformation)
                        .OrderByDescending(review => review.RecordedAtUtc)
                        .Select(review => review.Notes).FirstOrDefault(),
                    requestedAtUtc = db.CreditApplicationReviews.AsNoTracking()
                        .Where(review => review.CompanyId == companyId &&
                            review.ApplicationId == x.Id &&
                            review.NewStatus == CreditApplicationStatus.NeedsInformation)
                        .OrderByDescending(review => review.RecordedAtUtc)
                        .Select(review => (DateTime?)review.RecordedAtUtc).FirstOrDefault()
                }).ToListAsync(ct);
            return Results.Ok(new { total, items });
        });

        group.MapGet("/credit-applications/{applicationId:guid}", async (
            Guid companyId, Guid applicationId, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var application = await db.CreditApplications.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == applicationId, ct);
            if (application is null) return Results.NotFound();
            var documents = await db.CreditApplicationDocuments.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.ApplicationId == applicationId)
                .OrderBy(x => x.Kind)
                .Select(x => new { x.Id, x.Kind, x.FileName, x.ContentType,
                    x.Length, x.Sha256, x.CreatedAtUtc }).ToListAsync(ct);
            var reviews = await db.CreditApplicationReviews.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.ApplicationId == applicationId)
                .Join(db.Users.AsNoTracking(), review => review.ActorUserId,
                    user => user.Id, (review, user) => new { review, user.Email })
                .OrderBy(x => x.review.RecordedAtUtc).ThenBy(x => x.review.Id)
                .Select(x => new { x.review.Id, x.review.PreviousStatus,
                    x.review.NewStatus, x.review.Notes,
                    x.review.VerifiedMonthlyIncome, x.review.VerifiedMonthlyObligations,
                    x.review.ActorUserId, actorEmail = x.Email,
                    x.review.RecordedAtUtc }).ToListAsync(ct);
            return Results.Ok(new { application, documents, reviews });
        });

        group.MapPost("/credit-applications/{applicationId:guid}/reviews", async (
            Guid companyId, Guid applicationId, RecordCreditApplicationReview command,
            ICreditApplicationService service, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            var permission = command.Status is CreditApplicationStatus.Approved or
                CreditApplicationStatus.Rejected
                ? BusinessPermission.ApproveLoan : BusinessPermission.StudyApplications;
            if (!HasPermission(http, permission)) return Results.Forbid();
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
                return Results.Unauthorized();
            if ((command.Status is
                CreditApplicationStatus.Approved or CreditApplicationStatus.Rejected) &&
                await db.CreditApplicationReviews.AnyAsync(x =>
                    x.CompanyId == companyId && x.ApplicationId == applicationId &&
                    x.ActorUserId == userId, ct))
                return Results.Problem("La decisión debe tomarla otra persona distinta de quien estudió la solicitud.",
                    statusCode: 409);
            var review = await service.ReviewAsync(companyId, applicationId, userId,
                command, ct);
            return Results.Ok(new { review.Id, review.PreviousStatus,
                review.NewStatus, review.RecordedAtUtc });
        });

        group.MapPost("/credit-applications/{applicationId:guid}/response", async (
            Guid companyId, Guid applicationId, RespondToInformation command,
            ICreditApplicationService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
                return Results.Unauthorized();
            var review = await service.RespondToInformationAsync(companyId,
                applicationId, userId, command.Notes, ct);
            return Results.Ok(new { review.Id, review.PreviousStatus,
                review.NewStatus, review.RecordedAtUtc });
        });

        group.MapPost("/credit-applications/{applicationId:guid}/supplements", async (
            Guid companyId, Guid applicationId, string fileName,
            ICreditApplicationService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
                return Results.Unauthorized();
            if (!string.Equals(http.Request.ContentType, "application/pdf",
                StringComparison.OrdinalIgnoreCase))
                return Results.Problem("Adjunta un archivo PDF.", statusCode: 400);
            if (http.Request.ContentLength > CreditApplicationService.MaximumDocumentBytes)
                return Results.Problem("El soporte supera 10 MB.", statusCode: 413);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > CreditApplicationService.MaximumDocumentBytes)
                    return Results.Problem("El soporte supera 10 MB.", statusCode: 413);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            var document = await service.AttachSupplementAsync(companyId,
                applicationId, userId, fileName, buffer.ToArray(), ct);
            return Results.Created(
                $"/api/companies/{companyId}/credit-applications/{applicationId}/documents/{document.Id}",
                new { document.Id, document.Kind, document.FileName,
                    document.Length, document.Sha256, document.CreatedAtUtc });
        });

        group.MapPost("/credit-applications/{applicationId:guid}/loan", async (
            Guid companyId, Guid applicationId, DateOnly approvalDate,
            ICreditApplicationService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.CreateLoan)) return Results.Forbid();
            var loan = await service.ConvertApprovedAsync(companyId, applicationId,
                approvalDate, ct);
            return Results.Created($"/api/companies/{companyId}/loans/{loan.Id}",
                LoanView(loan));
        });

        group.MapPost("/credit-applications/{applicationId:guid}/signed-contract", async (
            Guid companyId, Guid applicationId, string fileName,
            ICreditApplicationService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.DisburseLoan)) return Results.Forbid();
            if (!string.Equals(http.Request.ContentType, "application/pdf",
                StringComparison.OrdinalIgnoreCase))
                return Results.Problem("Adjunta un archivo PDF.", statusCode: 400);
            if (http.Request.ContentLength > CreditApplicationService.MaximumDocumentBytes)
                return Results.Problem("El contrato supera 10 MB.", statusCode: 413);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > CreditApplicationService.MaximumDocumentBytes)
                    return Results.Problem("El contrato supera 10 MB.", statusCode: 413);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            var document = await service.AttachSignedContractAsync(companyId,
                applicationId, fileName, buffer.ToArray(), ct);
            return Results.Created(
                $"/api/companies/{companyId}/credit-applications/{applicationId}/documents/{document.Id}",
                new { document.Id, document.Kind, document.FileName,
                    document.Length, document.Sha256, document.CreatedAtUtc });
        });

        group.MapGet("/credit-applications/{applicationId:guid}/documents/{documentId:guid}",
            async (Guid companyId, Guid applicationId, Guid documentId,
                ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var document = await db.CreditApplicationDocuments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CompanyId == companyId &&
                    x.ApplicationId == applicationId && x.Id == documentId, ct);
            return document is null ? Results.NotFound() : Results.File(document.Content,
                "application/octet-stream", fileDownloadName: document.FileName);
        });

        group.MapPost("/loans", async (Guid companyId, CreateLoan command,
            ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.CreateLoan))
                return Results.Forbid();
            var loan = await service.CreateLoanAsync(companyId, command, ct);
            return Results.Created($"/api/companies/{companyId}/loans/{loan.Id}", LoanView(loan));
        });

        group.MapGet("/loans", async (Guid companyId, Guid? clientId,
            LoanStatus? status, string? search, int? skip, int? take,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            int offset = Math.Max(0, skip ?? 0);
            int limit = Math.Clamp(take ?? 25, 1, 100);
            var query = db.Loans.AsNoTracking().Where(x => x.CompanyId == companyId);
            if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                string nameTerm = term.ToUpperInvariant();
                query = query.Where(x => x.Number.Contains(term) ||
                    db.Clients.Any(client => client.CompanyId == companyId &&
                        client.Id == x.ClientId && client.Name.ToUpper().Contains(nameTerm)));
            }
            int total = await query.CountAsync(ct);
            var loans = await query.OrderByDescending(x => x.RequestDate)
                .ThenBy(x => x.Number).Skip(offset).Take(limit)
                .Select(x => new
                {
                    Loan = x,
                    ClientName = db.Clients.Where(client => client.CompanyId == companyId &&
                        client.Id == x.ClientId).Select(client => client.Name).FirstOrDefault()
                }).ToListAsync(ct);
            return Results.Ok(new { total, skip = offset, take = limit,
                items = loans.Select(x => LoanView(x.Loan, x.ClientName)).ToList() });
        });

        group.MapGet("/loans/{loanId:guid}", async (Guid companyId, Guid loanId,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var loan = await db.Loans.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CompanyId == companyId && x.Id == loanId, ct);
            if (loan is null) return Results.NotFound();
            var clientName = await db.Clients.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.Id == loan.ClientId)
                .Select(x => x.Name).SingleOrDefaultAsync(ct);
            return Results.Ok(LoanView(loan, clientName));
        });

        group.MapGet("/loans/{loanId:guid}/schedule", async (Guid companyId, Guid loanId,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var plan = await db.LoanPlans.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CompanyId == companyId && x.LoanId == loanId && x.IsCurrent, ct);
            if (plan is null) return Results.NotFound();
            var installments = await db.LoanInstallments.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loanId && x.PlanId == plan.Id)
                .OrderBy(x => x.Number)
                .Select(x => new
                {
                    x.Id, x.Number, x.DueDate, x.OpeningPrincipal, x.Principal, x.Interest,
                    x.CapitalizedInterest, x.Insurance, x.OtherCharges, x.Payment,
                    x.ClosingPrincipal
                }).ToListAsync(ct);
            var paid = await db.PaymentAllocations.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                    x.Component != BalanceComponent.LateInterest)
                .GroupBy(x => x.InstallmentId)
                .Select(group => new { InstallmentId = group.Key, Amount = group.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.InstallmentId, x => x.Amount, ct);
            var late = await db.LoanMovements.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                    x.Component == BalanceComponent.LateInterest && x.InstallmentId != null)
                .GroupBy(x => x.InstallmentId!.Value)
                .Select(group => new { InstallmentId = group.Key, Amount = group.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.InstallmentId, x => x.Amount, ct);
            return Results.Ok(new { plan.Id, plan.Number, plan.Reason,
                installments = installments.Select(x => new
                {
                    x.Id, x.Number, x.DueDate, x.OpeningPrincipal, x.Principal, x.Interest,
                    x.CapitalizedInterest, x.Insurance, x.OtherCharges, x.Payment,
                    x.ClosingPrincipal,
                    Remaining = x.Principal + x.Interest - x.CapitalizedInterest +
                        x.Insurance + x.OtherCharges - paid.GetValueOrDefault(x.Id) +
                        late.GetValueOrDefault(x.Id)
                }).ToList() });
        });

        group.MapGet("/loans/{loanId:guid}/statement", async (Guid companyId,
            Guid loanId, DateOnly asOf, IStatementService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var statement = await service.GetAsync(companyId, loanId, asOf, ct);
            return statement is null ? Results.NotFound() : Results.Ok(statement);
        });

        group.MapPost("/loans/{loanId:guid}/request", async (Guid companyId, Guid loanId,
            ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.CreateLoan))
                return Results.Forbid();
            return Results.Ok(LoanView(await service.RequestLoanAsync(companyId, loanId, ct)));
        });

        group.MapPost("/loans/{loanId:guid}/approve", async (Guid companyId, Guid loanId,
            ApproveLoan command, ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ApproveLoan))
                return Results.Forbid();
            return Results.Ok(LoanView(await service.ApproveLoanAsync(companyId, loanId, command, ct)));
        });

        group.MapPost("/loans/{loanId:guid}/reject", async (Guid companyId, Guid loanId,
            RejectLoan command, ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ApproveLoan)) return Results.Forbid();
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(LoanView(await service.RejectLoanAsync(companyId, loanId,
                command, actorUserId, ct)));
        });

        group.MapGet("/loans/{loanId:guid}/rejection", async (Guid companyId,
            Guid loanId, ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var rejection = await db.LoanRejections.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loanId)
                .Select(x => new { x.Id, x.LoanId, x.DecisionDate,
                    x.Reason, x.ActorUserId, x.CreatedAtUtc })
                .SingleOrDefaultAsync(ct);
            return rejection is null ? Results.NotFound() : Results.Ok(rejection);
        });

        group.MapPost("/loans/{loanId:guid}/disburse", async (Guid companyId, Guid loanId,
            DisburseLoan command, ILendingService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.DisburseLoan))
                return Results.Forbid();
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(LoanView(await service.DisburseLoanAsync(companyId, loanId, command,
                actorUserId, ct)));
        });

        group.MapPost("/loans/{loanId:guid}/payments", async (Guid companyId, Guid loanId,
            ReceivePayment command, IPaymentService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.RegisterPayment))
                return Results.Forbid();
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var receipt = await service.ReceiveAsync(companyId, loanId, command, actorUserId, ct);
            return Results.Ok(receipt);
        });

        group.MapGet("/loans/{loanId:guid}/payments/{receiptId:guid}", async (
            Guid companyId, Guid loanId, Guid receiptId, IPaymentService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            var receipt = await service.GetAsync(companyId, loanId, receiptId, ct);
            return receipt is null ? Results.NotFound() : Results.Ok(receipt);
        });

        group.MapGet("/loans/{loanId:guid}/payments", async (
            Guid companyId, Guid loanId, int? skip, int? take,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewLoans)) return Results.Forbid();
            if (!await db.Loans.AnyAsync(x => x.CompanyId == companyId && x.Id == loanId, ct))
                return Results.NotFound();
            int offset = Math.Max(0, skip ?? 0);
            int limit = Math.Clamp(take ?? 25, 1, 100);
            var query = db.PaymentReceipts.AsNoTracking().Where(x =>
                x.CompanyId == companyId && x.LoanId == loanId);
            int total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
                .Skip(offset).Take(limit)
                .Select(x => new { x.Id, x.EffectiveDate, x.Received, x.Applied,
                    x.Credit, x.CurrencyCode, x.Method, x.ExternalReference })
                .ToListAsync(ct);
            return Results.Ok(new { total, skip = offset, take = limit, items });
        });

        group.MapPost("/daily-closes", async (Guid companyId, RunDailyClose command,
            IDailyCloseService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.RunDailyClose))
                return Results.Forbid();
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await service.RunAsync(companyId, command, actorUserId, ct));
        });

        group.MapGet("/portfolio", async (Guid companyId, DateOnly asOf,
            IPortfolioService service, HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ViewReports)
                ? Results.Ok(await service.GetAsync(companyId, asOf, ct)) : Results.Forbid());

        group.MapPost("/loans/{loanId:guid}/collections", async (Guid companyId,
            Guid loanId, CreateCollectionActivity command, ICollectionService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.RecordCollection))
                return Results.Forbid();
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activity = await service.CreateAsync(companyId, loanId, command, actorUserId, ct);
            return Results.Ok(activity);
        });

        group.MapGet("/loans/{loanId:guid}/collections", async (Guid companyId,
            Guid loanId, ICollectionService service, HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ViewLoans)
                ? Results.Ok(await service.ListAsync(companyId, loanId, ct)) : Results.Forbid());

        group.MapGet("/dashboard", async (Guid companyId, DateOnly asOf,
            IDashboardService service, HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ViewReports)
                ? Results.Ok(await service.GetAsync(companyId, asOf, ct)) : Results.Forbid());

        group.MapGet("/reports/portfolio.csv", async (Guid companyId, DateOnly asOf,
            IPortfolioService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewReports)) return Results.Forbid();
            var portfolio = await service.GetAsync(companyId, asOf, ct);
            var csv = new StringBuilder();
            csv.AppendLine("Prestamo,Cliente,DiasAtraso,Edad,Moneda,Capital,InteresCorriente,Mora,Seguro,Cargos,SaldoFavor,Total");
            foreach (var row in portfolio.Loans)
            {
                csv.Append(Csv(row.LoanNumber)).Append(',')
                    .Append(row.ClientId).Append(',')
                    .Append(row.DaysPastDue).Append(',')
                    .Append(Csv(row.AgingBucket)).Append(',')
                    .Append(Csv(row.CurrencyCode));
                foreach (decimal amount in new[] { row.Principal, row.CurrentInterest,
                             row.LateInterest, row.Insurance, row.Charges, row.Credit, row.Total })
                    csv.Append(',').Append(amount.ToString(CultureInfo.InvariantCulture));
                csv.AppendLine();
            }
            byte[] content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return Results.File(content, "text/csv; charset=utf-8",
                $"cartera-{portfolio.AsOf:yyyy-MM-dd}.csv");
        });

        group.MapGet("/reports/portfolio.xlsx", async (Guid companyId, DateOnly asOf,
            IPortfolioService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewReports)) return Results.Forbid();
            var portfolio = await service.GetAsync(companyId, asOf, ct);
            return Results.File(PortfolioSpreadsheet.Create(portfolio),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"cartera-{portfolio.AsOf:yyyy-MM-dd}.xlsx");
        });

        group.MapGet("/reports/payments", async (Guid companyId,
            DateOnly from, DateOnly to, IPaymentReportService service,
            HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ViewReports)
                ? Results.Ok(await service.GetAsync(companyId, from, to, ct))
                : Results.Forbid());

        group.MapGet("/reports/payments.csv", async (Guid companyId,
            DateOnly from, DateOnly to, IPaymentReportService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewReports)) return Results.Forbid();
            var report = await service.GetAsync(companyId, from, to, ct);
            var csv = new StringBuilder();
            csv.AppendLine("Fecha,Préstamo,Cliente,Medio,Referencia,Usuario,Recibido,Aplicado,Moneda");
            foreach (var row in report.Rows)
                csv.AppendLine(string.Join(',', new[]
                {
                    row.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Csv(row.LoanNumber), Csv(row.ClientName), Csv(row.Method),
                    Csv(row.ExternalReference), Csv(row.ReceivedBy),
                    row.Received.ToString(CultureInfo.InvariantCulture),
                    row.Applied.ToString(CultureInfo.InvariantCulture), Csv(row.CurrencyCode)
                }));
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv; charset=utf-8", $"recaudos-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
        });

        group.MapGet("/reports/catalog/{kind}", async (Guid companyId, string kind,
            DateOnly from, DateOnly to, IReportCatalogService service,
            HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ViewReports)
                ? Results.Ok(await service.GetAsync(companyId, kind, from, to, ct))
                : Results.Forbid());

        group.MapGet("/reports/catalog/{kind}.csv", async (Guid companyId, string kind,
            DateOnly from, DateOnly to, IReportCatalogService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewReports)) return Results.Forbid();
            var report = await service.GetAsync(companyId, kind, from, to, ct);
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', report.Headers.Select(Csv)));
            foreach (var row in report.Rows)
                csv.AppendLine(string.Join(',', row.Select(Csv)));
            byte[] content = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return Results.File(content, "text/csv; charset=utf-8",
                $"{report.Kind}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
        });

        group.MapGet("/reports/catalog/{kind}.xlsx", async (Guid companyId, string kind,
            DateOnly from, DateOnly to, IReportCatalogService service,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewReports)) return Results.Forbid();
            var report = await service.GetAsync(companyId, kind, from, to, ct);
            return Results.File(ReportSpreadsheet.Create(report),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{report.Kind}-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
        });

        group.MapGet("/my-permissions", (HttpContext http) => Results.Ok(
            Enum.GetValues<BusinessPermission>()
                .Where(permission => HasPermission(http, permission)).ToArray()));

        group.MapGet("/roles", async (Guid companyId, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            var overrides = await db.CompanyRolePermissions.AsNoTracking()
                .Where(x => x.CompanyId == companyId).ToListAsync(ct);
            var roles = Enum.GetValues<InitialRole>().Select(role => new
            {
                role,
                permissions = Enum.GetValues<BusinessPermission>()
                    .Where(permission => PermissionPolicy.IsAllowed(role, permission,
                        overrides.Where(x => x.Role == role)
                            .ToDictionary(x => x.Permission, x => x.Allowed)))
                    .ToArray()
            });
            return Results.Ok(roles);
        });

        group.MapGet("/profiles", async (Guid companyId, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            var profiles = await db.AccessProfiles.AsNoTracking()
                .Where(x => x.CompanyId == companyId).OrderBy(x => x.Name).ToListAsync(ct);
            return Results.Ok(profiles.Select(x => new
            {
                x.Id, x.Name, x.IsActive,
                permissions = Enum.GetValues<BusinessPermission>()
                    .Where(permission => AccessProfilePolicy.IsAllowed(x.PermissionMask, permission))
                    .ToArray()
            }));
        });

        group.MapPost("/profiles", async (Guid companyId, CreateAccessProfileRequest command,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            string name = command.Name?.Trim() ?? string.Empty;
            if (!ValidProfileName(name) || command.Permissions is null)
                return Results.Problem("El nombre o los permisos del perfil no son válidos.", statusCode: 400);
            long mask;
            try { mask = AccessProfilePolicy.CreateMask(command.Permissions); }
            catch (ArgumentException) { return Results.Problem("El perfil contiene permisos no asignables.", statusCode: 400); }
            if (await db.AccessProfiles.AnyAsync(x => x.CompanyId == companyId && x.Name == name, ct))
                return Results.Problem("Ya existe un perfil con ese nombre.", statusCode: 409);
            var profile = new AccessProfile
            {
                CompanyId = companyId, Name = name, PermissionMask = mask,
                IsActive = command.IsActive
            };
            db.AccessProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/companies/{companyId}/profiles/{profile.Id}",
                new { profile.Id, profile.Name, profile.IsActive, command.Permissions });
        });

        group.MapPut("/profiles/{profileId:guid}", async (Guid companyId, Guid profileId,
            UpdateAccessProfileRequest command, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            string name = command.Name?.Trim() ?? string.Empty;
            if (!ValidProfileName(name) || command.Permissions is null ||
                !command.IsActive.HasValue)
                return Results.Problem("El nombre o los permisos del perfil no son válidos.", statusCode: 400);
            long mask;
            try { mask = AccessProfilePolicy.CreateMask(command.Permissions); }
            catch (ArgumentException) { return Results.Problem("El perfil contiene permisos no asignables.", statusCode: 400); }
            var profile = await db.AccessProfiles.SingleOrDefaultAsync(x =>
                x.CompanyId == companyId && x.Id == profileId, ct);
            if (profile is null) return Results.NotFound();
            if (!command.IsActive.Value && await db.UserCompanies.AnyAsync(x =>
                x.CompanyId == companyId && x.ProfileId == profileId && x.IsActive, ct))
                return Results.Problem("Primero reasigna los usuarios activos de este perfil.", statusCode: 409);
            profile.Name = name;
            profile.PermissionMask = mask;
            profile.IsActive = command.IsActive.Value;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { profile.Id, profile.Name, profile.IsActive, command.Permissions });
        });

        group.MapGet("/users", async (Guid companyId, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            var users = await db.UserCompanies.AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .Join(db.Users.AsNoTracking(), membership => membership.UserId,
                    user => user.Id, (membership, user) => new
                    {
                        user.Id, user.Email, membership.Role, membership.ProfileId,
                        profileName = db.AccessProfiles.Where(p => p.Id == membership.ProfileId)
                            .Select(p => p.Name).FirstOrDefault(),
                        isActive = user.IsActive && membership.IsActive,
                        user.CreatedAtUtc
                    }).OrderBy(x => x.Email).ToListAsync(ct);
            return Results.Ok(users);
        });

        group.MapPost("/users", async (Guid companyId, CreateUserRequest command,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            string email = command.Email?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!ValidUserEmail(email) || !ValidUserPassword(command.Password) ||
                (!command.ProfileId.HasValue &&
                 (!command.Role.HasValue || !Enum.IsDefined(command.Role.Value))))
                return Results.Problem("Revisa el correo, la contraseña (mínimo 12 caracteres) y el perfil.", statusCode: 400);
            if (command.ProfileId is Guid newProfileId &&
                !await db.AccessProfiles.AnyAsync(x => x.Id == newProfileId &&
                    x.CompanyId == companyId && x.IsActive, ct))
                return Results.Problem("El perfil personalizado no está disponible.", statusCode: 400);
            if (await db.Users.AnyAsync(x => x.Email == email, ct))
                return Results.Problem("Ya existe un usuario con ese correo.", statusCode: 409);
            var user = new UserAccount { Email = email };
            user.PasswordHash = new PasswordHasher<UserAccount>().HashPassword(user, command.Password);
            db.Users.Add(user);
            db.UserCompanies.Add(new UserCompanyMembership
            {
                CompanyId = companyId, UserId = user.Id,
                Role = command.ProfileId.HasValue ? InitialRole.ReadOnly : command.Role!.Value,
                ProfileId = command.ProfileId
            });
            db.AuditEntries.Add(new AuditEntry
            {
                CompanyId = companyId, EntityName = nameof(UserAccount),
                RecordKey = user.Id.ToString(), Operation = "Create",
                ActorUserId = db.AuditActorUserId, IpAddress = db.AuditIpAddress,
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { email, command.Role, command.ProfileId })
            });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/companies/{companyId}/users/{user.Id}",
                new { user.Id, user.Email, role = command.ProfileId.HasValue
                    ? InitialRole.ReadOnly : command.Role!.Value, command.ProfileId, user.IsActive });
        });

        group.MapPut("/users/{userId:guid}", async (Guid companyId, Guid userId,
            UpdateUserRequest command, ZikloDbContext db, HttpContext http,
            CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            if (!command.IsActive.HasValue || !command.ProfileId.HasValue &&
                (!command.Role.HasValue || !Enum.IsDefined(command.Role.Value)))
                return Results.Problem("El perfil no es válido.", statusCode: 400);
            if (command.ProfileId is Guid selectedProfileId &&
                !await db.AccessProfiles.AnyAsync(x => x.Id == selectedProfileId &&
                    x.CompanyId == companyId && x.IsActive, ct))
                return Results.Problem("El perfil personalizado no está disponible.", statusCode: 400);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            var membership = await db.UserCompanies.SingleOrDefaultAsync(x =>
                x.CompanyId == companyId && x.UserId == userId, ct);
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (membership is null || user is null) return Results.NotFound();
            if (membership.Role == InitialRole.Administrator &&
                (command.Role != InitialRole.Administrator || command.ProfileId.HasValue ||
                 !command.IsActive.Value))
            {
                if (userId == db.AuditActorUserId)
                    return Results.Problem("No puedes retirar tu propio acceso de administrador.", statusCode: 409);
                bool hasOtherAdmin = await db.UserCompanies.AnyAsync(x =>
                    x.CompanyId == companyId && x.UserId != userId && x.IsActive &&
                    x.Role == InitialRole.Administrator, ct);
                if (!hasOtherAdmin)
                    return Results.Problem("Debe quedar al menos un administrador activo.", statusCode: 409);
            }
            membership.Role = command.ProfileId.HasValue ? InitialRole.ReadOnly : command.Role!.Value;
            membership.ProfileId = command.ProfileId;
            membership.IsActive = command.IsActive.Value;
            user.IsActive = command.IsActive.Value;
            db.AuditEntries.Add(new AuditEntry
            {
                CompanyId = companyId, EntityName = nameof(UserAccount),
                RecordKey = userId.ToString(), Operation = "Update",
                ActorUserId = db.AuditActorUserId, IpAddress = db.AuditIpAddress,
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { command.Role, command.ProfileId, command.IsActive })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { user.Id, user.Email, membership.Role,
                membership.ProfileId, isActive = command.IsActive.Value });
        });

        group.MapPut("/users/{userId:guid}/password", async (Guid companyId, Guid userId,
            ResetUserPasswordRequest command, ZikloDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            if (!ValidUserPassword(command.Password))
                return Results.Problem("La contraseña debe tener entre 12 y 128 caracteres.", statusCode: 400);
            if (!await db.UserCompanies.AnyAsync(x =>
                x.CompanyId == companyId && x.UserId == userId, ct)) return Results.NotFound();
            var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
            user.PasswordHash = new PasswordHasher<UserAccount>().HashPassword(user, command.Password);
            db.AuditEntries.Add(new AuditEntry
            {
                CompanyId = companyId, EntityName = nameof(UserAccount),
                RecordKey = userId.ToString(), Operation = "PasswordReset",
                ActorUserId = db.AuditActorUserId, IpAddress = db.AuditIpAddress
            });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/permissions", async (Guid companyId, IPermissionService service,
            HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ManagePermissions)
                ? Results.Ok(await service.ListAsync(companyId, ct)) : Results.Forbid());

        group.MapPut("/permissions/{role}/{permission}", async (Guid companyId,
            InitialRole role, BusinessPermission permission, SetPermission command,
            IPermissionService service, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ManagePermissions)) return Results.Forbid();
            Guid actorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await service.SetAsync(companyId, role, permission,
                command.Allowed, actorUserId,
                http.Connection.RemoteIpAddress?.ToString() ?? string.Empty, ct));
        });

        group.MapGet("/audit", async (Guid companyId, int? take,
            ZikloDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!HasPermission(http, BusinessPermission.ViewAudit)) return Results.Forbid();
            int count = Math.Clamp(take ?? 50, 1, 200);
            var entries = await db.AuditEntries.AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
                .Take(count).ToListAsync(ct);
            return Results.Ok(entries);
        });

        group.MapGet("/consistency", async (Guid companyId,
            IConsistencyService service, HttpContext http, CancellationToken ct) =>
            HasPermission(http, BusinessPermission.ViewAudit)
                ? Results.Ok(await service.CheckAsync(companyId, ct)) : Results.Forbid());
    }

    private static bool HasPermission(HttpContext http, BusinessPermission permission) =>
        http.Items.TryGetValue("profilePermissions", out object? mask) && mask is long custom
            ? AccessProfilePolicy.IsAllowed(custom, permission)
            : http.Items.TryGetValue("companyRole", out object? role) && role is InitialRole actual &&
              PermissionPolicy.IsAllowed(actual, permission,
                  http.Items["companyPermissions"] as IReadOnlyDictionary<BusinessPermission, bool>);

    private static bool ValidUserEmail(string email) => email.Length is > 3 and <= 250 &&
        MailAddress.TryCreate(email, out var parsed) &&
        string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);

    private static bool ValidUserPassword(string? password) =>
        password is { Length: >= 12 and <= 128 } && !password.Any(char.IsControl);

    private static bool ValidProfileName(string name) => name.Length is >= 3 and <= 100 &&
        !name.Any(char.IsControl);

    private static string Csv(string value)
    {
        string leading = value.TrimStart(' ', '\t', '\r', '\n');
        string safe = leading.Length > 0 && "=+-@".Contains(leading[0]) ? "'" + value : value;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }

    private static object LoanView(Loan loan, string? clientName = null) => new
    {
        loan.Id, loan.Number, loan.ClientId, ClientName = clientName,
        loan.ProductId, loan.ProductVersionId,
        loan.OriginalPrincipal, loan.CurrencyCode, loan.RequestedInstallments,
        loan.Status, loan.RequestDate, loan.ApprovalDate, loan.DisbursementDate,
        loan.FirstDueDate
    };
}

internal sealed record CreateUserRequest(string Email, string Password, InitialRole? Role,
    Guid? ProfileId = null);
internal sealed record UpdateUserRequest(InitialRole? Role, bool? IsActive,
    Guid? ProfileId = null);
internal sealed record ResetUserPasswordRequest(string Password);
internal sealed record CreateAccessProfileRequest(string Name,
    BusinessPermission[] Permissions, bool IsActive = true);
internal sealed record UpdateAccessProfileRequest(string Name,
    BusinessPermission[] Permissions, bool? IsActive);
