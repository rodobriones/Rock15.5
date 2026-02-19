/*
<doc>
    <summary>
        Migration script for Custom Giving Entry Obsidian Block
    </summary>
    <remarks>
        This script registers the CustomGivingEntry BlockType and all its attributes.

        Prerequisites:
        - Rock RMS 15.5 or later
        - Rock.Blocks.CustomGiving.CustomGivingEntry.cs deployed to Rock.Blocks
        - CustomGivingEntry.obs deployed to Rock.JavaScript.Obsidian.Blocks

        Instructions:
        1. Deploy the C# and .obs files first
        2. Execute this SQL script against your Rock database
        3. Verify the block appears in CMS > Block Types

        Rollback:
        - To remove, delete the BlockType record (it will cascade delete attributes)
        - Or use the Down() section if implementing as C# migration
    </remarks>
</doc>
*/

BEGIN TRANSACTION

DECLARE @EntityTypeId INT
DECLARE @BlockTypeId INT
DECLARE @FieldTypeId_FinancialGateway INT
DECLARE @FieldTypeId_Boolean INT
DECLARE @FieldTypeId_Text INT
DECLARE @FieldTypeId_DefinedValue INT
DECLARE @FieldTypeId_AccountsField INT
DECLARE @FieldTypeId_CodeEditor INT
DECLARE @FieldTypeId_SystemCommunication INT
DECLARE @BlockTypeGuid UNIQUEIDENTIFIER = '8E9A4B02-5F4C-4E8D-9B3A-6E7C8D9F1A2B' -- Custom GUID for this block type

-- Get FieldType IDs
SELECT @FieldTypeId_FinancialGateway = Id FROM FieldType WHERE [Guid] = 'D4C06F4F-FCEE-4C84-8DAC-A3C073F9BFCA' -- FinancialGatewayFieldType
SELECT @FieldTypeId_Boolean = Id FROM FieldType WHERE [Guid] = '1EDAFDED-DFE6-4334-B019-6EECBA89E05A' -- BooleanFieldType
SELECT @FieldTypeId_Text = Id FROM FieldType WHERE [Guid] = '9C204CD0-1233-41C5-818A-C5DA439445AA' -- TextFieldType
SELECT @FieldTypeId_DefinedValue = Id FROM FieldType WHERE [Guid] = 'BC48720C-3610-4BCF-AE66-D255A17F1CDF' -- DefinedValueFieldType
SELECT @FieldTypeId_AccountsField = Id FROM FieldType WHERE [Guid] = '17033CDD-EF97-4413-A483-7B85A787A87F' -- AccountsFieldType
SELECT @FieldTypeId_CodeEditor = Id FROM FieldType WHERE [Guid] = '1D0D3794-C210-48A8-8C68-3FBEC08A6BA5' -- CodeEditorFieldType
SELECT @FieldTypeId_SystemCommunication = Id FROM FieldType WHERE [Guid] = '80A8C783-EC91-4FBD-9E28-2A09691B5380' -- SystemCommunicationFieldType

-- Register EntityType for the block
IF NOT EXISTS (SELECT 1 FROM EntityType WHERE [Name] = 'Rock.Blocks.CustomGiving.CustomGivingEntry')
BEGIN
    INSERT INTO EntityType ([Name], [FriendlyName], [IsEntity], [IsSecured], [IsCommon], [Guid])
    VALUES (
        'Rock.Blocks.CustomGiving.CustomGivingEntry',
        'Custom Giving Entry',
        0,
        0,
        0,
        NEWID()
    )
END

SELECT @EntityTypeId = Id FROM EntityType WHERE [Name] = 'Rock.Blocks.CustomGiving.CustomGivingEntry'

