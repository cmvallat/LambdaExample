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
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;


namespace LambdaLayerCommonFunctions
{
    public class NotifyUsers
    {
        DatabaseConnection dbConnection = new DatabaseConnection();
        private readonly AmazonSimpleNotificationServiceClient snsClient = new AmazonSimpleNotificationServiceClient();

        public async Task<List<string>> GetSNSListAsync(string partyCode, bool isHost, string? guest_username)
        {
            string connStr = await dbConnection.GetDatabaseSecret();
            using var connection = new MySqlConnection(connStr);
            await connection.OpenAsync();

            string storedProcedureName = isHost ? "GetHostSNS" : "GetGuestSNSList";
            Console.WriteLine("NotifyUsers prepping to call stored procedure: " + storedProcedureName + " with party_code" + partyCode);

            //call the appropriate stored procedure to get either the Host's SNS endpoint or all the Guests'
            MySqlCommand cmd = new MySqlCommand(storedProcedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@pc", partyCode);

            // loop through all the returned ARNs and notify them via SNS
            var snsEndpoints = new List<string>();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                // check if we are specifying a Guest's username to filter
                // if we only want to send a message to one specified Guest
                if (guest_username != null && isHost)
                {
                    // only add notification for the user whose username matches
                    if (string.Equals(guest_username, reader.GetString("username")))
                    {
                        snsEndpoints.Add(reader.GetString("sns_endpoint_arn"));
                    }
                }
                // if we want to alert all party guests
                else
                {
                    snsEndpoints.Add(reader.GetString("sns_endpoint_arn"));
                }
            }
            return snsEndpoints;
        }

        public async Task NotifyUsersAsync(List<string> snsEndpoints, string title, string message)
        {
            foreach (var arn in snsEndpoints)
            {
                var apnsPayload = new
                {
                    aps = new
                    {
                        alert = new
                        {
                            title = title,
                            body = message
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
    }
}