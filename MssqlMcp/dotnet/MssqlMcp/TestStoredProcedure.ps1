# PowerShell script to test the CreateStoredProcedure MCP tool

Write-Host "Testing CreateStoredProcedure MCP tool..." -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan

# Set the connection string
$env:CONNECTION_STRING = "Server=tctapps-development.database.windows.net;Database=TCTBackOffice;User Id=tctdev;Password=TCTapps4pp5;"

# Create the MCP request for CreateStoredProcedure
$request = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "CreateStoredProcedure"
        arguments = @{
            procedureName = "cursor_test"
            sqlDefinition = @"
    -- Test stored procedure created by MCP tool
    SELECT 
        'Hello from cursor_test stored procedure!' AS Message,
        GETDATE() AS CurrentDateTime,
        @@VERSION AS ServerVersion,
        DB_NAME() AS DatabaseName
"@
            parameters = "@testParam VARCHAR(50) = 'Default Value'"
            schema = "dbo"
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Creating stored procedure 'cursor_test'..." -ForegroundColor Yellow
Write-Host "Request:" -ForegroundColor Cyan
Write-Host $request -ForegroundColor Gray

try {
    # Start the MCP server process
    $process = Start-Process -FilePath ".\bin\Debug\net8.0\MssqlMcp.exe" -PassThru -RedirectStandardInput -RedirectStandardOutput -RedirectStandardError -WindowStyle Hidden -NoNewWindow
    
    # Send the request
    $process.StandardInput.WriteLine($request)
    $process.StandardInput.Close()
    
    # Wait for response
    Start-Sleep -Seconds 5
    
    # Read the output
    $output = $process.StandardOutput.ReadToEnd()
    $error = $process.StandardError.ReadToEnd()
    
    # Kill the process
    $process.Kill()
    
    Write-Host "`nMCP Server Response:" -ForegroundColor Green
    Write-Host "===================" -ForegroundColor Green
    
    if ($output) {
        Write-Host $output -ForegroundColor White
    }
    
    if ($error) {
        Write-Host "`nError Output:" -ForegroundColor Red
        Write-Host $error -ForegroundColor Red
    }
    
    # Test if the stored procedure was created by trying to execute it
    Write-Host "`nTesting the created stored procedure..." -ForegroundColor Yellow
    $testResult = sqlcmd -S "tctapps-development.database.windows.net" -d "TCTBackOffice" -U "tctdev" -P "TCTapps4pp5" -Q "EXEC cursor_test 'Test Parameter'" -h -1 -W
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Stored procedure executed successfully!" -ForegroundColor Green
        Write-Host "Result:" -ForegroundColor White
        Write-Host $testResult -ForegroundColor Gray
    } else {
        Write-Host "❌ Failed to execute stored procedure" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error executing test: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest completed." -ForegroundColor Green