-- Register BlockType
IF NOT EXISTS (SELECT 1 FROM BlockType WHERE [Guid] = @BlockTypeGuid)
BEGIN
    INSERT INTO BlockType (
        [IsSystem],
        [Path],
        [Category],
        [Name],
        [Description],
        [EntityTypeId],
        [Guid]
    )
    VALUES (
        0, -- IsSystem (false for custom blocks)
        '~/Blocks/CustomGiving/CustomGivingEntry.obs', -- Virtual path
        'Custom', -- Category
        'Custom Giving Entry', -- Name
        'Custom Obsidian Giving block with native Rock transaction processing.', -- Description
        @EntityTypeId,
        @BlockTypeGuid
    )

    PRINT 'BlockType created successfully'
END
ELSE
BEGIN
    PRINT 'BlockType already exists'
END

SELECT @BlockTypeId = Id FROM BlockType WHERE [Guid] = @BlockTypeGuid

-- Create Block Attributes
DECLARE @Order INT = 0

-- Financial Gateway
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''FinancialGateway'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem],
            [FieldTypeId],
            [EntityTypeId],
            [EntityTypeQualifierColumn],
            [EntityTypeQualifierValue],
            [Key],
            [Name],
            [Description],
            [Order],
            [IsGridColumn],
            [IsMultiValue],
            [IsRequired],
            [Guid]
        )
        VALUES (
            0,
            @FieldTypeId_FinancialGateway,
            @EntityTypeId,
            ''BlockTypeId'',
            CAST(@BlockTypeId AS NVARCHAR(10)),
            ''FinancialGateway'',
            ''Financial Gateway'',
            ''The payment gateway to use for credit card and ACH transactions.'',
            0,
            0,
            0,
            0,
            NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_FinancialGateway INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_FinancialGateway, @BlockTypeId

-- Enable ACH
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''EnableACH'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Boolean, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''EnableACH'', ''Enable ACH'', '''', 1, 0, 0, 0,
            ''True'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Boolean INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Boolean, @BlockTypeId

-- Enable Credit Card
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''EnableCreditCard'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Boolean, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''EnableCreditCard'', ''Enable Credit Card'', '''', 2, 0, 0, 0,
            ''True'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Boolean INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Boolean, @BlockTypeId

-- Batch Name Prefix
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''BatchNamePrefix'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Text, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''BatchNamePrefix'', ''Batch Name Prefix'', ''The batch prefix name to use when creating a new batch.'', 3, 0, 0, 0,
            ''Online Giving'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Text INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Text, @BlockTypeId

-- Source (DefinedValue)
DECLARE @SourceDefaultGuid NVARCHAR(50) = 'BE7ECF50-52BC-4774-808D-574BA842DB98' -- FINANCIAL_SOURCE_TYPE_WEBSITE

EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''Source'')
    BEGIN
        DECLARE @AttributeGuid UNIQUEIDENTIFIER = NEWID()
        DECLARE @AttributeId INT

        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [Guid]
        )
        VALUES (
            0, @FieldTypeId_DefinedValue, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''Source'', ''Source'', ''The Financial Source Type to use when creating transactions.'', 4, 0, 0, 0,
            @AttributeGuid
        )

        SET @AttributeId = SCOPE_IDENTITY()

        -- Add qualifier for DefinedType
        INSERT INTO AttributeQualifier ([IsSystem], [AttributeId], [Key], [Value], [Guid])
        VALUES (0, @AttributeId, ''definedtype'', ''4F02B41E-AB7D-4345-8A97-3904DDD89B01'', NEWID()) -- FINANCIAL_SOURCE_TYPE

        -- Set default value
        DECLARE @SourceDefaultValueId INT = (SELECT TOP 1 Id FROM DefinedValue WHERE [Guid] = @SourceDefaultGuid)
        IF @SourceDefaultValueId IS NOT NULL
        BEGIN
            UPDATE Attribute SET [DefaultValue] = CAST(@SourceDefaultValueId AS NVARCHAR(10)) WHERE Id = @AttributeId
        END
    END',
    N'@EntityTypeId INT, @FieldTypeId_DefinedValue INT, @BlockTypeId INT, @SourceDefaultGuid NVARCHAR(50)',
    @EntityTypeId, @FieldTypeId_DefinedValue, @BlockTypeId, @SourceDefaultGuid

