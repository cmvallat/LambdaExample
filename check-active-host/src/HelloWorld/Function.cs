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
            // Ensure QueryStringParameters is not null
            string party_code = string.Empty;

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
            var body = Handle(party_code);
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new
                {
                    message = body ? "Host is active" : "Host is not active",
                    data = body
                })
            };
        }

        public bool Handle(String party_code)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetHost", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@pc", party_code);

                // Execute the command and return the object, then close the connection
                List<Host> returnedHostList = new List<Host>();

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        returnedHostList.Add(new Host(){
                            username = reader.GetString("username"),
                            party_name = reader.GetString("party_name"),
                            party_code = reader.GetString("party_code"),
                            invite_only = reader.GetInt32("invite_only"),
                            description = reader.GetString("description"),
                        });
                    }
                }
                connection.Close();

                return returnedHostList.Any();
            }
            catch (MySqlException ex)
            {
                // Todo: add exception handling
                throw new Exception("Something went wrong checking the host status: " + ex);
            }
        }
    }
}
