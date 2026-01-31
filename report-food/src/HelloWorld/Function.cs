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
        NotifyUsers notify = new NotifyUsers();
        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(FoodReportRequest request, ILambdaContext context)
        {
            var result = await Handle(request.food, request.isHost);
            var statusCode = result == "Success!" ? 200 : 500;
            var nullData = new List<Guest>();
            Console.WriteLine("result:" + result);

            try
            {
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
            // return new APIGatewayHttpApiV2ProxyResponse
            // {
            //     StatusCode = statusCode,
            //     Body = JsonSerializer.Serialize(new
            //     {
            //         message = result,
            //         data = nullData
            //     })
            // };
            }

        public async Task<string> Handle(Food food, bool isHost)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("UpdateFoodStatus", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@item", food.item_name);
                cmd.Parameters.AddWithValue("@pc", food.party_code);
                cmd.Parameters.AddWithValue("@stat", food.status);

                // Execute the command and get the number of rows affected, then close the connection
                int rowsAffected = cmd.ExecuteNonQuery();
                connection.Close();
                
                //if something was updated in the db, return success
                if(rowsAffected != 0)
                {
                    // determine who is sending the notification to who and structure message accordingly
                    string actor = isHost ? "Host" : "Guest";
                    string notifyMessage = $"{actor} has reported {food.item_name} as {food.status}.";

                    List<string> endpoints = await notify.GetSNSListAsync(food.party_code, isHost, null);
                    await notify.NotifyUsersAsync(endpoints, "Food update", notifyMessage);

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
