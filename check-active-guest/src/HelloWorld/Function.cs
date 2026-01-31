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

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
    public class Function
    {
        DatabaseConnection dbConnection = new DatabaseConnection();

        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            string party_code = string.Empty;
            string username = string.Empty;

            if (request.QueryStringParameters != null)
            {
                request.QueryStringParameters.TryGetValue("party_code", out party_code);
                request.QueryStringParameters.TryGetValue("username", out username);
            }

            if (string.IsNullOrEmpty(party_code) || string.IsNullOrEmpty(username))
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { error = "party_code and username are required fields." })
                };
            }

            try
            {
                var guestStatus = await HandleAsync(party_code, username);

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new
                    {
                        message = guestStatus, // "active", "left", or "removed"
                        data = guestStatus == "active" ? true : false
                    })
                };
            }
            catch (Exception ex)
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = ex.Message })
                };
            }
        }

        public async Task<string> HandleAsync(string party_code, string username)
        {
            string secret = await dbConnection.GetDatabaseSecret();
            using var connection = new MySqlConnection(secret);
            await connection.OpenAsync();

            try
            {
                var cmd = new MySqlCommand("GetGuest", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@pc", party_code);
                cmd.Parameters.AddWithValue("@un", username);

                List<Guest> returnedGuestList = new();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    returnedGuestList.Add(new Guest()
                    {
                        username = reader.GetString("username"),
                        guest_name = reader.GetString("guest_name"),
                        party_code = reader.GetString("party_code"),
                        at_party = reader.GetInt32("at_party")
                    });
                }

                if (returnedGuestList.Count > 1)
                    throw new Exception("Multiple guests found for given username and party_code. This should not happen.");

                if (returnedGuestList.Count == 0)
                    return "DNE";

                return returnedGuestList[0].at_party == 1 ? "active" : "left";
            }
            catch (MySqlException ex)
            {
                throw new Exception("Database error when checking guest status: " + ex.Message);
            }
        }
    }
}
