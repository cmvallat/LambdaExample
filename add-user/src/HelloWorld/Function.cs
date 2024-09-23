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
        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(User request, ILambdaContext context)
        {
            // var user = JsonSerializer.Deserialize<User>(request.Body);
            var result = Handle(request);
            return new APIGatewayHttpApiV2ProxyResponse
            {
                Body = result,
                StatusCode = 200
            };
        }

        public string Handle(User user)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("AddUser", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@un", user.username);
                cmd.Parameters.AddWithValue("@e", user.email);
                cmd.Parameters.AddWithValue("@cun", user.cognito_username);

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
                // Duplicate entry on foreign key party_code
                if (ex.Number == 1062)
                {
                    return "SQL Exception 1062: duplicate entry. Could not create object."; 
                }

                // throw generic message for other SQL errors
                return "SQL Exception: Something went wrong";
            }
        }
    }
}
