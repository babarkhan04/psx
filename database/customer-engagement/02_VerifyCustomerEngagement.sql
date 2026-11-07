SELECT DB_NAME() AS DatabaseName, COL_LENGTH(N'dbo.Customers',N'NormalizedPhone') AS NormalizedPhoneColumnBytes,
    COL_LENGTH(N'dbo.Customers',N'WhatsAppNumber') AS WhatsAppColumnBytes,
    COL_LENGTH(N'dbo.Customers',N'DateOfBirth') AS DateOfBirthColumnBytes,
    COL_LENGTH(N'dbo.Customers',N'BirthdayEmailOptIn') AS BirthdayOptInColumnBytes;
SELECT name, is_unique FROM sys.indexes WHERE object_id IN (OBJECT_ID(N'dbo.Customers'), OBJECT_ID(N'dbo.CustomerBirthdayEmails'))
    AND name IN (N'IX_Customers_TenantId_NormalizedPhone', N'IX_CustomerBirthdayEmails_TenantId_CustomerId_BirthdayYear', N'IX_CustomerBirthdayEmails_Status_CreatedOn');
