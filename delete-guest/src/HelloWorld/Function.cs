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
        NotifyUsers notify = new NotifyUsers();
        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            var party_code_string = string.Empty;
            string username = string.Empty;
            // bool isHost;

            if (request.QueryStringParameters != null)
            {
                if (request.QueryStringParameters.ContainsKey("username"))
                {
                    username = request.QueryStringParameters["username"];
                }
                if (request.QueryStringParameters.ContainsKey("party_code"))
                {
                    party_code_string = request.QueryStringParameters["party_code"];
                }
            }

            // if no parameters or no isHost value or invalid boolean
            if (request.QueryStringParameters == null ||
                !request.QueryStringParameters.TryGetValue("isHost", out var isHostStr) ||
                !bool.TryParse(isHostStr, out var isHost))
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { error = "isHost is a required field." })
                };
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(party_code_string))
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { error = "username and party_code are required fields." })
                };
            }

            var response = await Handle(party_code_string, username, isHost);
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new { message = response })
            };
        }

        public async Task<string> Handle(String party_code, String username, bool isHost)
        {
            var secret = dbConnection.GetDatabaseSecret().Result;
            var connection = new MySqlConnection(secret);
            connection.Open();
            
            string storedProcedureName = isHost ? "DeleteGuest" : "UpdateGuest";

            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand(storedProcedureName, connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@pc", party_code);
                cmd.Parameters.AddWithValue("@un", username);
                if (!isHost)
                {
                    // if we are just leaving the party, set at_party to 0
                    cmd.Parameters.AddWithValue("@ap", 0);
                }

                List<string> endpoints;
                try
                {
                    Console.WriteLine("calling GetSNSListAsync with parameters: " + party_code + " " + isHost + " " + username);
                    endpoints = await notify.GetSNSListAsync(party_code, isHost, username);
                    if (endpoints.Count == 0)
                    {
                        return "No users on notification list. Exiting...";
                    }
                }
                catch (Exception ex)
                {
                    return "Something went wrong with gathering the SNS endpoints for notification: " + ex;
                }

                // Execute the command and get the number of rows affected, then close the connection
                int rowsAffected = cmd.ExecuteNonQuery();
                connection.Close();
                string notifyMessage = isHost
                    ? $"You have been kicked from {party_code}."
                    : $"{username} has left your party with the party code {party_code}";

                //if something was removed from the db, notify and return success
                // guest removal
                if (rowsAffected != 0 && isHost)
                {
                    try
                    {
                        await notify.NotifyUsersAsync(endpoints, "Party Removal", notifyMessage);
                        return "Guest was successfully removed from party";
                    }
                    catch (Exception ex)
                    {
                        return "Something went wrong with notifying guest of their removal" + ex;
                    }
                }
                // leave party
                else if (rowsAffected != 0 && !isHost)
                {
                    try
                    {
                        await notify.NotifyUsersAsync(endpoints, "Guest Departure", notifyMessage);
                        return "Guest has successfully left the party";
                    }
                    catch (Exception ex)
                    {
                        return "Something went wrong with notifying host of guest departure: " + ex;
                    }
                }
                // if nothing was upserted into the db, don't try to notify and return error
                else if (rowsAffected == 0 && isHost)
                {
                    return "Error: Guest was not able to be removed from party";
                }
                else if (rowsAffected == 0 && !isHost)
                {
                    return "Error: Guest was not able to leave party";
                }
                //if nothing was updated in the db, but not a SQL error, return generic error message
                return "General Database Exception: Something went wrong";
            }
            catch (Exception ex)
            {
                return "Error: general exception" + ex;
            }
        }
    }
}
