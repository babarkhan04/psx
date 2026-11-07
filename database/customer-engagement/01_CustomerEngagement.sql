SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
        THROW 51000, 'The existing SmartPOS Customers table was not found. Select the SmartPOS database first.', 1;

    IF COL_LENGTH(N'dbo.Customers', N'NormalizedPhone') IS NULL
        ALTER TABLE dbo.Customers ADD NormalizedPhone nvarchar(20) NULL;
    IF COL_LENGTH(N'dbo.Customers', N'WhatsAppNumber') IS NULL
        ALTER TABLE dbo.Customers ADD WhatsAppNumber nvarchar(40) NULL;
    IF COL_LENGTH(N'dbo.Customers', N'DateOfBirth') IS NULL
        ALTER TABLE dbo.Customers ADD DateOfBirth date NULL;
    IF COL_LENGTH(N'dbo.Customers', N'BirthdayEmailOptIn') IS NULL
        ALTER TABLE dbo.Customers ADD BirthdayEmailOptIn bit NOT NULL CONSTRAINT DF_Customers_BirthdayEmailOptIn DEFAULT (0) WITH VALUES;

    EXEC sys.sp_executesql N'
        UPDATE c SET NormalizedPhone = CASE
            WHEN LEN(n.Digits) BETWEEN 8 AND 15 AND n.Digits NOT LIKE N''%[^0-9]%'' AND LEFT(n.Digits,1) <> N''0'' THEN N''+'' + n.Digits ELSE NULL END
        FROM dbo.Customers c
        CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(c.Phone)),N'' '',N''''),N''('',N''''),N'')'',N''''),N''-'',N''''),N''.'',N''''),N''/'',N''''),N''+'',N'''') AS Digits) raw
        CROSS APPLY (SELECT CASE
            WHEN LEFT(raw.Digits,2)=N''00'' THEN SUBSTRING(raw.Digits,3,40)
            WHEN LEFT(raw.Digits,1)=N''0'' THEN N''92''+SUBSTRING(raw.Digits,2,40)
            WHEN LEN(raw.Digits)=10 AND LEFT(raw.Digits,2)<>N''92'' AND LEFT(LTRIM(c.Phone),1)<>N''+'' THEN N''92''+raw.Digits
            ELSE raw.Digits END AS Digits) n
        WHERE c.Phone IS NOT NULL AND c.NormalizedPhone IS NULL;';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Customers') AND name=N'IX_Customers_TenantId_NormalizedPhone')
        EXEC sys.sp_executesql N'CREATE INDEX IX_Customers_TenantId_NormalizedPhone ON dbo.Customers(TenantId, NormalizedPhone);';

    IF OBJECT_ID(N'dbo.CustomerBirthdayEmails', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CustomerBirthdayEmails
        (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerBirthdayEmails PRIMARY KEY,
            CustomerId uniqueidentifier NOT NULL,
            BirthdayYear int NOT NULL,
            Recipient nvarchar(200) NOT NULL,
            Subject nvarchar(200) NOT NULL,
            Body nvarchar(5000) NOT NULL,
            Status nvarchar(20) NOT NULL,
            Attempts int NOT NULL,
            SentOn datetime2 NULL,
            LeaseUntil datetime2 NULL,
            LastError nvarchar(500) NULL,
            TenantId uniqueidentifier NOT NULL,
            CreatedOn datetime2 NOT NULL,
            CreatedBy nvarchar(max) NULL,
            ModifiedOn datetime2 NULL,
            ModifiedBy nvarchar(max) NULL,
            IsDeleted bit NOT NULL,
            CONSTRAINT FK_CustomerBirthdayEmails_Customers_CustomerId FOREIGN KEY(CustomerId) REFERENCES dbo.Customers(Id)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerBirthdayEmails') AND name=N'IX_CustomerBirthdayEmails_TenantId_CustomerId_BirthdayYear')
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX IX_CustomerBirthdayEmails_TenantId_CustomerId_BirthdayYear ON dbo.CustomerBirthdayEmails(TenantId, CustomerId, BirthdayYear);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerBirthdayEmails') AND name=N'IX_CustomerBirthdayEmails_Status_CreatedOn')
        EXEC sys.sp_executesql N'CREATE INDEX IX_CustomerBirthdayEmails_Status_CreatedOn ON dbo.CustomerBirthdayEmails(Status, CreatedOn);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerBirthdayEmails') AND name=N'IX_CustomerBirthdayEmails_CustomerId')
        EXEC sys.sp_executesql N'CREATE INDEX IX_CustomerBirthdayEmails_CustomerId ON dbo.CustomerBirthdayEmails(CustomerId);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
