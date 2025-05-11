using System;
using System.Data;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using MySql.Data.MySqlClient;
using System.Text.Json;
using System.Collections.Generic;
using LambdaLayerCommonFunctions;

// Assembly attribute for Lambda JSON serialization
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
    public class Function
    {
        DatabaseConnection dbConnection = new DatabaseConnection();
        private const string PlatformApplicationArn = "arn:aws:sns:us-east-1:905418463222:app/APNS_SANDBOX/PPMobileAppCertificate";

        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            if (!request.QueryStringParameters.TryGetValue("username", out var username) ||
                !request.QueryStringParameters.TryGetValue("deviceToken", out var deviceToken) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(deviceToken))
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { error = "Missing required query parameters: username or deviceToken." })
                };
            }

            string snsEndpointArn = null;

            try
            {
                snsEndpointArn = await RegisterWithSNS(deviceToken, username);
                await UpdateUserRecord(username, snsEndpointArn);

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new { message = "Device token registered successfully." })
                };
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(snsEndpointArn))
                {
                    await DeleteSNSEndpoint(snsEndpointArn);
                }

                context.Logger.LogError($"Error: {ex.Message}");
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = ex.Message })
                };
            }
        }

        private async Task<string> RegisterWithSNS(string deviceToken, string username)
        {
            using var snsClient = new AmazonSimpleNotificationServiceClient(RegionEndpoint.USEast1); // change region if needed

            var request = new CreatePlatformEndpointRequest
            {
                PlatformApplicationArn = PlatformApplicationArn,
                Token = deviceToken,
                CustomUserData = username
            };

            var response = await snsClient.CreatePlatformEndpointAsync(request);
            return response.EndpointArn;
        }

        private async Task DeleteSNSEndpoint(string endpointArn)
        {
            using var snsClient = new AmazonSimpleNotificationServiceClient(RegionEndpoint.USEast1);

            try
            {
                var deleteRequest = new DeleteEndpointRequest
                {
                    EndpointArn = endpointArn
                };

                await snsClient.DeleteEndpointAsync(deleteRequest);
            }
            catch (Exception ex)
            {
                // Log and continue—don’t throw again so we don't mask the original error
                Console.WriteLine($"Failed to delete SNS endpoint: {ex.Message}");
            }
        }


        private async Task UpdateUserRecord(string username, string snsEndpointArn)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            await connection.OpenAsync();

            //call the stored procedure with parameters
            MySqlCommand cmd = new MySqlCommand("RegisterDeviceToken", connection);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@token", snsEndpointArn);
            cmd.Parameters.AddWithValue("@un", username);

            // Execute the command and get the number of rows affected, then close the connection
            int rowsAffected = cmd.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                throw new Exception("No user found with that username.");
            }
            connection.Close();
        }
    }
}