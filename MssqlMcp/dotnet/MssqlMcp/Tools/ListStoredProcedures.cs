// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

/// <summary>
/// Tool for listing stored procedures in SQL Server database.
/// This tool allows users to view all existing stored procedures and their details.
/// </summary>
public partial class Tools
{
    /// <summary>
    /// Lists all stored procedures in the SQL Server database with their details.
    /// </summary>
    /// <param name="schema">Optional schema name to filter by (defaults to all schemas).</param>
    /// <param name="procedureName">Optional procedure name pattern to filter by (supports wildcards).</param>
    /// <returns>List of stored procedures with their details including schema, name, creation date, and parameters.</returns>
    [McpServerTool(
        Title = "List Stored Procedures",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists all stored procedures in the SQL Database with their details including schema, name, creation date, and parameters.")]
    public async Task<DbOperationResult> ListStoredProcedures(
        [Description("Optional schema name to filter by (defaults to all schemas)")] string? schema = null,
        [Description("Optional procedure name pattern to filter by (supports wildcards like '%admin%')")] string? procedureName = null)
    {
        var conn = await _connectionFactory.GetOpenConnectionAsync();
        try
        {
            using (conn)
            {
                // Build the query to get stored procedure information
                var query = @"
                    SELECT 
                        s.name AS SchemaName,
                        p.name AS ProcedureName,
                        p.create_date AS CreateDate,
                        p.modify_date AS ModifyDate,
                        p.is_auto_executed AS IsAutoExecuted,
                        p.is_execution_replicated AS IsExecutionReplicated,
                        p.is_repl_serializable_only AS IsReplSerializableOnly,
                        p.is_ms_shipped AS IsMsShipped
                    FROM sys.procedures p
                    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                    WHERE 1=1";

                var parameters = new List<SqlParameter>();

                // Add schema filter if provided
                if (!string.IsNullOrWhiteSpace(schema))
                {
                    query += " AND s.name = @Schema";
                    parameters.Add(new SqlParameter("@Schema", schema));
                }

                // Add procedure name filter if provided
                if (!string.IsNullOrWhiteSpace(procedureName))
                {
                    query += " AND p.name LIKE @ProcedureName";
                    parameters.Add(new SqlParameter("@ProcedureName", procedureName));
                }

                query += " ORDER BY s.name, p.name";

                _logger.LogInformation("Listing stored procedures with filters - Schema: {Schema}, ProcedureName: {ProcedureName}", 
                    schema ?? "All", procedureName ?? "All");

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddRange(parameters.ToArray());

                var procedures = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var procedure = new
                    {
                        SchemaName = reader.GetString("SchemaName"),
                        ProcedureName = reader.GetString("ProcedureName"),
                        FullName = $"{reader.GetString("SchemaName")}.{reader.GetString("ProcedureName")}",
                        CreateDate = reader.GetDateTime("CreateDate"),
                        ModifyDate = reader.GetDateTime("ModifyDate"),
                        IsAutoExecuted = reader.GetBoolean("IsAutoExecuted"),
                        IsExecutionReplicated = reader.GetBoolean("IsExecutionReplicated"),
                        IsReplSerializableOnly = reader.GetBoolean("IsReplSerializableOnly"),
                        IsMsShipped = reader.GetBoolean("IsMsShipped")
                    };
                    procedures.Add(procedure);
                }

                // Get parameters for each procedure
                var proceduresWithParams = new List<object>();
                foreach (var proc in procedures)
                {
                    var procObj = (dynamic)proc;
                    var paramQuery = @"
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

                    var parametersList = new List<object>();
                    using (var paramCmd = new SqlCommand(paramQuery, conn))
                    {
                        paramCmd.Parameters.AddWithValue("@ProcedureName", procObj.ProcedureName);
                        paramCmd.Parameters.AddWithValue("@Schema", procObj.SchemaName);
                        
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
                            parametersList.Add(param);
                        }
                    }

                    var procedureWithParams = new
                    {
                        SchemaName = procObj.SchemaName,
                        ProcedureName = procObj.ProcedureName,
                        FullName = procObj.FullName,
                        CreateDate = procObj.CreateDate,
                        ModifyDate = procObj.ModifyDate,
                        IsAutoExecuted = procObj.IsAutoExecuted,
                        IsExecutionReplicated = procObj.IsExecutionReplicated,
                        IsReplSerializableOnly = procObj.IsReplSerializableOnly,
                        IsMsShipped = procObj.IsMsShipped,
                        Parameters = parametersList
                    };
                    proceduresWithParams.Add(procedureWithParams);
                }

                var result = new DbOperationResult(
                    success: true, 
                    data: new { 
                        procedures = proceduresWithParams,
                        count = proceduresWithParams.Count,
                        message = $"Found {proceduresWithParams.Count} stored procedure(s)."
                    });

                _logger.LogInformation("Successfully listed {Count} stored procedures", proceduresWithParams.Count);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListStoredProcedures failed: {Message}", ex.Message);
            return new DbOperationResult(success: false, error: $"Failed to list stored procedures: {ex.Message}");
        }
    }
}
