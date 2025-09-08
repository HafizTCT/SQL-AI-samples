# Simple test for stored procedure functionality

Write-Host "Testing Stored Procedure Creation and MCP Tools..." -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Cyan

# Set the connection string
$env:CONNECTION_STRING = "Server=tctapps-development.database.windows.net;Database=TCTBackOffice;User Id=tctdev;Password=TCTapps4pp5;"

try {
    # First, create a test stored procedure using sqlcmd
    Write-Host "Creating test stored procedure 'cursor_test' using sqlcmd..." -ForegroundColor Yellow
    
    $createProcSQL = @"
CREATE OR ALTER PROCEDURE dbo.cursor_test
    @testParam VARCHAR(50) = 'Default Value'
AS
BEGIN
    -- Test stored procedure created for MCP testing
    SELECT 
        'Hello from cursor_test stored procedure!' AS Message,
        @testParam AS InputParameter,
        GETDATE() AS CurrentDateTime,
        @@VERSION AS ServerVersion,
        DB_NAME() AS DatabaseName
END
"@
    
    $createResult = sqlcmd -S "tctapps-development.database.windows.net" -d "TCTBackOffice" -U "tctdev" -P "TCTapps4pp5" -Q $createProcSQL -h -1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Stored procedure 'cursor_test' created successfully!" -ForegroundColor Green
        
        # Test executing the stored procedure
        Write-Host "`nTesting execution of the stored procedure..." -ForegroundColor Yellow
        $execResult = sqlcmd -S "tctapps-development.database.windows.net" -d "TCTBackOffice" -U "tctdev" -P "TCTapps4pp5" -Q "EXEC cursor_test 'Test Parameter from MCP'" -h -1 -W
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Stored procedure executed successfully!" -ForegroundColor Green
            Write-Host "Execution Result:" -ForegroundColor White
            Write-Host $execResult -ForegroundColor Gray
        } else {
            Write-Host "❌ Failed to execute stored procedure" -ForegroundColor Red
        }
        
        # Now test the MCP ListStoredProcedures tool
        Write-Host "`nTesting MCP ListStoredProcedures tool..." -ForegroundColor Yellow
        
        # Create a simple test for listing procedures
        $listRequest = @{
            jsonrpc = "2.0"
            id = 2
            method = "tools/call"
            params = @{
                name = "ListStoredProcedures"
                arguments = @{
                    procedureName = "cursor_test"
                }
            }
        } | ConvertTo-Json -Depth 10
        
        Write-Host "ListStoredProcedures request:" -ForegroundColor Cyan
        Write-Host $listRequest -ForegroundColor Gray
        
        # Test GetStoredProcedureDefinition tool
        Write-Host "`nTesting MCP GetStoredProcedureDefinition tool..." -ForegroundColor Yellow
        
        $getDefRequest = @{
            jsonrpc = "2.0"
            id = 3
            method = "tools/call"
            params = @{
                name = "GetStoredProcedureDefinition"
                arguments = @{
                    procedureName = "cursor_test"
                    schema = "dbo"
                }
            }
        } | ConvertTo-Json -Depth 10
        
        Write-Host "GetStoredProcedureDefinition request:" -ForegroundColor Cyan
        Write-Host $getDefRequest -ForegroundColor Gray
        
        Write-Host "`n✅ Stored procedure test setup completed!" -ForegroundColor Green
        Write-Host "The stored procedure 'cursor_test' has been created and is ready for MCP tool testing." -ForegroundColor White
        
    } else {
        Write-Host "❌ Failed to create stored procedure" -ForegroundColor Red
        Write-Host "Error output: $createResult" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error during test: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest completed." -ForegroundColor Green
