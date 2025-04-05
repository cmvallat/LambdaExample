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
            string item_name = string.Empty;
            Guid cognito_username = Guid.NewGuid();

            try
            {
                // Ensure QueryStringParameters is not null
                if (request.QueryStringParameters != null)
                {
                    if (request.QueryStringParameters.ContainsKey("party_code"))
                    {
                        party_code = request.QueryStringParameters["party_code"];
                    }

                    if (request.QueryStringParameters.ContainsKey("item_name"))
                    {
                        item_name = request.QueryStringParameters["item_name"];
                    }

                    if (request.QueryStringParameters.ContainsKey("cognito_username"))
                    {
                        var cognito_username_string = request.QueryStringParameters["cognito_username"];
                        if (!Guid.TryParse(cognito_username_string, out cognito_username))
                        {
                            return new APIGatewayHttpApiV2ProxyResponse
                            {
                                StatusCode = 400,
                                Body = JsonSerializer.Serialize(new { error = "Invalid cognito_username format." })
                            };
                        }
                    }
                }

                // Validate required fields
                if (string.IsNullOrEmpty(party_code) || 
                    string.IsNullOrEmpty(item_name) || 
                    cognito_username == Guid.Empty)
                {
                    return new APIGatewayHttpApiV2ProxyResponse
                    {
                        StatusCode = 400,
                        Body = JsonSerializer.Serialize(new { error = "party_code and item_name are required." })
                    };
                }

                // Handle the request and get the response
                string response = Handle(party_code, item_name, cognito_username);

                // Check if the response indicates success or failure
                int statusCode = response == "Success!" ? 200 : 400;

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = statusCode,
                    Body = JsonSerializer.Serialize(new { message = response })
                };
            }
            catch (Exception)
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = "Internal Server Error" })
                };
            }
        }

        public string Handle(String party_code, String item_name_string, Guid cognito_username)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();

            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("DeleteFood", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@pc", party_code);
                cmd.Parameters.AddWithValue("@cun", cognito_username);
                cmd.Parameters.AddWithValue("@item", item_name_string);

                // Execute the command and check rows affected
                int rowsAffected = cmd.ExecuteNonQuery();

                //if something was added to the db, return success
                return rowsAffected > 0
                    ? "Success!"
                    : "No matching record found to delete.";
            }
            catch (MySqlException ex)
            {
                // Duplicate entry on foreign key party_code
                // throw generic message for other SQL errors
                return ex.Number == 1062
                    ? "SQL Exception 1062: duplicate entry. Could not delete object."
                    : ex.Message;
            }
            catch
            {
                return "An unexpected error occurred while processing the request.";
            }
        }
    }
}
