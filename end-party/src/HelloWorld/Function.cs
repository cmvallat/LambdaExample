using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using LambdaLayerObjects;
using LambdaLayerCommonFunctions;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
    public class Function
    {
        private readonly DatabaseConnection dbConnection = new DatabaseConnection();
        private readonly AmazonSimpleNotificationServiceClient snsClient = new AmazonSimpleNotificationServiceClient();
        private const int maxRetries = 3;

        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            string partyCode = request.QueryStringParameters?["party_code"];
            if (string.IsNullOrEmpty(partyCode))
                return CreateResponse(400, new { error = "Missing required field: party_code" });

            try
            {
                // Notify guests first
                await NotifyGuestsAsync(partyCode);

                // Delete Food and Guest with retries
                await RetryDeleteAsync("DeleteFoodFromEndParty", partyCode);
                await RetryDeleteAsync("DeleteGuestFromEndParty", partyCode);

                // Delete Host last
                await ExecuteStoredProcedure("DeleteHostFromEndParty", partyCode);

                return CreateResponse(200, new { message = "Party ended and guests notified successfully." });
            }
            catch (Exception ex)
            {
                return CreateResponse(500, new { error = "Failed to end party", details = ex.Message });
            }
        }

        private async Task RetryDeleteAsync(string procedureName, string partyCode)
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                try
                {
                    await ExecuteStoredProcedure(procedureName, partyCode);
                    return; // Success
                }
                catch (MySqlException ex)
                {
                    lastException = ex;
                    attempts++;
                    await Task.Delay(500 * attempts); // Exponential backoff
                }
            }

            throw new Exception($"Max retries exceeded for {procedureName}", lastException);
        }

        private async Task ExecuteStoredProcedure(string procedureName, string partyCode)
        {
            string connStr = await dbConnection.GetDatabaseSecret();
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(procedureName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@pc", partyCode);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task NotifyGuestsAsync(string partyCode)
        {
            string connStr = await dbConnection.GetDatabaseSecret();
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "SELECT u.sns_endpoint_arn FROM Users u " + 
                "INNER JOIN Guest g ON u.username = g.username WHERE " + 
                "g.party_code = @party_code AND u.sns_endpoint_arn IS NOT NULL;", conn);
            cmd.Parameters.AddWithValue("@party_code", partyCode);

            var snsEndpoints = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snsEndpoints.Add(reader.GetString("sns_endpoint_arn"));
            }

            foreach (var arn in snsEndpoints)
            {
                var apnsPayload = new
                {
                    aps = new
                    {
                        alert = new
                        {
                            title = "Party Ended",
                            body = $"The party with code {partyCode} has been ended by the host."
                        },
                        sound = "default"
                    }
                };

                var messageJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "APNS_SANDBOX", JsonSerializer.Serialize(apnsPayload) }
                });

                var publishRequest = new PublishRequest
                {
                    TargetArn = arn,
                    MessageStructure = "json",
                    Message = messageJson
                };

                try
                {
                    await snsClient.PublishAsync(publishRequest);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to notify {arn}: {ex.Message}");
                }
            }
        }

        private APIGatewayHttpApiV2ProxyResponse CreateResponse(int statusCode, object body)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = statusCode,
                Body = JsonSerializer.Serialize(body),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}
