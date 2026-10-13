using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class ZikloDbContext(DbContextOptions<ZikloDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<UserCompanyMembership> UserCompanies => Set<UserCompanyMembership>();
    public DbSet<AccessProfile> AccessProfiles => Set<AccessProfile>();
    public DbSet<CompanyRolePermission> CompanyRolePermissions => Set<CompanyRolePermission>();
    public DbSet<PermissionChange> PermissionChanges => Set<PermissionChange>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public Guid? AuditActorUserId { get; set; }
    public string AuditIpAddress { get; set; } = string.Empty;
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientReference> ClientReferences => Set<ClientReference>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<CreditProduct> CreditProducts => Set<CreditProduct>();
    public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<CreditApplication> CreditApplications => Set<CreditApplication>();
    public DbSet<CreditApplicationReview> CreditApplicationReviews => Set<CreditApplicationReview>();
    public DbSet<CreditApplicationDocument> CreditApplicationDocuments => Set<CreditApplicationDocument>();
    public DbSet<LoanRejection> LoanRejections => Set<LoanRejection>();
    public DbSet<LoanTerms> LoanTerms => Set<LoanTerms>();
    public DbSet<LoanPlanVersion> LoanPlans => Set<LoanPlanVersion>();
    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
    public DbSet<LoanEvent> LoanEvents => Set<LoanEvent>();
    public DbSet<LoanMovement> LoanMovements => Set<LoanMovement>();
    public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
    public DbSet<PaymentAllocationRecord> PaymentAllocations => Set<PaymentAllocationRecord>();
    public DbSet<DailyCloseRun> DailyCloseRuns => Set<DailyCloseRun>();
    public DbSet<LateInterestAssessment> LateInterestAssessments => Set<LateInterestAssessment>();
    public DbSet<CollectionActivity> CollectionActivities => Set<CollectionActivity>();
    public DbSet<PaymentPromise> PaymentPromises => Set<PaymentPromise>();
    public DbSet<PromiseEvaluation> PromiseEvaluations => Set<PromiseEvaluation>();

    public override int SaveChanges()
    {
        ValidateImmutableRecords();
        PrepareAuditEntries();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateImmutableRecords();
        PrepareAuditEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateImmutableRecords()
    {
        foreach (var entry in ChangeTracker.Entries<ProductVersion>())
            if (entry.State is EntityState.Modified or EntityState.Deleted &&
                entry.OriginalValues.GetValue<bool>(nameof(ProductVersion.IsPublished)))
                throw new InvalidOperationException("Una versión de producto publicada es inmutable.");
        if (ChangeTracker.Entries<LoanTerms>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<LoanEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<LoanMovement>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<PaymentReceipt>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<PaymentAllocationRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<DailyCloseRun>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<LateInterestAssessment>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<CollectionActivity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<ClientDocument>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<CreditApplicationDocument>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<CreditApplicationReview>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<LoanRejection>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<PromiseEvaluation>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<PermissionChange>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<AuditEntry>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Las condiciones aprobadas y el libro financiero son inmutables.");
    }

    private void PrepareAuditEntries()
    {
        ChangeTracker.DetectChanges();
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        foreach (var entry in ChangeTracker.Entries().Where(x =>
                     (x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
                     (x.Entity is Client or ClientReference or ClientDocument or CreditApplication or CreditApplicationDocument or CreditApplicationReview or CreditProduct or ProductVersion or Loan or LoanRejection or Ziklo.Domain.LoanTerms or AccessProfile or
                         PaymentReceipt or CollectionActivity or PaymentPromise or CompanyRolePermission)).ToList())
        {
            Guid companyId = entry.Entity switch
            {
                CompanyRecord record => record.CompanyId,
                LoanTerms terms => terms.CompanyId,
                CompanyRolePermission grant => grant.CompanyId,
                _ => Guid.Empty
            };
            if (companyId == Guid.Empty) continue;
            var properties = entry.Properties.Where(x => x.Metadata.Name is not ("Version" or "Content") &&
                (entry.State != EntityState.Modified || x.IsModified &&
                    !Equals(x.OriginalValue, x.CurrentValue))).ToList();
            if (properties.Count == 0) continue;
            var before = entry.State == EntityState.Added
                ? new Dictionary<string, object?>()
                : properties.ToDictionary(x => x.Metadata.Name, x => x.OriginalValue);
            var after = entry.State == EntityState.Deleted
                ? new Dictionary<string, object?>()
                : properties.ToDictionary(x => x.Metadata.Name, x => x.CurrentValue);
            var key = entry.Metadata.FindPrimaryKey()?.Properties.Select(x =>
                entry.Property(x.Name).CurrentValue?.ToString() ?? string.Empty) ?? [];
            AuditEntries.Add(new AuditEntry
            {
                CompanyId = companyId, EntityName = entry.Metadata.ClrType.Name,
                RecordKey = string.Join(":", key), Operation = entry.State.ToString(),
                BeforeJson = JsonSerializer.Serialize(before, options),
                AfterJson = JsonSerializer.Serialize(after, options),
                ActorUserId = AuditActorUserId,
                IpAddress = AuditIpAddress.Length <= 60 ? AuditIpAddress : AuditIpAddress[..60]
            });
        }
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Company>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.TaxIdentifier).HasMaxLength(50).IsRequired();
            b.Property(x => x.TimeZoneId).HasMaxLength(80).IsRequired();
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength();
            b.HasIndex(x => x.TaxIdentifier).IsUnique();
        });

        model.Entity<UserAccount>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).HasMaxLength(250).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
        });

        model.Entity<UserCompanyMembership>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.UserId });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            b.HasOne<AccessProfile>().WithMany().HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AccessProfile>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        });

        model.Entity<CompanyRolePermission>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.Role, x.Permission });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Permission).HasConversion<string>().HasMaxLength(50);
            b.Property(x => x.Version).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                b.Property(x => x.Version).HasDefaultValueSql("randomblob(8)");
        });

        model.Entity<PermissionChange>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Permission).HasConversion<string>().HasMaxLength(50);
            b.Property(x => x.IpAddress).HasMaxLength(60);
            b.HasIndex(x => new { x.CompanyId, x.ChangedAtUtc });
        });

        model.Entity<AuditEntry>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.EntityName).HasMaxLength(80);
            b.Property(x => x.RecordKey).HasMaxLength(250);
            b.Property(x => x.Operation).HasMaxLength(20);
            b.Property(x => x.IpAddress).HasMaxLength(60);
            b.HasIndex(x => new { x.CompanyId, x.OccurredAtUtc });
            b.HasIndex(x => new { x.CompanyId, x.EntityName, x.RecordKey });
        });

        model.Entity<OutboxMessage>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            b.Property(x => x.PayloadJson).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.EventType, x.AggregateId }).IsUnique();
            b.HasIndex(x => new { x.PublishedAtUtc, x.OccurredAtUtc });
        });

        CompanyKey<Client>(model);
        model.Entity<Client>(b =>
        {
            b.Property(x => x.IdentificationType).HasMaxLength(30).IsRequired();
            b.Property(x => x.IdentificationNumber).HasMaxLength(60).IsRequired();
            b.Property(x => x.Name).HasMaxLength(250).IsRequired();
            b.Property(x => x.Address).HasMaxLength(400);
            b.Property(x => x.Phone).HasMaxLength(60);
            b.Property(x => x.WhatsApp).HasMaxLength(60);
            b.Property(x => x.Email).HasMaxLength(250);
            b.Property(x => x.City).HasMaxLength(120);
            b.Property(x => x.EconomicActivity).HasMaxLength(200);
            b.Property(x => x.Employer).HasMaxLength(250);
            b.Property(x => x.MonthlyIncome).HasPrecision(19, 4);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Version).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                b.Property(x => x.Version).HasDefaultValueSql("randomblob(8)");
            b.HasIndex(x => new { x.CompanyId, x.IdentificationType, x.IdentificationNumber }).IsUnique();
        });

        CompanyKey<ClientReference>(model);
        model.Entity<ClientReference>(b =>
        {
            b.HasOne<Client>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ClientId })
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Name).HasMaxLength(250).IsRequired();
            b.Property(x => x.Relationship).HasMaxLength(120);
            b.Property(x => x.Phone).HasMaxLength(60);
            b.Property(x => x.Email).HasMaxLength(250);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.CompanyId, x.ClientId, x.Kind });
        });

        CompanyKey<ClientDocument>(model);
        model.Entity<ClientDocument>(b =>
        {
            b.HasOne<Client>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ClientId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(80).IsRequired();
            b.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            b.Property(x => x.Content).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.ClientId, x.CreatedAtUtc });
        });

        CompanyKey<CreditProduct>(model);
        model.Entity<CreditProduct>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Version).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                b.Property(x => x.Version).HasDefaultValueSql("randomblob(8)");
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        CompanyKey<ProductVersion>(model);
        model.Entity<ProductVersion>(b =>
        {
            b.HasOne<CreditProduct>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ProductId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.ProductId, x.Number }).IsUnique();
            b.HasAlternateKey(x => new { x.CompanyId, x.ProductId, x.Id });
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength();
            b.Property(x => x.MinimumAmount).HasPrecision(19, 4);
            b.Property(x => x.MaximumAmount).HasPrecision(19, 4);
            b.Property(x => x.Rate).HasPrecision(18, 10);
            b.Property(x => x.InsurancePerInstallment).HasPrecision(19, 4);
            b.Property(x => x.OtherChargePerInstallment).HasPrecision(19, 4);
            b.Property(x => x.LateAnnualRate).HasPrecision(18, 10);
            b.Property(x => x.LateMaximumPerInstallment).HasPrecision(19, 4);
            b.Property(x => x.LateGraceMode).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.PaymentPriority).HasMaxLength(250).IsRequired();
            b.Property(x => x.RateKind).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.DayCount).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(40);
            b.ToTable(t => t.HasCheckConstraint("CK_ProductVersion_Limits",
                "[MinimumAmount] > 0 AND [MaximumAmount] >= [MinimumAmount] AND [MinimumInstallments] > 0 AND [MaximumInstallments] >= [MinimumInstallments] AND [Rate] >= 0"));
        });

        CompanyKey<CreditApplication>(model);
        model.Entity<CreditApplication>(b =>
        {
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.SubmittedByUserId, x.Status });
            b.HasOne<Loan>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.LoanId).IsConcurrencyToken();
            b.HasOne<ProductVersion>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.ProductVersionId })
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Reference).HasMaxLength(35).IsRequired();
            b.Property(x => x.FormDataJson).IsRequired();
            b.Property(x => x.IdentificationType).HasMaxLength(30).IsRequired();
            b.Property(x => x.IdentificationNumber).HasMaxLength(60).IsRequired();
            b.Property(x => x.ApplicantName).HasMaxLength(250).IsRequired();
            b.Property(x => x.Email).HasMaxLength(250).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(60).IsRequired();
            b.Property(x => x.City).HasMaxLength(120).IsRequired();
            b.Property(x => x.EconomicActivity).HasMaxLength(200).IsRequired();
            b.Property(x => x.Purpose).HasMaxLength(1000).IsRequired();
            b.Property(x => x.AcknowledgementVersion).HasMaxLength(30).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsConcurrencyToken();
            Money(b.Property(x => x.MonthlyIncome));
            Money(b.Property(x => x.MonthlyObligations));
            Money(b.Property(x => x.RequestedAmount));
            b.HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.LoanId }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.CompanyId, x.IdentificationNumber });
        });

        CompanyKey<CreditApplicationDocument>(model);
        CompanyKey<CreditApplicationReview>(model);
        model.Entity<CreditApplicationReview>(b =>
        {
            b.HasOne<CreditApplication>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.ApplicationId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany()
                .HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
            b.Property(x => x.VerifiedMonthlyIncome).HasPrecision(19, 4);
            b.Property(x => x.VerifiedMonthlyObligations).HasPrecision(19, 4);
            b.HasIndex(x => new { x.CompanyId, x.ApplicationId, x.RecordedAtUtc });
        });
        model.Entity<CreditApplicationDocument>(b =>
        {
            b.HasOne<CreditApplication>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.ApplicationId })
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(80).IsRequired();
            b.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            b.Property(x => x.Content).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.ApplicationId, x.Kind }).IsUnique();
        });

        CompanyKey<Loan>(model);
        model.Entity<Loan>(b =>
        {
            b.HasOne<Client>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ClientId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CreditProduct>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ProductId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ProductVersion>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.ProductId, x.ProductVersionId })
                .HasPrincipalKey(x => new { x.CompanyId, x.ProductId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.OriginalPrincipal).HasPrecision(19, 4);
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Version).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                b.Property(x => x.Version).HasDefaultValueSql("randomblob(8)");
            b.HasIndex(x => new { x.CompanyId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.ClientId, x.Status });
            b.ToTable(t => t.HasCheckConstraint("CK_Loan_Principal", "[OriginalPrincipal] > 0"));
            b.HasOne(x => x.Terms).WithOne().HasForeignKey<LoanTerms>(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        CompanyKey<LoanRejection>(model);
        model.Entity<LoanRejection>(b =>
        {
            b.HasOne<Loan>().WithMany().HasForeignKey(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.LoanId }).IsUnique();
        });

        model.Entity<LoanTerms>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.LoanId });
            b.HasOne<ProductVersion>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.SourceProductVersionId })
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Rate).HasPrecision(18, 10);
            b.Property(x => x.RateKind).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.DayCount).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.InsurancePerInstallment).HasPrecision(19, 4);
            b.Property(x => x.OtherChargePerInstallment).HasPrecision(19, 4);
            b.Property(x => x.LateAnnualRate).HasPrecision(18, 10);
            b.Property(x => x.LateMaximumPerInstallment).HasPrecision(19, 4);
            b.Property(x => x.LateGraceMode).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.PaymentPriority).HasMaxLength(250).IsRequired();
        });

        CompanyKey<LoanPlanVersion>(model);
        model.Entity<LoanPlanVersion>(b =>
        {
            b.HasOne<Loan>().WithMany().HasForeignKey(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasAlternateKey(x => new { x.CompanyId, x.LoanId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.LoanId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.LoanId, x.IsCurrent })
                .IsUnique().HasFilter("[IsCurrent] = 1");
            b.Property(x => x.Reason).HasMaxLength(300);
        });

        CompanyKey<LoanInstallment>(model);
        model.Entity<LoanInstallment>(b =>
        {
            b.HasOne<LoanPlanVersion>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.PlanId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasAlternateKey(x => new { x.CompanyId, x.LoanId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.PlanId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.DueDate });
            Money(b.Property(x => x.OpeningPrincipal));
            Money(b.Property(x => x.Principal));
            Money(b.Property(x => x.Interest));
            Money(b.Property(x => x.CapitalizedInterest));
            Money(b.Property(x => x.Insurance));
            Money(b.Property(x => x.OtherCharges));
            Money(b.Property(x => x.Payment));
            Money(b.Property(x => x.ClosingPrincipal));
        });

        CompanyKey<LoanEvent>(model);
        model.Entity<LoanEvent>(b =>
        {
            b.HasOne<Loan>().WithMany().HasForeignKey(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasAlternateKey(x => new { x.CompanyId, x.LoanId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.Type, x.IdempotencyKey }).IsUnique();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
            b.Property(x => x.OriginReference).HasMaxLength(120).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
        });

        CompanyKey<LoanMovement>(model);
        model.Entity<LoanMovement>(b =>
        {
            b.HasOne<LoanEvent>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.EventId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<LoanInstallment>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.InstallmentId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.LoanId, x.Sequence }).IsUnique();
            b.Property(x => x.Component).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength();
            b.ToTable(t => t.HasCheckConstraint("CK_LoanMovement_Amount", "[Amount] <> 0"));
        });

        CompanyKey<PaymentReceipt>(model);
        model.Entity<PaymentReceipt>(b =>
        {
            b.HasOne<Loan>().WithMany().HasForeignKey(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Client>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ClientId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<LoanEvent>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.EventId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasAlternateKey(x => new { x.CompanyId, x.LoanId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.IdempotencyKey }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.EffectiveDate, x.Status });
            Money(b.Property(x => x.Received));
            Money(b.Property(x => x.Applied));
            Money(b.Property(x => x.Credit));
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength();
            b.Property(x => x.Method).HasMaxLength(40).IsRequired();
            b.Property(x => x.ExternalReference).HasMaxLength(120).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.ToTable(t => t.HasCheckConstraint("CK_PaymentReceipt_Amounts",
                "[Received] > 0 AND [Applied] >= 0 AND [Credit] >= 0 AND [Received] = [Applied] + [Credit]"));
        });

        CompanyKey<PaymentAllocationRecord>(model);
        model.Entity<PaymentAllocationRecord>(b =>
        {
            b.HasOne<PaymentReceipt>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.ReceiptId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<LoanInstallment>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.InstallmentId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.ReceiptId, x.Order }).IsUnique();
            Money(b.Property(x => x.Amount));
            b.Property(x => x.Component).HasConversion<string>().HasMaxLength(40);
            b.ToTable(t => t.HasCheckConstraint("CK_PaymentAllocation_Amount", "[Amount] > 0"));
        });

        model.Entity<DailyCloseRun>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.BusinessDate }).IsUnique();
        });

        CompanyKey<LateInterestAssessment>(model);
        model.Entity<LateInterestAssessment>(b =>
        {
            b.HasOne<LoanEvent>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.EventId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<LoanInstallment>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.InstallmentId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.LoanId, x.InstallmentId, x.CutoffDate }).IsUnique();
            Money(b.Property(x => x.BaseAtCutoff));
            b.Property(x => x.AnnualRate).HasPrecision(18, 10);
            Money(b.Property(x => x.AlreadyPosted));
            Money(b.Property(x => x.CumulativeInterest));
            Money(b.Property(x => x.AmountPosted));
            b.Property(x => x.GraceMode).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.DayCount).HasConversion<string>().HasMaxLength(40);
        });

        CompanyKey<CollectionActivity>(model);
        model.Entity<CollectionActivity>(b =>
        {
            b.HasOne<Loan>().WithMany().HasForeignKey(x => new { x.CompanyId, x.LoanId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Client>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ClientId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasAlternateKey(x => new { x.CompanyId, x.LoanId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.LoanId, x.ContactDate });
            b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Result).HasMaxLength(120).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
        });

        CompanyKey<PaymentPromise>(model);
        model.Entity<PaymentPromise>(b =>
        {
            b.HasOne<CollectionActivity>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.CollectionActivityId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasAlternateKey(x => new { x.CompanyId, x.LoanId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.LoanId, x.Status })
                .IsUnique().HasFilter("[Status] = 'Pending'");
            b.HasIndex(x => new { x.CompanyId, x.DueDate, x.Status });
            Money(b.Property(x => x.PromisedAmount));
            b.Property(x => x.ReceivedAmount).HasPrecision(19, 4);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Version).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                b.Property(x => x.Version).HasDefaultValueSql("randomblob(8)");
            b.ToTable(t => t.HasCheckConstraint("CK_PaymentPromise_Amount", "[PromisedAmount] > 0"));
        });

        CompanyKey<PromiseEvaluation>(model);
        model.Entity<PromiseEvaluation>(b =>
        {
            b.HasOne<PaymentPromise>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.LoanId, x.PromiseId })
                .HasPrincipalKey(x => new { x.CompanyId, x.LoanId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DailyCloseRun>().WithMany()
                .HasForeignKey(x => new { x.CompanyId, x.DailyCloseRunId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.PromiseId }).IsUnique();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            Money(b.Property(x => x.ReceivedAmount));
        });
    }

    private static void CompanyKey<T>(ModelBuilder model) where T : CompanyRecord
    {
        model.Entity<T>(b =>
        {
            b.HasKey(x => new { x.CompanyId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void Money(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal> property) =>
        property.HasPrecision(19, 4);
}
