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
        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            var username = request.QueryStringParameters["username"];

            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();

            //call the stored procedure with parameters
            MySqlCommand cmd = new MySqlCommand("CheckSNSStatus", connection);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@un", username);

            var existingArn = cmd.ExecuteScalar()?.ToString();

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new { alreadyRegistered = !string.IsNullOrEmpty(existingArn) })
            };
        }
    }
}
