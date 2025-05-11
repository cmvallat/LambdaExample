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
                if (string.IsNullOrEmpty(party_code) || cognito_username == Guid.Empty)
                {
                    return new APIGatewayHttpApiV2ProxyResponse
                    {
                        StatusCode = 400,
                        Body = JsonSerializer.Serialize(new { error = "party_code and cognito_username are required." })
                    };
                }

                // Retrieve food list
                var foodList = Handle(party_code, cognito_username);

                // Handle empty lists or successful retrieval
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new
                    {
                        message = foodList.Count > 0 ? "Food list retrieved successfully." : "No foods found.",
                        data = foodList
                    })
                };
            }
            catch (MySqlException ex)
            {
                // Handle MySQL-specific exceptions
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = $"MySQL error: {ex.Message}" })
                };
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = $"Internal Server Error: {ex.Message}" })
                };
            }
        }

        public List<Food> Handle(String party_code, Guid cognito_username)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();

            // make sure the user is the host or a guest of the party
            // otherwise, they should not be able to get the food list
            MySqlCommand authorizeCmd = new MySqlCommand("AuthorizeUser", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            authorizeCmd.Parameters.AddWithValue("@pc", party_code);
            authorizeCmd.Parameters.AddWithValue("@cun", cognito_username);

            bool isAuthorized = false;
            using (var reader = authorizeCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    isAuthorized = reader.GetInt32("authorized") == 1;
                }
            }

            if (!isAuthorized)
            {
                throw new UnauthorizedAccessException("User not authorized to access this party's food list.");
            }

            // if authorized, get the food list
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetCurrentFoods", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@pc", party_code);

                // Execute the command and return the object, then close the connection
                List<Food> returnedFoodList = new List<Food>();

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        returnedFoodList.Add(new Food(){
                            username = reader.GetString("username"),
                            item_name = reader.GetString("item_name"),
                            party_code = reader.GetString("party_code"),
                            cognito_username = reader.GetGuid("cognito_username"),
                            status = reader.GetString("status")
                        });
                    }
                }

                return returnedFoodList;
            }
            catch (MySqlException ex)
            {
                throw new Exception($"Failed to execute query: {ex.Message}", ex);
            }
            finally
            {
                connection.Close();
            }
        }
    }
}
