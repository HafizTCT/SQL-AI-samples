// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

/// <summary>
/// Tool for updating existing stored procedures in SQL Server database.
/// This tool allows users to modify existing stored procedures with new SQL logic.
/// </summary>
public partial class Tools
{
    /// <summary>
    /// Updates an existing stored procedure in the SQL Server database.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to update.</param>
    /// <param name="sqlDefinition">The new SQL definition/body of the stored procedure.</param>
    /// <param name="parameters">Optional new parameters for the stored procedure (e.g., "@param1 INT, @param2 VARCHAR(50)").</param>
    /// <param name="schema">Optional schema name (defaults to 'dbo').</param>
    /// <returns>Result indicating success or failure of the stored procedure update.</returns>
    [McpServerTool(
        Title = "Update Stored Procedure",
        ReadOnly = false,
        Idempotent = false,
        Destructive = true),
        Description("Updates an existing stored procedure in the SQL Database with new SQL definition and parameters.")]
    public async Task<DbOperationResult> UpdateStoredProcedure(
        [Description("Name of the stored procedure to update")] string procedureName,
        [Description("New SQL definition/body of the stored procedure")] string sqlDefinition,
        [Description("Optional new parameters for the stored procedure (e.g., '@param1 INT, @param2 VARCHAR(50)')")] string? parameters = null,
        [Description("Schema name (defaults to 'dbo')")] string? schema = null)
    {
        var conn = await _connectionFactory.GetOpenConnectionAsync();
        try
        {
            using (conn)
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(procedureName))
                {
                    return new DbOperationResult(success: false, error: "Procedure name cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(sqlDefinition))
                {
                    return new DbOperationResult(success: false, error: "SQL definition cannot be empty.");
                }

                // Set default schema
                schema ??= "dbo";

                // Clean procedure name (remove brackets if present)
                var cleanProcedureName = procedureName.Trim('[', ']');

                // First, check if the stored procedure exists
                var checkExistsQuery = @"
                    SELECT COUNT(*) 
                    FROM sys.procedures p 
                    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id 
                    WHERE p.name = @ProcedureName AND s.name = @Schema";

                using (var checkCmd = new SqlCommand(checkExistsQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@ProcedureName", cleanProcedureName);
                    checkCmd.Parameters.AddWithValue("@Schema", schema);
                    
                    var exists = (int)await checkCmd.ExecuteScalarAsync();
                    if (exists == 0)
                    {
                        return new DbOperationResult(
                            success: false, 
                            error: $"Stored procedure '{schema}.{cleanProcedureName}' does not exist. Use CreateStoredProcedure to create a new one.");
                    }
                }

                // Build the ALTER PROCEDURE statement
                var alterStatement = $"ALTER PROCEDURE [{schema}].[{cleanProcedureName}]";
                
                if (!string.IsNullOrWhiteSpace(parameters))
                {
                    alterStatement += $" {parameters.Trim()}";
                }
                
                alterStatement += $"\nAS\nBEGIN\n{sqlDefinition}\nEND";

                _logger.LogInformation("Updating stored procedure: {Schema}.{ProcedureName}", schema, cleanProcedureName);
                _logger.LogDebug("SQL Statement: {AlterStatement}", alterStatement);

                using var cmd = new SqlCommand(alterStatement, conn);
                await cmd.ExecuteNonQueryAsync();

                var result = new DbOperationResult(
                    success: true, 
                    data: new { 
                        message = $"Stored procedure '{schema}.{cleanProcedureName}' updated successfully.",
                        procedureName = $"{schema}.{cleanProcedureName}",
                        parameters = parameters ?? "None",
                        sqlDefinition = sqlDefinition
                    });

                _logger.LogInformation("Successfully updated stored procedure: {Schema}.{ProcedureName}", schema, cleanProcedureName);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateStoredProcedure failed for {ProcedureName}: {Message}", procedureName, ex.Message);
            return new DbOperationResult(success: false, error: $"Failed to update stored procedure: {ex.Message}");
        }
    }
}
