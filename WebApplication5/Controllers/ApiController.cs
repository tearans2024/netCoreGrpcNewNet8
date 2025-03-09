﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

//[Route("api/[controller]")]
//[ApiController]
//public class ApiController : ControllerBase

[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<ApiController> _logger;

    public ApiController(IOptions<DatabaseConfig> dbConfig, ILogger<ApiController> logger)
    {
        _connectionString = dbConfig.Value.ConnectionString;
        _logger = logger;
    }

    [HttpPost("query")]
    public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
        {
            return BadRequest(new ResponseModel
            {
                Status = false,
                Message = "Query cannot be empty",
                Data = null
            });
        }

        var result = new List<Dictionary<string, object>>();

        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(request.Query, conn);

            foreach (var param in request.Parameters)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Add(row);
            }

            return Ok(new ResponseModel
            {
                Status = true,
                Message = "Success",
                Data = result
            });
        }
        catch (NpgsqlException ex)
        {
            _logger.LogError(ex, "Database error occurred while executing query: {Query}", request.Query);
            return StatusCode(500, new ResponseModel
            {
                Status = false,
                Message = "Database error: " + ex.Message,
                Data = null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while executing query: {Query}", request.Query);
            return StatusCode(500, new ResponseModel
            {
                Status = false,
                Message = "Internal server error",
                Data = null
            });
        }
    }
}
