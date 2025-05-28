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
            string username = string.Empty;

            if (request.QueryStringParameters != null)
            {
                if (request.QueryStringParameters.ContainsKey("username"))
                {
                    username = request.QueryStringParameters["username"];
                }
            }
            if (string.IsNullOrEmpty(username))
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { error = "username is a required field." })
                };
            }

            var body = Handle(username);
            return new APIGatewayHttpApiV2ProxyResponse{
                StatusCode = 200,
                Body = JsonSerializer.Serialize(body)
            };
        }

        public List<Host> Handle(String username)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetAttendingFromUser", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@un", username);

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

                return returnedHostList;
            }
            catch (MySqlException ex)
            {
                // Todo: add exception handling
                return null;
            }
        }
    }
}
