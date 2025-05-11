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
            var cognito_username = Guid.NewGuid();
            if(request.QueryStringParameters.ContainsKey("party_code"))
            {
                party_code_string = request.QueryStringParameters["party_code"];
            }
            if(request.QueryStringParameters.ContainsKey("cognito_username"))
            {
                var cognito_username_string = request.QueryStringParameters["cognito_username"];
                cognito_username = new Guid(cognito_username_string);
            }
            var body = Handle(party_code_string, cognito_username);
            return new APIGatewayHttpApiV2ProxyResponse{
                StatusCode = 200,
                Body = body
            };
        }

        public string Handle(String party_code, Guid cognito_username)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("DeleteGuest", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@pc", party_code);
                cmd.Parameters.AddWithValue("@cun", cognito_username);

               // Execute the command and get the number of rows affected, then close the connection
                int rowsAffected = cmd.ExecuteNonQuery();
                connection.Close();
                
                //if something was added to the db, return success
                if(rowsAffected != 0)
                {
                    return "Success!";
                }
                //if nothing was updated in the db, but not a SQL error, return generic error message
                return "General Database Exception: Something went wrong";
            }
            catch (MySqlException ex)
            {
                // Todo: add exception handling
                return null;
            }
        }
    }
}