-- Accounts
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''AccountsToDisplay'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [Guid]
        )
        VALUES (
            0, @FieldTypeId_AccountsField, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''AccountsToDisplay'', ''Accounts'', ''The accounts available to donors in this block.'', 5, 0, 0, 0,
            NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_AccountsField INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_AccountsField, @BlockTypeId

-- Allow Scheduled
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''AllowScheduled'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Boolean, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''AllowScheduled'', ''Allow Scheduled Transactions'', ''Allows repeating scheduled gifts if the gateway supports them.'', 6, 0, 0, 0,
            ''True'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Boolean INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Boolean, @BlockTypeId

-- Enable Business Giving
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''EnableBusinessGiving'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Boolean, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''EnableBusinessGiving'', ''Enable Business Giving'', ''Allow donor to give as a business.'', 7, 0, 0, 0,
            ''True'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Boolean INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Boolean, @BlockTypeId

-- Enable Anonymous Giving
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''EnableAnonymousGiving'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Boolean, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''EnableAnonymousGiving'', ''Enable Anonymous Giving'', ''Allow donor to mark gift as anonymous.'', 8, 0, 0, 0,
            ''False'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Boolean INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Boolean, @BlockTypeId

-- Enable Comment Entry
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''EnableCommentEntry'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Boolean, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''EnableCommentEntry'', ''Enable Comment Entry'', ''Allow donor to provide a comment.'', 9, 0, 0, 0,
            ''False'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Boolean INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Boolean, @BlockTypeId

-- Comment Entry Label
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''CommentEntryLabel'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [DefaultValue], [Guid]
        )
        VALUES (
            0, @FieldTypeId_Text, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''CommentEntryLabel'', ''Comment Entry Label'', ''Label for donor comment field.'', 10, 0, 0, 0,
            ''Comment'', NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_Text INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_Text, @BlockTypeId

-- Payment Comment Template
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''PaymentCommentTemplate'')
    BEGIN
        DECLARE @AttributeGuid UNIQUEIDENTIFIER = NEWID()
        DECLARE @AttributeId INT

        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [Guid]
        )
        VALUES (
            0, @FieldTypeId_CodeEditor, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''PaymentCommentTemplate'', ''Payment Comment Template'', ''Comment template sent to gateway.'', 11, 0, 0, 0,
            @AttributeGuid
        )

        SET @AttributeId = SCOPE_IDENTITY()

        -- Add qualifier for code editor mode
        INSERT INTO AttributeQualifier ([IsSystem], [AttributeId], [Key], [Value], [Guid])
        VALUES (0, @AttributeId, ''editorMode'', ''4'', NEWID()) -- Lava = 4
    END',
    N'@EntityTypeId INT, @FieldTypeId_CodeEditor INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_CodeEditor, @BlockTypeId

-- Receipt Email
EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''ReceiptEmail'')
    BEGIN
        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [Guid]
        )
        VALUES (
            0, @FieldTypeId_SystemCommunication, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''ReceiptEmail'', ''Receipt Email'', ''System communication used to send receipts.'', 12, 0, 0, 0,
            NEWID()
        )
    END',
    N'@EntityTypeId INT, @FieldTypeId_SystemCommunication INT, @BlockTypeId INT',
    @EntityTypeId, @FieldTypeId_SystemCommunication, @BlockTypeId

-- Person Connection Status
DECLARE @ConnectionStatusProspectGuid NVARCHAR(50) = '39F491C5-D6AC-4A9B-8AC0-C431CB17D588' -- PERSON_CONNECTION_STATUS_PROSPECT

EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''PersonConnectionStatus'')
    BEGIN
        DECLARE @AttributeGuid UNIQUEIDENTIFIER = NEWID()
        DECLARE @AttributeId INT

        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [Guid]
        )
        VALUES (
            0, @FieldTypeId_DefinedValue, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''PersonConnectionStatus'', ''Connection Status'', ''Default connection status for new persons created through this block.'', 13, 0, 0, 1,
            @AttributeGuid
        )

        SET @AttributeId = SCOPE_IDENTITY()

        -- Add qualifier for DefinedType
        INSERT INTO AttributeQualifier ([IsSystem], [AttributeId], [Key], [Value], [Guid])
        VALUES (0, @AttributeId, ''definedtype'', ''2E6540EA-63F0-40FE-BE50-F2A84735E600'', NEWID()) -- PERSON_CONNECTION_STATUS

        -- Set default value
        DECLARE @ConnectionDefaultValueId INT = (SELECT TOP 1 Id FROM DefinedValue WHERE [Guid] = @ConnectionStatusProspectGuid)
        IF @ConnectionDefaultValueId IS NOT NULL
        BEGIN
            UPDATE Attribute SET [DefaultValue] = CAST(@ConnectionDefaultValueId AS NVARCHAR(10)) WHERE Id = @AttributeId
        END
    END',
    N'@EntityTypeId INT, @FieldTypeId_DefinedValue INT, @BlockTypeId INT, @ConnectionStatusProspectGuid NVARCHAR(50)',
    @EntityTypeId, @FieldTypeId_DefinedValue, @BlockTypeId, @ConnectionStatusProspectGuid

-- Person Record Status
DECLARE @RecordStatusPendingGuid NVARCHAR(50) = '283999EC-7346-42E3-B807-BCE9B2BABB49' -- PERSON_RECORD_STATUS_PENDING

EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [EntityTypeId] = @EntityTypeId AND [Key] = ''PersonRecordStatus'')
    BEGIN
        DECLARE @AttributeGuid UNIQUEIDENTIFIER = NEWID()
        DECLARE @AttributeId INT

        INSERT INTO Attribute (
            [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
            [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
            [Guid]
        )
        VALUES (
            0, @FieldTypeId_DefinedValue, @EntityTypeId, ''BlockTypeId'', CAST(@BlockTypeId AS NVARCHAR(10)),
            ''PersonRecordStatus'', ''Record Status'', ''Default record status for new persons created through this block.'', 14, 0, 0, 1,
            @AttributeGuid
        )

        SET @AttributeId = SCOPE_IDENTITY()

        -- Add qualifier for DefinedType
        INSERT INTO AttributeQualifier ([IsSystem], [AttributeId], [Key], [Value], [Guid])
        VALUES (0, @AttributeId, ''definedtype'', ''8522BADD-2871-45A5-81DD-C76DA07E2E7E'', NEWID()) -- PERSON_RECORD_STATUS

        -- Set default value
        DECLARE @RecordDefaultValueId INT = (SELECT TOP 1 Id FROM DefinedValue WHERE [Guid] = @RecordStatusPendingGuid)
        IF @RecordDefaultValueId IS NOT NULL
        BEGIN
            UPDATE Attribute SET [DefaultValue] = CAST(@RecordDefaultValueId AS NVARCHAR(10)) WHERE Id = @AttributeId
        END
    END',
    N'@EntityTypeId INT, @FieldTypeId_DefinedValue INT, @BlockTypeId INT, @RecordStatusPendingGuid NVARCHAR(50)',
    @EntityTypeId, @FieldTypeId_DefinedValue, @BlockTypeId, @RecordStatusPendingGuid

COMMIT TRANSACTION

PRINT 'Custom Giving Entry BlockType migration completed successfully'
PRINT ''
PRINT 'Next steps:'
PRINT '1. Navigate to CMS > Block Types in Rock'
PRINT '2. Search for "Custom Giving Entry"'
PRINT '3. Add the block to a page to configure it'
PRINT '4. Configure the Financial Gateway and Accounts attributes'
