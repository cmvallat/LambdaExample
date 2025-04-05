using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MySql.Data.MySqlClient;
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
            Food foodRequest = JsonSerializer.Deserialize<Food>(request.Body);

            try
            {
                // Call the Handle method to process the request
                string result = Handle(foodRequest);

                // Determine status code based on the result
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
                // Return a 500 response in case of an unhandled exception
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

        public string Handle(Food food)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            using var connection = new MySqlConnection(secret);
            connection.Open();

            try
            {
                // Call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("AddFood", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@item", food.item_name);
                cmd.Parameters.AddWithValue("@pc", food.party_code);
                cmd.Parameters.AddWithValue("@st", food.status);
                cmd.Parameters.AddWithValue("@un", food.username);
                cmd.Parameters.AddWithValue("@cun", food.cognito_username);

                // Execute the command and check the result
                int rowsAffected = cmd.ExecuteNonQuery();
                connection.Close();

                return rowsAffected > 0
                    ? "Success!"
                    : "General Database Exception: Something went wrong";
            }
            catch (MySqlException ex)
            {
                return ex.Number == 1062
                    ? "SQL Exception 1062: duplicate entry. Could not create object."
                    : ex.Message;
            }
        }
    }
}
