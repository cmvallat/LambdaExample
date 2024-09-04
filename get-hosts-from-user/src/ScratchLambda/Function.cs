using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ScratchLambda
{
    public class Function
    {
        public async Task<List<Host>> FunctionHandler(User request, ILambdaContext context)
        {
            // var user = JsonSerializer.Deserialize<User>(request.Body);
            return Handle(request.username);
        }

        public List<Host> Handle(String username)
        {
            var connection = new MySqlConnection("server=devdatabasejuly24.cl0k26eoghf5.us-east-1.rds.amazonaws.com;port=3306;database=party;user=cvallat;password=PartyPushProject24!");
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetHostsFromUser", connection);
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
                            phone_number = reader.GetString("phone_number"),
                            spotify_device_id = reader.GetString("spotify_device_id"),
                            invite_only = reader.GetInt32("invite_only")
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
