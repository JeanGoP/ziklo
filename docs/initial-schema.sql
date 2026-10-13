IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TaxIdentifier] nvarchar(50) NOT NULL,
        [TimeZoneId] nvarchar(80) NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [Clients] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [IdentificationType] nvarchar(30) NOT NULL,
        [IdentificationNumber] nvarchar(60) NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Address] nvarchar(400) NULL,
        [Phone] nvarchar(60) NULL,
        [WhatsApp] nvarchar(60) NULL,
        [Email] nvarchar(250) NULL,
        [City] nvarchar(120) NULL,
        [BirthDate] date NULL,
        [EconomicActivity] nvarchar(200) NULL,
        [MonthlyIncome] decimal(19,4) NULL,
        [Employer] nvarchar(250) NULL,
        [Status] nvarchar(30) NOT NULL,
        [Version] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_Clients_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [CreditProducts] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [Version] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CreditProducts] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_CreditProducts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [ProductVersions] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [ValidFrom] date NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [MinimumAmount] decimal(19,4) NOT NULL,
        [MaximumAmount] decimal(19,4) NOT NULL,
        [MinimumInstallments] int NOT NULL,
        [MaximumInstallments] int NOT NULL,
        [Rate] decimal(18,10) NOT NULL,
        [RateKind] nvarchar(40) NOT NULL,
        [DayCount] nvarchar(40) NOT NULL,
        [Frequency] nvarchar(40) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [CurrencyDecimals] int NOT NULL,
        [AllowsCapitalization] bit NOT NULL,
        [InsurancePerInstallment] decimal(19,4) NOT NULL,
        [OtherChargePerInstallment] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductVersions] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_ProductVersions_CompanyId_ProductId_Id] UNIQUE ([CompanyId], [ProductId], [Id]),
        CONSTRAINT [CK_ProductVersion_Limits] CHECK ([MinimumAmount] > 0 AND [MaximumAmount] >= [MinimumAmount] AND [MinimumInstallments] > 0 AND [MaximumInstallments] >= [MinimumInstallments] AND [Rate] >= 0),
        CONSTRAINT [FK_ProductVersions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductVersions_CreditProducts_CompanyId_ProductId] FOREIGN KEY ([CompanyId], [ProductId]) REFERENCES [CreditProducts] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [Loans] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductVersionId] uniqueidentifier NOT NULL,
        [Number] nvarchar(50) NOT NULL,
        [RequestDate] date NOT NULL,
        [ApprovalDate] date NULL,
        [DisbursementDate] date NULL,
        [FirstDueDate] date NULL,
        [OriginalPrincipal] decimal(19,4) NOT NULL,
        [RequestedInstallments] int NOT NULL,
        [RequestedCapitalization] bit NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Version] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Loans] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [CK_Loan_Principal] CHECK ([OriginalPrincipal] > 0),
        CONSTRAINT [FK_Loans_Clients_CompanyId_ClientId] FOREIGN KEY ([CompanyId], [ClientId]) REFERENCES [Clients] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Loans_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Loans_CreditProducts_CompanyId_ProductId] FOREIGN KEY ([CompanyId], [ProductId]) REFERENCES [CreditProducts] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Loans_ProductVersions_CompanyId_ProductId_ProductVersionId] FOREIGN KEY ([CompanyId], [ProductId], [ProductVersionId]) REFERENCES [ProductVersions] ([CompanyId], [ProductId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [LoanEvents] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [EffectiveDate] date NOT NULL,
        [OriginReference] nvarchar(120) NOT NULL,
        [IdempotencyKey] nvarchar(120) NOT NULL,
        [ReversesEventId] uniqueidentifier NULL,
        [Reason] nvarchar(500) NULL,
        [ActorUserId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanEvents] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_LoanEvents_CompanyId_LoanId_Id] UNIQUE ([CompanyId], [LoanId], [Id]),
        CONSTRAINT [FK_LoanEvents_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanEvents_Loans_CompanyId_LoanId] FOREIGN KEY ([CompanyId], [LoanId]) REFERENCES [Loans] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [LoanPlans] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [IsCurrent] bit NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [PreviousPlanId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanPlans] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_LoanPlans_CompanyId_LoanId_Id] UNIQUE ([CompanyId], [LoanId], [Id]),
        CONSTRAINT [FK_LoanPlans_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanPlans_Loans_CompanyId_LoanId] FOREIGN KEY ([CompanyId], [LoanId]) REFERENCES [Loans] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [LoanTerms] (
        [CompanyId] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [SourceProductVersionId] uniqueidentifier NOT NULL,
        [Rate] decimal(18,10) NOT NULL,
        [RateKind] nvarchar(40) NOT NULL,
        [DayCount] nvarchar(40) NOT NULL,
        [Frequency] nvarchar(40) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [Installments] int NOT NULL,
        [CurrencyDecimals] int NOT NULL,
        [CapitalizeInterest] bit NOT NULL,
        [InsurancePerInstallment] decimal(19,4) NOT NULL,
        [OtherChargePerInstallment] decimal(19,4) NOT NULL,
        [CapturedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanTerms] PRIMARY KEY ([CompanyId], [LoanId]),
        CONSTRAINT [FK_LoanTerms_Loans_CompanyId_LoanId] FOREIGN KEY ([CompanyId], [LoanId]) REFERENCES [Loans] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanTerms_ProductVersions_CompanyId_SourceProductVersionId] FOREIGN KEY ([CompanyId], [SourceProductVersionId]) REFERENCES [ProductVersions] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [LoanInstallments] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [DueDate] date NOT NULL,
        [OpeningPrincipal] decimal(19,4) NOT NULL,
        [Principal] decimal(19,4) NOT NULL,
        [Interest] decimal(19,4) NOT NULL,
        [CapitalizedInterest] decimal(19,4) NOT NULL,
        [Insurance] decimal(19,4) NOT NULL,
        [OtherCharges] decimal(19,4) NOT NULL,
        [Payment] decimal(19,4) NOT NULL,
        [ClosingPrincipal] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanInstallments] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_LoanInstallments_CompanyId_LoanId_Id] UNIQUE ([CompanyId], [LoanId], [Id]),
        CONSTRAINT [FK_LoanInstallments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanInstallments_LoanPlans_CompanyId_LoanId_PlanId] FOREIGN KEY ([CompanyId], [LoanId], [PlanId]) REFERENCES [LoanPlans] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE TABLE [LoanMovements] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [InstallmentId] uniqueidentifier NULL,
        [Sequence] bigint NOT NULL,
        [Component] nvarchar(40) NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanMovements] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [CK_LoanMovement_Amount] CHECK ([Amount] <> 0),
        CONSTRAINT [FK_LoanMovements_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanMovements_LoanEvents_CompanyId_LoanId_EventId] FOREIGN KEY ([CompanyId], [LoanId], [EventId]) REFERENCES [LoanEvents] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanMovements_LoanInstallments_CompanyId_LoanId_InstallmentId] FOREIGN KEY ([CompanyId], [LoanId], [InstallmentId]) REFERENCES [LoanInstallments] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Clients_CompanyId_IdentificationType_IdentificationNumber] ON [Clients] ([CompanyId], [IdentificationType], [IdentificationNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_TaxIdentifier] ON [Companies] ([TaxIdentifier]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CreditProducts_CompanyId_Code] ON [CreditProducts] ([CompanyId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LoanEvents_CompanyId_Type_IdempotencyKey] ON [LoanEvents] ([CompanyId], [Type], [IdempotencyKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_LoanInstallments_CompanyId_DueDate] ON [LoanInstallments] ([CompanyId], [DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_LoanInstallments_CompanyId_LoanId_PlanId] ON [LoanInstallments] ([CompanyId], [LoanId], [PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LoanInstallments_CompanyId_PlanId_Number] ON [LoanInstallments] ([CompanyId], [PlanId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_LoanMovements_CompanyId_LoanId_EventId] ON [LoanMovements] ([CompanyId], [LoanId], [EventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_LoanMovements_CompanyId_LoanId_InstallmentId] ON [LoanMovements] ([CompanyId], [LoanId], [InstallmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LoanMovements_CompanyId_LoanId_Sequence] ON [LoanMovements] ([CompanyId], [LoanId], [Sequence]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LoanPlans_CompanyId_LoanId_IsCurrent] ON [LoanPlans] ([CompanyId], [LoanId], [IsCurrent]) WHERE [IsCurrent] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LoanPlans_CompanyId_LoanId_Number] ON [LoanPlans] ([CompanyId], [LoanId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_Loans_CompanyId_ClientId_Status] ON [Loans] ([CompanyId], [ClientId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Loans_CompanyId_Number] ON [Loans] ([CompanyId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_Loans_CompanyId_ProductId_ProductVersionId] ON [Loans] ([CompanyId], [ProductId], [ProductVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE INDEX [IX_LoanTerms_CompanyId_SourceProductVersionId] ON [LoanTerms] ([CompanyId], [SourceProductVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVersions_CompanyId_ProductId_Number] ON [ProductVersions] ([CompanyId], [ProductId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001154356_InitialLendingSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001154356_InitialLendingSchema', N'8.0.13');
END;
GO

COMMIT;
GO
BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001155636_InitialIdentity'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(250) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001155636_InitialIdentity'
)
BEGIN
    CREATE TABLE [UserCompanies] (
        [CompanyId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [IsActive] bit NOT NULL,
        [GrantedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserCompanies] PRIMARY KEY ([CompanyId], [UserId]),
        CONSTRAINT [FK_UserCompanies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserCompanies_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001155636_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_UserCompanies_UserId] ON [UserCompanies] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001155636_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001155636_InitialIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001155636_InitialIdentity', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001160011_EventActorRelation'
)
BEGIN
    CREATE INDEX [IX_LoanEvents_ActorUserId] ON [LoanEvents] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001160011_EventActorRelation'
)
BEGIN
    ALTER TABLE [LoanEvents] ADD CONSTRAINT [FK_LoanEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001160011_EventActorRelation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001160011_EventActorRelation', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE TABLE [PaymentReceipts] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [EffectiveDate] date NOT NULL,
        [Received] decimal(19,4) NOT NULL,
        [Applied] decimal(19,4) NOT NULL,
        [Credit] decimal(19,4) NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [ExternalReference] nvarchar(120) NOT NULL,
        [IdempotencyKey] nvarchar(120) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentReceipts] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_PaymentReceipts_CompanyId_LoanId_Id] UNIQUE ([CompanyId], [LoanId], [Id]),
        CONSTRAINT [CK_PaymentReceipt_Amounts] CHECK ([Received] > 0 AND [Applied] >= 0 AND [Credit] >= 0 AND [Received] = [Applied] + [Credit]),
        CONSTRAINT [FK_PaymentReceipts_Clients_CompanyId_ClientId] FOREIGN KEY ([CompanyId], [ClientId]) REFERENCES [Clients] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentReceipts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentReceipts_LoanEvents_CompanyId_LoanId_EventId] FOREIGN KEY ([CompanyId], [LoanId], [EventId]) REFERENCES [LoanEvents] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentReceipts_Loans_CompanyId_LoanId] FOREIGN KEY ([CompanyId], [LoanId]) REFERENCES [Loans] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentReceipts_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE TABLE [PaymentAllocations] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [ReceiptId] uniqueidentifier NOT NULL,
        [InstallmentId] uniqueidentifier NOT NULL,
        [Component] nvarchar(40) NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Order] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentAllocations] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [CK_PaymentAllocation_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_PaymentAllocations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocations_LoanInstallments_CompanyId_LoanId_InstallmentId] FOREIGN KEY ([CompanyId], [LoanId], [InstallmentId]) REFERENCES [LoanInstallments] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocations_PaymentReceipts_CompanyId_LoanId_ReceiptId] FOREIGN KEY ([CompanyId], [LoanId], [ReceiptId]) REFERENCES [PaymentReceipts] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_CompanyId_LoanId_InstallmentId] ON [PaymentAllocations] ([CompanyId], [LoanId], [InstallmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_CompanyId_LoanId_ReceiptId] ON [PaymentAllocations] ([CompanyId], [LoanId], [ReceiptId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentAllocations_CompanyId_ReceiptId_Order] ON [PaymentAllocations] ([CompanyId], [ReceiptId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE INDEX [IX_PaymentReceipts_ActorUserId] ON [PaymentReceipts] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE INDEX [IX_PaymentReceipts_CompanyId_ClientId] ON [PaymentReceipts] ([CompanyId], [ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE INDEX [IX_PaymentReceipts_CompanyId_EffectiveDate_Status] ON [PaymentReceipts] ([CompanyId], [EffectiveDate], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentReceipts_CompanyId_IdempotencyKey] ON [PaymentReceipts] ([CompanyId], [IdempotencyKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    CREATE INDEX [IX_PaymentReceipts_CompanyId_LoanId_EventId] ON [PaymentReceipts] ([CompanyId], [LoanId], [EventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001171808_PaymentReceipts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001171808_PaymentReceipts', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [ProductVersions] ADD [LateAnnualRate] decimal(18,10) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [ProductVersions] ADD [LateGraceDays] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [ProductVersions] ADD [LateGraceMode] nvarchar(30) NOT NULL DEFAULT N'Prospective';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [ProductVersions] ADD [LateMaximumPerInstallment] decimal(19,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [LoanTerms] ADD [LateAnnualRate] decimal(18,10) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [LoanTerms] ADD [LateGraceDays] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [LoanTerms] ADD [LateGraceMode] nvarchar(30) NOT NULL DEFAULT N'Prospective';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    ALTER TABLE [LoanTerms] ADD [LateMaximumPerInstallment] decimal(19,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    CREATE TABLE [DailyCloseRuns] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [BusinessDate] date NOT NULL,
        [CompletedAtUtc] datetime2 NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [LoansProcessed] int NOT NULL,
        [AccrualEvents] int NOT NULL,
        [LateEvents] int NOT NULL,
        CONSTRAINT [PK_DailyCloseRuns] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_DailyCloseRuns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DailyCloseRuns_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    CREATE TABLE [LateInterestAssessments] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [InstallmentId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [DueDate] date NOT NULL,
        [CutoffDate] date NOT NULL,
        [EligibleDays] int NOT NULL,
        [BaseAtCutoff] decimal(19,4) NOT NULL,
        [AnnualRate] decimal(18,10) NOT NULL,
        [AlreadyPosted] decimal(19,4) NOT NULL,
        [CumulativeInterest] decimal(19,4) NOT NULL,
        [AmountPosted] decimal(19,4) NOT NULL,
        [GraceDays] int NOT NULL,
        [GraceMode] nvarchar(30) NOT NULL,
        [DayCount] nvarchar(40) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LateInterestAssessments] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_LateInterestAssessments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LateInterestAssessments_LoanEvents_CompanyId_LoanId_EventId] FOREIGN KEY ([CompanyId], [LoanId], [EventId]) REFERENCES [LoanEvents] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LateInterestAssessments_LoanInstallments_CompanyId_LoanId_InstallmentId] FOREIGN KEY ([CompanyId], [LoanId], [InstallmentId]) REFERENCES [LoanInstallments] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    CREATE INDEX [IX_DailyCloseRuns_ActorUserId] ON [DailyCloseRuns] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DailyCloseRuns_CompanyId_BusinessDate] ON [DailyCloseRuns] ([CompanyId], [BusinessDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    CREATE INDEX [IX_LateInterestAssessments_CompanyId_LoanId_EventId] ON [LateInterestAssessments] ([CompanyId], [LoanId], [EventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LateInterestAssessments_CompanyId_LoanId_InstallmentId_CutoffDate] ON [LateInterestAssessments] ([CompanyId], [LoanId], [InstallmentId], [CutoffDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001173050_DailyCloseAndLateTerms'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001173050_DailyCloseAndLateTerms', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    ALTER TABLE [DailyCloseRuns] ADD [PromisesEvaluated] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE TABLE [CollectionActivities] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [ContactDate] date NOT NULL,
        [Channel] nvarchar(30) NOT NULL,
        [Result] nvarchar(120) NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        [NextContactDate] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CollectionActivities] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_CollectionActivities_CompanyId_LoanId_Id] UNIQUE ([CompanyId], [LoanId], [Id]),
        CONSTRAINT [FK_CollectionActivities_Clients_CompanyId_ClientId] FOREIGN KEY ([CompanyId], [ClientId]) REFERENCES [Clients] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionActivities_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionActivities_Loans_CompanyId_LoanId] FOREIGN KEY ([CompanyId], [LoanId]) REFERENCES [Loans] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionActivities_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE TABLE [PaymentPromises] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [CollectionActivityId] uniqueidentifier NOT NULL,
        [StartDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [PromisedAmount] decimal(19,4) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [EvaluatedOn] date NULL,
        [ReceivedAmount] decimal(19,4) NULL,
        [Version] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentPromises] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [AK_PaymentPromises_CompanyId_LoanId_Id] UNIQUE ([CompanyId], [LoanId], [Id]),
        CONSTRAINT [CK_PaymentPromise_Amount] CHECK ([PromisedAmount] > 0),
        CONSTRAINT [FK_PaymentPromises_CollectionActivities_CompanyId_LoanId_CollectionActivityId] FOREIGN KEY ([CompanyId], [LoanId], [CollectionActivityId]) REFERENCES [CollectionActivities] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentPromises_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE TABLE [PromiseEvaluations] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [PromiseId] uniqueidentifier NOT NULL,
        [DailyCloseRunId] uniqueidentifier NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ReceivedAmount] decimal(19,4) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PromiseEvaluations] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_PromiseEvaluations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromiseEvaluations_DailyCloseRuns_CompanyId_DailyCloseRunId] FOREIGN KEY ([CompanyId], [DailyCloseRunId]) REFERENCES [DailyCloseRuns] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromiseEvaluations_PaymentPromises_CompanyId_LoanId_PromiseId] FOREIGN KEY ([CompanyId], [LoanId], [PromiseId]) REFERENCES [PaymentPromises] ([CompanyId], [LoanId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromiseEvaluations_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_CollectionActivities_ActorUserId] ON [CollectionActivities] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_CollectionActivities_CompanyId_ClientId] ON [CollectionActivities] ([CompanyId], [ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_CollectionActivities_CompanyId_LoanId_ContactDate] ON [CollectionActivities] ([CompanyId], [LoanId], [ContactDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_PaymentPromises_CompanyId_DueDate_Status] ON [PaymentPromises] ([CompanyId], [DueDate], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_PaymentPromises_CompanyId_LoanId_CollectionActivityId] ON [PaymentPromises] ([CompanyId], [LoanId], [CollectionActivityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PaymentPromises_CompanyId_LoanId_Status] ON [PaymentPromises] ([CompanyId], [LoanId], [Status]) WHERE [Status] = ''Pending''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_PromiseEvaluations_ActorUserId] ON [PromiseEvaluations] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_PromiseEvaluations_CompanyId_DailyCloseRunId] ON [PromiseEvaluations] ([CompanyId], [DailyCloseRunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE INDEX [IX_PromiseEvaluations_CompanyId_LoanId_PromiseId] ON [PromiseEvaluations] ([CompanyId], [LoanId], [PromiseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PromiseEvaluations_CompanyId_PromiseId] ON [PromiseEvaluations] ([CompanyId], [PromiseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001174303_CollectionsAndPromises'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001174303_CollectionsAndPromises', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001175715_RolePermissions'
)
BEGIN
    CREATE TABLE [CompanyRolePermissions] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [Permission] nvarchar(50) NOT NULL,
        [Allowed] bit NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_CompanyRolePermissions] PRIMARY KEY ([CompanyId], [Role], [Permission]),
        CONSTRAINT [FK_CompanyRolePermissions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompanyRolePermissions_Users_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001175715_RolePermissions'
)
BEGIN
    CREATE TABLE [PermissionChanges] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [Permission] nvarchar(50) NOT NULL,
        [PreviousAllowed] bit NOT NULL,
        [NewAllowed] bit NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [IpAddress] nvarchar(60) NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PermissionChanges] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_PermissionChanges_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PermissionChanges_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001175715_RolePermissions'
)
BEGIN
    CREATE INDEX [IX_CompanyRolePermissions_ChangedByUserId] ON [CompanyRolePermissions] ([ChangedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001175715_RolePermissions'
)
BEGIN
    CREATE INDEX [IX_PermissionChanges_ActorUserId] ON [PermissionChanges] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001175715_RolePermissions'
)
BEGIN
    CREATE INDEX [IX_PermissionChanges_CompanyId_ChangedAtUtc] ON [PermissionChanges] ([CompanyId], [ChangedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001175715_RolePermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001175715_RolePermissions', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180048_AuditTrail'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [EntityName] nvarchar(80) NOT NULL,
        [RecordKey] nvarchar(250) NOT NULL,
        [Operation] nvarchar(20) NOT NULL,
        [BeforeJson] nvarchar(max) NOT NULL,
        [AfterJson] nvarchar(max) NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [IpAddress] nvarchar(60) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_AuditEntries_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AuditEntries_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180048_AuditTrail'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_ActorUserId] ON [AuditEntries] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180048_AuditTrail'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_CompanyId_EntityName_RecordKey] ON [AuditEntries] ([CompanyId], [EntityName], [RecordKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180048_AuditTrail'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_CompanyId_OccurredAtUtc] ON [AuditEntries] ([CompanyId], [OccurredAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180048_AuditTrail'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001180048_AuditTrail', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180419_IntegrationOutbox'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [AggregateId] uniqueidentifier NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [PublishedAtUtc] datetime2 NULL,
        [DeliveryAttempts] int NOT NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_OutboxMessages_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180419_IntegrationOutbox'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OutboxMessages_CompanyId_EventType_AggregateId] ON [OutboxMessages] ([CompanyId], [EventType], [AggregateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180419_IntegrationOutbox'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublishedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([PublishedAtUtc], [OccurredAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001180419_IntegrationOutbox'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001180419_IntegrationOutbox', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001183027_PaymentPriorityPolicy'
)
BEGIN
    ALTER TABLE [ProductVersions] ADD [PaymentPriority] nvarchar(250) NOT NULL DEFAULT N'Charges,LateInterest,OverdueCurrentInterest,OverduePrincipal,CurrentInterest,Principal';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001183027_PaymentPriorityPolicy'
)
BEGIN
    ALTER TABLE [LoanTerms] ADD [PaymentPriority] nvarchar(250) NOT NULL DEFAULT N'Charges,LateInterest,OverdueCurrentInterest,OverduePrincipal,CurrentInterest,Principal';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001183027_PaymentPriorityPolicy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001183027_PaymentPriorityPolicy', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001184222_ClientReferences'
)
BEGIN
    CREATE TABLE [ClientReferences] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Relationship] nvarchar(120) NULL,
        [Phone] nvarchar(60) NULL,
        [Email] nvarchar(250) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ClientReferences] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_ClientReferences_Clients_CompanyId_ClientId] FOREIGN KEY ([CompanyId], [ClientId]) REFERENCES [Clients] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientReferences_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001184222_ClientReferences'
)
BEGIN
    CREATE INDEX [IX_ClientReferences_CompanyId_ClientId_Kind] ON [ClientReferences] ([CompanyId], [ClientId], [Kind]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001184222_ClientReferences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001184222_ClientReferences', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185219_ClientDocuments'
)
BEGIN
    CREATE TABLE [ClientDocuments] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(80) NOT NULL,
        [Length] bigint NOT NULL,
        [Sha256] nchar(64) NOT NULL,
        [Content] varbinary(max) NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ClientDocuments] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_ClientDocuments_Clients_CompanyId_ClientId] FOREIGN KEY ([CompanyId], [ClientId]) REFERENCES [Clients] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientDocuments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientDocuments_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185219_ClientDocuments'
)
BEGIN
    CREATE INDEX [IX_ClientDocuments_CompanyId_ClientId_CreatedAtUtc] ON [ClientDocuments] ([CompanyId], [ClientId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185219_ClientDocuments'
)
BEGIN
    CREATE INDEX [IX_ClientDocuments_UploadedByUserId] ON [ClientDocuments] ([UploadedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185219_ClientDocuments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001185219_ClientDocuments', N'8.0.13');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185900_LoanRejections'
)
BEGIN
    CREATE TABLE [LoanRejections] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [LoanId] uniqueidentifier NOT NULL,
        [DecisionDate] date NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanRejections] PRIMARY KEY ([CompanyId], [Id]),
        CONSTRAINT [FK_LoanRejections_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanRejections_Loans_CompanyId_LoanId] FOREIGN KEY ([CompanyId], [LoanId]) REFERENCES [Loans] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanRejections_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185900_LoanRejections'
)
BEGIN
    CREATE INDEX [IX_LoanRejections_ActorUserId] ON [LoanRejections] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185900_LoanRejections'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LoanRejections_CompanyId_LoanId] ON [LoanRejections] ([CompanyId], [LoanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185900_LoanRejections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001185900_LoanRejections', N'8.0.13');
END;
GO

COMMIT;
GO
