-- ============================================================
-- Shree Jewellers — SQL Server Maintenance Scripts
-- Run these via SQL Server Agent jobs or scheduled tasks
-- ============================================================

USE ShreeJewellers;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. INDEX MAINTENANCE STORED PROCEDURE
--    Rebuilds fragmented indexes; reorganizes slightly fragmented ones.
--    Run weekly during off-hours (Sunday 2 AM IST).
-- ─────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.usp_MaintainIndexes
    @FragmentationThresholdReorganize FLOAT = 10.0,
    @FragmentationThresholdRebuild    FLOAT = 30.0,
    @MinPageCount                     INT   = 100
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SchemaName    NVARCHAR(128),
            @TableName     NVARCHAR(128),
            @IndexName     NVARCHAR(128),
            @Fragmentation FLOAT,
            @SQL           NVARCHAR(MAX),
            @StartTime     DATETIME2 = SYSDATETIME();

    DECLARE index_cursor CURSOR FAST_FORWARD FOR
        SELECT
            s.name           AS SchemaName,
            o.name           AS TableName,
            i.name           AS IndexName,
            ips.avg_fragmentation_in_percent AS Fragmentation
        FROM sys.dm_db_index_physical_stats(
            DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
        JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
        JOIN sys.objects o ON i.object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE ips.avg_fragmentation_in_percent > @FragmentationThresholdReorganize
          AND ips.page_count > @MinPageCount
          AND i.type_desc IN ('CLUSTERED','NONCLUSTERED')
          AND o.type = 'U'
        ORDER BY ips.avg_fragmentation_in_percent DESC;

    OPEN index_cursor;
    FETCH NEXT FROM index_cursor INTO @SchemaName, @TableName, @IndexName, @Fragmentation;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Fragmentation >= @FragmentationThresholdRebuild
        BEGIN
            SET @SQL = N'ALTER INDEX ' + QUOTENAME(@IndexName) +
                       N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
                       N' REBUILD WITH (ONLINE = ON, FILLFACTOR = 80, STATISTICS_NORECOMPUTE = OFF);';
            PRINT 'REBUILD: ' + @TableName + '.' + @IndexName +
                  ' (' + CAST(ROUND(@Fragmentation,1) AS VARCHAR) + '% fragmented)';
        END
        ELSE
        BEGIN
            SET @SQL = N'ALTER INDEX ' + QUOTENAME(@IndexName) +
                       N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
                       N' REORGANIZE;';
            PRINT 'REORGANIZE: ' + @TableName + '.' + @IndexName +
                  ' (' + CAST(ROUND(@Fragmentation,1) AS VARCHAR) + '% fragmented)';
        END

        BEGIN TRY
            EXEC sp_executesql @SQL;
        END TRY
        BEGIN CATCH
            PRINT 'ERROR on ' + @TableName + '.' + @IndexName + ': ' + ERROR_MESSAGE();
        END CATCH;

        FETCH NEXT FROM index_cursor INTO @SchemaName, @TableName, @IndexName, @Fragmentation;
    END;

    CLOSE index_cursor;
    DEALLOCATE index_cursor;

    -- Update statistics for all user tables
    EXEC sp_updatestats;

    PRINT 'Index maintenance completed in ' +
          CAST(DATEDIFF(SECOND, @StartTime, SYSDATETIME()) AS VARCHAR) + ' seconds.';
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. DAILY BACKUP JOB SCRIPT
--    Full backup on Sunday, Differential Mon-Sat.
--    Transaction log backup every 4 hours.
--    Deletes backups older than 30 days.
-- ─────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.usp_DailyBackup
    @BackupPath     NVARCHAR(500) = N'/var/opt/mssql/backup',
    @FullBackupDay  INT           = 1    -- 1 = Sunday
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FileName   NVARCHAR(1000),
            @DateStr    NVARCHAR(20) = FORMAT(GETDATE(), 'yyyyMMdd_HHmmss'),
            @DayOfWeek  INT          = DATEPART(WEEKDAY, GETDATE());

    IF @DayOfWeek = @FullBackupDay
    BEGIN
        -- Full backup on Sunday
        SET @FileName = @BackupPath + N'/ShreeJewellers_FULL_' + @DateStr + N'.bak';
        BACKUP DATABASE ShreeJewellers
            TO DISK = @FileName
            WITH COMPRESSION, CHECKSUM, STATS = 10,
                 DESCRIPTION = N'Weekly Full Backup - ' + @DateStr;
        PRINT 'Full backup created: ' + @FileName;
    END
    ELSE
    BEGIN
        -- Differential backup Mon-Sat
        SET @FileName = @BackupPath + N'/ShreeJewellers_DIFF_' + @DateStr + N'.bak';
        BACKUP DATABASE ShreeJewellers
            TO DISK = @FileName
            WITH DIFFERENTIAL, COMPRESSION, CHECKSUM, STATS = 10,
                 DESCRIPTION = N'Daily Differential Backup - ' + @DateStr;
        PRINT 'Differential backup created: ' + @FileName;
    END;

    -- Clean up backups older than 30 days
    DECLARE @CutoffDate DATETIME = DATEADD(DAY, -30, GETDATE());
    EXEC master.dbo.xp_delete_file
        0,                                    -- 0 = backup files
        @BackupPath,                          -- folder
        N'bak',                               -- extension
        @CutoffDate,                          -- delete before this date
        1;                                    -- include sub-folders

    PRINT 'Backup cleanup completed (files older than 30 days removed).';
END;
GO

-- Transaction log backup (run every 4 hours via SQL Agent)
CREATE OR ALTER PROCEDURE dbo.usp_LogBackup
    @BackupPath NVARCHAR(500) = N'/var/opt/mssql/backup/logs'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FileName NVARCHAR(1000) =
        @BackupPath + N'/ShreeJewellers_LOG_' + FORMAT(GETDATE(), 'yyyyMMdd_HHmmss') + N'.trn';

    BACKUP LOG ShreeJewellers
        TO DISK = @FileName
        WITH COMPRESSION, CHECKSUM, STATS = 25;

    PRINT 'Log backup: ' + @FileName;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. DATA ARCHIVAL — Move old closed loans to archive table
--    Run annually (1st January). Loans closed 5+ years ago.
--    Keeps main tables lean for query performance.
-- ─────────────────────────────────────────────────────────────

-- Create archive tables (run once on fresh setup)
IF OBJECT_ID('dbo.GoldLoans_Archive') IS NULL
BEGIN
    SELECT TOP 0 * INTO dbo.GoldLoans_Archive FROM dbo.GoldLoans;
    ALTER TABLE dbo.GoldLoans_Archive ADD ArchivedAt DATETIME2 DEFAULT SYSDATETIME();

    SELECT TOP 0 * INTO dbo.GoldLoanItems_Archive FROM dbo.GoldLoanItems;
    ALTER TABLE dbo.GoldLoanItems_Archive ADD ArchivedAt DATETIME2 DEFAULT SYSDATETIME();

    SELECT TOP 0 * INTO dbo.LoanRepayments_Archive FROM dbo.LoanRepayments;
    ALTER TABLE dbo.LoanRepayments_Archive ADD ArchivedAt DATETIME2 DEFAULT SYSDATETIME();

    SELECT TOP 0 * INTO dbo.SalesOrders_Archive FROM dbo.SalesOrders;
    ALTER TABLE dbo.SalesOrders_Archive ADD ArchivedAt DATETIME2 DEFAULT SYSDATETIME();

    PRINT 'Archive tables created.';
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ArchiveOldData
    @ArchiveBeforeYears INT = 5,
    @BatchSize          INT = 1000,
    @DryRun             BIT = 1      -- Set to 0 to actually archive
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CutoffDate  DATE = DATEADD(YEAR, -@ArchiveBeforeYears, GETDATE()),
            @LoansArchived   INT = 0,
            @OrdersArchived  INT = 0;

    PRINT 'Archive cutoff date: ' + CAST(@CutoffDate AS VARCHAR);
    PRINT 'Mode: ' + CASE WHEN @DryRun = 1 THEN 'DRY RUN (no changes)' ELSE 'LIVE (changes committed)' END;

    -- Preview counts
    DECLARE @LoanCount INT, @OrderCount INT;

    SELECT @LoanCount = COUNT(*) FROM dbo.GoldLoans
    WHERE LoanStatus IN (2, 3)    -- Closed (2) or Defaulted (3)
      AND ClosedDate < @CutoffDate;

    SELECT @OrderCount = COUNT(*) FROM dbo.SalesOrders
    WHERE Status IN (2, 4)        -- Delivered (2) or Returned (4)
      AND OrderDate < CAST(@CutoffDate AS DATETIME2);

    PRINT 'Loans eligible for archive: ' + CAST(@LoanCount AS VARCHAR);
    PRINT 'Orders eligible for archive: ' + CAST(@OrderCount AS VARCHAR);

    IF @DryRun = 1
    BEGIN
        PRINT 'DRY RUN: No records moved. Re-run with @DryRun = 0 to archive.';
        RETURN;
    END;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Archive Gold Loan Items first (FK dependency)
        INSERT INTO dbo.GoldLoanItems_Archive
        SELECT gli.*, SYSDATETIME()
        FROM dbo.GoldLoanItems gli
        JOIN dbo.GoldLoans gl ON gli.GoldLoanId = gl.Id
        WHERE gl.LoanStatus IN (2, 3) AND gl.ClosedDate < @CutoffDate;

        -- Archive Repayments
        INSERT INTO dbo.LoanRepayments_Archive
        SELECT lr.*, SYSDATETIME()
        FROM dbo.LoanRepayments lr
        JOIN dbo.GoldLoans gl ON lr.GoldLoanId = gl.Id
        WHERE gl.LoanStatus IN (2, 3) AND gl.ClosedDate < @CutoffDate;

        -- Archive Gold Loans
        INSERT INTO dbo.GoldLoans_Archive
        SELECT *, SYSDATETIME() FROM dbo.GoldLoans
        WHERE LoanStatus IN (2, 3) AND ClosedDate < @CutoffDate;

        SET @LoansArchived = @@ROWCOUNT;

        DELETE gli FROM dbo.GoldLoanItems gli
        JOIN dbo.GoldLoans gl ON gli.GoldLoanId = gl.Id
        WHERE gl.LoanStatus IN (2, 3) AND gl.ClosedDate < @CutoffDate;

        DELETE lr FROM dbo.LoanRepayments lr
        JOIN dbo.GoldLoans gl ON lr.GoldLoanId = gl.Id
        WHERE gl.LoanStatus IN (2, 3) AND gl.ClosedDate < @CutoffDate;

        DELETE FROM dbo.GoldLoans
        WHERE LoanStatus IN (2, 3) AND ClosedDate < @CutoffDate;

        -- Archive Sales Orders
        INSERT INTO dbo.SalesOrders_Archive
        SELECT *, SYSDATETIME() FROM dbo.SalesOrders
        WHERE Status IN (2, 4) AND OrderDate < CAST(@CutoffDate AS DATETIME2);

        SET @OrdersArchived = @@ROWCOUNT;

        DELETE FROM dbo.SalesOrders
        WHERE Status IN (2, 4) AND OrderDate < CAST(@CutoffDate AS DATETIME2);

        -- Archive old audit logs (keep 2 years)
        DELETE FROM dbo.AuditLogs
        WHERE Timestamp < DATEADD(YEAR, -2, GETDATE());

        COMMIT TRANSACTION;

        PRINT 'Archive complete. Loans: ' + CAST(@LoansArchived AS VARCHAR) +
              ', Orders: ' + CAST(@OrdersArchived AS VARCHAR);

        -- Log the archival operation
        INSERT INTO dbo.AuditLogs (UserId, Action, EntityName, NewValues, Timestamp)
        VALUES (NULL, 'DATA_ARCHIVE', 'Multiple',
                '{"LoansArchived":' + CAST(@LoansArchived AS VARCHAR) +
                ',"OrdersArchived":' + CAST(@OrdersArchived AS VARCHAR) +
                ',"CutoffDate":"' + CAST(@CutoffDate AS VARCHAR) + '"}',
                SYSDATETIME());
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        PRINT 'ERROR: ' + ERROR_MESSAGE();
        THROW;
    END CATCH;
END;
GO

PRINT 'All maintenance procedures created successfully.';

-- ─────────────────────────────────────────────────────────────
-- 4. HEALTH CHECK QUERY (used by /health/db endpoint)
-- ─────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.usp_HealthCheck
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        DB_NAME()                           AS DatabaseName,
        @@VERSION                           AS SqlVersion,
        GETUTCDATE()                        AS ServerTimeUtc,
        (SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1)          AS ActiveUsers,
        (SELECT COUNT(*) FROM dbo.GoldLoans WHERE LoanStatus IN (0,1,4)) AS ActiveLoans,
        (SELECT COUNT(*) FROM dbo.Products WHERE IsActive = 1)       AS ActiveProducts,
        'Healthy'                           AS Status;
END;
GO
