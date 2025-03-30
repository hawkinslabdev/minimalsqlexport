CREATE PROCEDURE dbo.LogExportActivity (
    @ExportType VARCHAR(50),
    @FilePath VARCHAR(500),
    @RecordCount INT,
    @ExportTime DATETIME,
    @UserName VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorCode INT = 0;
    
    BEGIN TRY
        -- Create the log table if it doesn't exist
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ExportLogs')
        BEGIN
            CREATE TABLE dbo.ExportLogs (
                LogID INT IDENTITY(1,1) PRIMARY KEY,
                ExportType VARCHAR(50) NOT NULL,
                FilePath VARCHAR(500) NOT NULL,
                RecordCount INT NOT NULL,
                ExportTime DATETIME NOT NULL,
                UserName VARCHAR(100) NOT NULL,
                LoggedAt DATETIME DEFAULT GETDATE()
            );
        END
        
        -- Log the export activity
        INSERT INTO dbo.ExportLogs (
            ExportType,
            FilePath,
            RecordCount,
            ExportTime,
            UserName
        ) VALUES (
            @ExportType,
            @FilePath,
            @RecordCount,
            @ExportTime,
            @UserName
        );
        
        -- You could add additional logic here:
        -- - Update a status table to mark the export as complete
        -- - Insert a notification record for alerting users
        -- - Trigger additional processing
        
        PRINT 'Export activity logged successfully: ' + @ExportType + ' with ' + CAST(@RecordCount AS VARCHAR(10)) + ' records';
    END TRY
    BEGIN CATCH
        SET @ErrorCode = ERROR_NUMBER();
        
        -- Log the error
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        PRINT 'Error logging export activity: ' + @ErrorMessage;
        
        -- Optionally, you could insert into an error log table here
        
        -- Return the error code
        RETURN @ErrorCode;
    END CATCH
    
    -- Return 0 for success
    RETURN @ErrorCode;
END