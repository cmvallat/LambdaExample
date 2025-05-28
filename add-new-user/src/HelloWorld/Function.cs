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

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
    public class Function
    {
        DatabaseConnection dbConnection = new DatabaseConnection();
        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            User userRequest = JsonSerializer.Deserialize<User>(request.Body);

            try
            {
                // Call the Handle method to process the request.
                string result = Handle(userRequest);

                // Determine status code based on the result.
                int statusCode = result == "Success!" ? 200 : 400;

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = statusCode,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" }
                    },
                    Body = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        message = result
                    })
                };
            }
            catch (Exception ex)
            {
                // Return a 500 response in case of an unhandled exception.
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" }
                    },
                    Body = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "Internal Server Error",
                        details = ex.Message
                    })
                };
            }
        }

        public string Handle(User user)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            using var connection = new MySqlConnection(secret);
            connection.Open();

            try
            {
                // Call the stored procedure with parameters.
                MySqlCommand cmd = new MySqlCommand("AddUser", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@un", user.username);
                cmd.Parameters.AddWithValue("@e", user.email);
                cmd.Parameters.AddWithValue("@sns", user.sns_endpoint_arn);

                // Execute the command and get the number of rows affected, then close the connection
                int rowsAffected = cmd.ExecuteNonQuery();
                connection.Close();

                // if something was added to the db, return success
                // if nothing was updated in the db, but not a SQL error, return generic error message
                return rowsAffected > 0
                    ? "Success!"
                    : "General Database Exception: Something went wrong";
            }
            catch (MySqlException ex)
            {
                // Duplicate entry on foreign key party_code
                // throw generic message for other SQL errors
                return ex.Number == 1062
                    ? "SQL Exception 1062: duplicate entry. Could not create object."
                    : ex.Message;
            }
        }
    }
}
