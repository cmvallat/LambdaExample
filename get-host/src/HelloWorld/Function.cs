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

            try
            {
                // Validate required fields
                if (request.QueryStringParameters != null)
                {
                    if (request.QueryStringParameters.ContainsKey("party_code"))
                    {
                        party_code = request.QueryStringParameters["party_code"];
                    }
                }
                if (string.IsNullOrEmpty(party_code))
                {
                    return new APIGatewayHttpApiV2ProxyResponse
                    {
                        StatusCode = 400,
                        Body = JsonSerializer.Serialize(new { error = "party_code is a required field." })
                    };
                }

                // Retrieve user list (should just be one but we need to return a list for front end)
                var returnedHostList = Handle(party_code);

                // Handle empty lists or successful retrieval
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new
                    {
                        message = (returnedHostList.Count == 1) ? "Host retrieved successfully." : "Host not found.",
                        data = returnedHostList
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

        public List<Host> Handle(String party_code)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();

            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetHost", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@pc", party_code);

                // Execute the command and return the object, then close the connection
                List<Host> returnedHostList = new List<Host>();

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        returnedHostList.Add(new Host()
                        {
                            username = reader.GetString("username"),
                            party_code = reader.GetString("party_code"),
                            party_name = reader.GetString("party_name"),
                            invite_only = reader.GetInt32("invite_only"),
                            description = SafeGetString(reader, "description"),
                        });
                    }
                }
                if (returnedHostList.Count > 1)
                {
                    throw new Exception("Error: Multiple users found for one party_code");
                }
                return returnedHostList;
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
        public static string SafeGetString(MySqlDataReader reader, string fieldName)
        {
            // get the column index from the column name we know
            int colIndex = reader.GetOrdinal(fieldName);

            // check if the database returned a null string, if so, return null
            if (!reader.IsDBNull(colIndex))
                return reader.GetString(colIndex);
            return null;
        }

    }
}
