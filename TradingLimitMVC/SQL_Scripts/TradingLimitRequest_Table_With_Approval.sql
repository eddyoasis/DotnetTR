-- Updated CREATE TABLE script for TradingLimitRequests with approval workflow
-- This script includes all the approval email fields for the workflow

-- Create TradingLimitRequests table (Temp_TL_TradingLimitRequests)
CREATE TABLE [dbo].[Temp_TL_TradingLimitRequests] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [RequestId] nvarchar(50) NULL,
    [TRCode] nvarchar(50) NOT NULL,
    [RequestDate] datetime2(7) NOT NULL,
    [LimitEndDate] datetime2(7) NOT NULL,
    [ClientCode] nvarchar(50) NOT NULL,
    [RequestType] nvarchar(100) NOT NULL,
    [BriefDescription] nvarchar(1000) NOT NULL,
    [GLCurrentLimit] decimal(18,2) NOT NULL,
    [GLProposedLimit] decimal(18,2) NOT NULL,
    [CurrentCurrentLimit] decimal(18,2) NOT NULL,
    [CurrentProposedLimit] decimal(18,2) NOT NULL,
    [Status] nvarchar(50) NULL DEFAULT ('Draft'),
    
    -- Audit fields
    [CreatedBy] nvarchar(100) NULL,
    [CreatedDate] datetime2(7) NOT NULL DEFAULT (GETDATE()),
    [ModifiedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2(7) NULL,
    
    -- Submission fields
    [SubmittedBy] nvarchar(100) NULL,
    [SubmittedDate] datetime2(7) NULL,
    
    -- Approval workflow fields
    [ApprovalEmail] nvarchar(200) NULL,
    [ApprovedBy] nvarchar(100) NULL,
    [ApprovedDate] datetime2(7) NULL,
    [ApprovalComments] nvarchar(500) NULL,
    
    CONSTRAINT [PK_Temp_TL_TradingLimitRequests] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Create indexes for performance
CREATE UNIQUE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequests_RequestId] 
ON [dbo].[Temp_TL_TradingLimitRequests] ([RequestId] ASC)
WHERE ([RequestId] IS NOT NULL);

CREATE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequests_TRCode] 
ON [dbo].[Temp_TL_TradingLimitRequests] ([TRCode] ASC);

CREATE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequests_ClientCode] 
ON [dbo].[Temp_TL_TradingLimitRequests] ([ClientCode] ASC);

CREATE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequests_Status] 
ON [dbo].[Temp_TL_TradingLimitRequests] ([Status] ASC);

-- New index for approval workflow
CREATE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequests_ApprovalEmail] 
ON [dbo].[Temp_TL_TradingLimitRequests] ([ApprovalEmail] ASC);

-- Create TradingLimitRequestAttachments table (Temp_TL_TradingLimitRequestAttachments)
CREATE TABLE [dbo].[Temp_TL_TradingLimitRequestAttachments] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [TradingLimitRequestId] int NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [FilePath] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [FileSize] bigint NOT NULL,
    [UploadDate] datetime2(7) NOT NULL DEFAULT (GETDATE()),
    [UploadedBy] nvarchar(100) NULL,
    
    CONSTRAINT [PK_Temp_TL_TradingLimitRequestAttachments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Temp_TL_TradingLimitRequestAttachments_Temp_TL_TradingLimitRequests] 
        FOREIGN KEY ([TradingLimitRequestId]) 
        REFERENCES [dbo].[Temp_TL_TradingLimitRequests] ([Id]) 
        ON DELETE CASCADE
);

-- Create index for foreign key
CREATE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequestAttachments_TradingLimitRequestId] 
ON [dbo].[Temp_TL_TradingLimitRequestAttachments] ([TradingLimitRequestId] ASC);

-- If you need to update an existing table, use these ALTER statements instead:
/*
-- Add approval workflow columns to existing table
ALTER TABLE [dbo].[Temp_TL_TradingLimitRequests] 
ADD 
    [ApprovalEmail] nvarchar(200) NULL,
    [ApprovedBy] nvarchar(100) NULL,
    [ApprovedDate] datetime2(7) NULL,
    [ApprovalComments] nvarchar(500) NULL;

-- Add index for approval email
CREATE NONCLUSTERED INDEX [IX_Temp_TL_TradingLimitRequests_ApprovalEmail] 
ON [dbo].[Temp_TL_TradingLimitRequests] ([ApprovalEmail] ASC);
*/