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
            string username = string.Empty;

            try
            {
                // Ensure QueryStringParameters is not null
                if (request.QueryStringParameters != null)
                {
                    if (request.QueryStringParameters.ContainsKey("username"))
                    {
                        username = request.QueryStringParameters["username"];
                    }
                }

                // Validate required fields
                if (string.IsNullOrEmpty(username))
                {
                    return new APIGatewayHttpApiV2ProxyResponse
                    {
                        StatusCode = 400,
                        Body = JsonSerializer.Serialize(new { error = "username is a required field." })
                    };
                }

                // Handle the request and get the response
                string response = Handle(username);

                // Check if the response indicates success or failure
                int statusCode = response == "Success!" ? 200 : 400;

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = statusCode,
                    Body = JsonSerializer.Serialize(new { message = response })
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

        public string Handle(String username)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();

            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("DeleteUser", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@un", username);

                // Execute the command and check rows affected
                int rowsAffected = cmd.ExecuteNonQuery();

                //if something was added to the db, return success
                return rowsAffected > 0
                    ? "Success!"
                    : "No matching record found to delete.";
            }
            catch (MySqlException ex)
            {
                return ex.Message;
            }
            catch
            {
                return "An unexpected error occurred while processing the request.";
            }
        }
    }
}
