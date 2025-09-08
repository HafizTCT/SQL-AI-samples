// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

/// <summary>
/// Tool for retrieving the definition of an existing stored procedure.
/// This tool allows users to view the complete SQL definition of a stored procedure.
/// </summary>
public partial class Tools
{
    /// <summary>
    /// Gets the complete definition of an existing stored procedure.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to get the definition for.</param>
    /// <param name="schema">Optional schema name (defaults to 'dbo').</param>
    /// <returns>The complete SQL definition of the stored procedure.</returns>
    [McpServerTool(
        Title = "Get Stored Procedure Definition",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Gets the complete SQL definition of an existing stored procedure including parameters and body.")]
    public async Task<DbOperationResult> GetStoredProcedureDefinition(
        [Description("Name of the stored procedure to get the definition for")] string procedureName,
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
                            error: $"Stored procedure '{schema}.{cleanProcedureName}' does not exist.");
                    }
                }

                // Get the stored procedure definition
                var definitionQuery = @"
                    SELECT 
                        m.definition AS Definition,
                        p.create_date AS CreateDate,
                        p.modify_date AS ModifyDate,
                        s.name AS SchemaName,
                        p.name AS ProcedureName
                    FROM sys.sql_modules m
                    INNER JOIN sys.procedures p ON m.object_id = p.object_id
                    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                    WHERE p.name = @ProcedureName AND s.name = @Schema";

                using var cmd = new SqlCommand(definitionQuery, conn);
                cmd.Parameters.AddWithValue("@ProcedureName", cleanProcedureName);
                cmd.Parameters.AddWithValue("@Schema", schema);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var definition = reader.GetString("Definition");
                    var createDate = reader.GetDateTime("CreateDate");
                    var modifyDate = reader.GetDateTime("ModifyDate");
                    var schemaName = reader.GetString("SchemaName");
                    var procName = reader.GetString("ProcedureName");

                    // Get parameters information
                    var parametersQuery = @"
                        SELECT 
                            p.name AS ParameterName,
                            t.name AS DataType,
                            p.max_length,
                            p.precision,
                            p.scale,
                            p.is_output AS IsOutput,
                            p.has_default_value AS HasDefaultValue,
                            p.default_value AS DefaultValue
                        FROM sys.parameters p
                        INNER JOIN sys.types t ON p.user_type_id = t.user_type_id
                        WHERE p.object_id = (
                            SELECT p2.object_id 
                            FROM sys.procedures p2 
                            INNER JOIN sys.schemas s2 ON p2.schema_id = s2.schema_id 
                            WHERE p2.name = @ProcedureName AND s2.name = @Schema
                        )
                        ORDER BY p.parameter_id";

                    var parameters = new List<object>();
                    using (var paramCmd = new SqlCommand(parametersQuery, conn))
                    {
                        paramCmd.Parameters.AddWithValue("@ProcedureName", cleanProcedureName);
                        paramCmd.Parameters.AddWithValue("@Schema", schema);
                        
                        using var paramReader = await paramCmd.ExecuteReaderAsync();
                        while (await paramReader.ReadAsync())
                        {
                            var param = new
                            {
                                ParameterName = paramReader.GetString("ParameterName"),
                                DataType = paramReader.GetString("DataType"),
                                MaxLength = paramReader.GetInt16("max_length"),
                                Precision = paramReader.GetByte("precision"),
                                Scale = paramReader.GetByte("scale"),
                                IsOutput = paramReader.GetBoolean("IsOutput"),
                                HasDefaultValue = paramReader.GetBoolean("HasDefaultValue"),
                                DefaultValue = paramReader.IsDBNull("DefaultValue") ? null : paramReader.GetString("DefaultValue")
                            };
                            parameters.Add(param);
                        }
                    }

                    var result = new DbOperationResult(
                        success: true, 
                        data: new { 
                            procedureName = $"{schemaName}.{procName}",
                            definition = definition,
                            createDate = createDate,
                            modifyDate = modifyDate,
                            parameters = parameters,
                            message = $"Retrieved definition for stored procedure '{schemaName}.{procName}'."
                        });

                    _logger.LogInformation("Successfully retrieved definition for stored procedure: {Schema}.{ProcedureName}", schemaName, procName);
                    return result;
                }
                else
                {
                    return new DbOperationResult(
                        success: false, 
                        error: $"Could not retrieve definition for stored procedure '{schema}.{cleanProcedureName}'.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStoredProcedureDefinition failed for {ProcedureName}: {Message}", procedureName, ex.Message);
            return new DbOperationResult(success: false, error: $"Failed to get stored procedure definition: {ex.Message}");
        }
    }
}
