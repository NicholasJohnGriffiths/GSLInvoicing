SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.[Transaction]', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Transaction]
    (
        Id INT IDENTITY(1,1) NOT NULL,
        ClientID INT NOT NULL,
        TransDate DATETIME NOT NULL,
        TransType VARCHAR(10) NOT NULL,
        Title VARCHAR(255) NULL,
        Detail VARCHAR(255) NULL,
        Particulars VARCHAR(255) NULL,
        Code VARCHAR(255) NULL,
        Reference VARCHAR(255) NULL,
        Amount MONEY NOT NULL,
        AccountNumber VARCHAR(255) NULL,

        CONSTRAINT PK_Transaction PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Transaction_Client FOREIGN KEY (ClientID)
            REFERENCES dbo.Client (Id),
        CONSTRAINT CK_Transaction_TransType
            CHECK (TransType IN ('Credit', 'Debit'))
    );

    CREATE INDEX IX_Transaction_ClientID ON dbo.[Transaction] (ClientID);
    CREATE INDEX IX_Transaction_TransDate ON dbo.[Transaction] (TransDate);
END
GO
