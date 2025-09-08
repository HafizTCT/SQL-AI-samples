// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

/// <summary>
/// Tool for creating stored procedures in SQL Server database.
/// This tool allows users to create new stored procedures with custom SQL logic.
/// </summary>
public partial class Tools
{
    /// <summary>
    /// Creates a new stored procedure in the SQL Server database.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to create.</param>
    /// <param name="sqlDefinition">The SQL definition/body of the stored procedure.</param>
    /// <param name="parameters">Optional parameters for the stored procedure (e.g., "@param1 INT, @param2 VARCHAR(50)").</param>
    /// <param name="schema">Optional schema name (defaults to 'dbo').</param>
    /// <returns>Result indicating success or failure of the stored procedure creation.</returns>
    [McpServerTool(
        Title = "Create Stored Procedure",
        ReadOnly = false,
        Idempotent = false,
        Destructive = true),
        Description("Creates a new stored procedure in the SQL Database with the specified name and SQL definition.")]
    public async Task<DbOperationResult> CreateStoredProcedure(
        [Description("Name of the stored procedure to create")] string procedureName,
        [Description("SQL definition/body of the stored procedure")] string sqlDefinition,
        [Description("Optional parameters for the stored procedure (e.g., '@param1 INT, @param2 VARCHAR(50)')")] string? parameters = null,
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

                // Build the CREATE PROCEDURE statement
                var createStatement = $"CREATE PROCEDURE [{schema}].[{cleanProcedureName}]";
                
                if (!string.IsNullOrWhiteSpace(parameters))
                {
                    createStatement += $" {parameters.Trim()}";
                }
                
                createStatement += $"\nAS\nBEGIN\n{sqlDefinition}\nEND";

                _logger.LogInformation("Creating stored procedure: {Schema}.{ProcedureName}", schema, cleanProcedureName);
                _logger.LogDebug("SQL Statement: {CreateStatement}", createStatement);

                using var cmd = new SqlCommand(createStatement, conn);
                await cmd.ExecuteNonQueryAsync();

                var result = new DbOperationResult(
                    success: true, 
                    data: new { 
                        message = $"Stored procedure '{schema}.{cleanProcedureName}' created successfully.",
                        procedureName = $"{schema}.{cleanProcedureName}",
                        parameters = parameters ?? "None",
                        sqlDefinition = sqlDefinition
                    });

                _logger.LogInformation("Successfully created stored procedure: {Schema}.{ProcedureName}", schema, cleanProcedureName);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateStoredProcedure failed for {ProcedureName}: {Message}", procedureName, ex.Message);
            return new DbOperationResult(success: false, error: $"Failed to create stored procedure: {ex.Message}");
        }
    }
}
