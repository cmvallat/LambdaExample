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
            var party_code_string = "";
            if(request.QueryStringParameters.ContainsKey("party_code"))
            {
                party_code_string = request.QueryStringParameters["party_code"];
            }
            var body = Handle(party_code_string);
            return new APIGatewayHttpApiV2ProxyResponse{
                StatusCode = 200,
                Body = JsonSerializer.Serialize(body)
            };
        }

        public List<Guest> Handle(String party_code)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetAllGuests", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@pc", party_code);

                // Execute the command and return the object, then close the connection
                List<Guest> returnedGuestList = new List<Guest>();

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        returnedGuestList.Add(new Guest(){
                            username = reader.GetString("username"),
                            guest_name = reader.GetString("guest_name"),
                            party_code = reader.GetString("party_code"),
                            cognito_username = reader.GetGuid("cognito_username"),
                            at_party = reader.GetInt32("at_party")
                        });
                    }
                }
                connection.Close();

                return returnedGuestList;
            }
            catch (MySqlException ex)
            {
                // Todo: add exception handling
                return null;
            }
        }
    }
}
