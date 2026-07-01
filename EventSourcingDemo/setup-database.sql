-- ═══════════════════════════════════════════════════════════
-- Event Store — SQL Server Setup
-- ═══════════════════════════════════════════════════════════
-- Run this script once to create the database and table.
-- You can use SQL Server Management Studio, Azure Data Studio,
-- or the sqlcmd command line tool.

-- Create database (skip if using an existing database)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'EventSourcingDemo')
BEGIN
    CREATE DATABASE EventSourcingDemo;
END
GO

USE EventSourcingDemo;
GO

-- Create the Events table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Events')
BEGIN
    CREATE TABLE Events (
        -- Auto-incrementing ID for global ordering
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Which aggregate this event belongs to
        AggregateId     UNIQUEIDENTIFIER NOT NULL,

        -- The version number within this aggregate (0, 1, 2, ...)
        -- Used for optimistic concurrency
        Version         INT NOT NULL,

        -- The event type name (e.g., "OrderPlaced", "OrderAccepted")
        EventType       NVARCHAR(200) NOT NULL,

        -- The event payload as JSON
        Data            NVARCHAR(MAX) NOT NULL,

        -- When this event was stored
        Timestamp       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        -- CRITICAL: This constraint prevents two events with the same
        -- version for the same aggregate. If two requests try to save
        -- version 1 simultaneously, only one succeeds.
        CONSTRAINT UQ_Aggregate_Version UNIQUE (AggregateId, Version)
    );

    -- Index for loading events by aggregate (the most common query)
    CREATE INDEX IX_Events_AggregateId
        ON Events (AggregateId, Version);

    -- Index for global event ordering (useful for projections)
    CREATE INDEX IX_Events_Id
        ON Events (Id);

    PRINT 'Events table created successfully.';
END
ELSE
BEGIN
    PRINT 'Events table already exists.';
END
GO
