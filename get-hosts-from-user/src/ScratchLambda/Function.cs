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
        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            // Todo: business logic goes here
            var cognito_username = Guid.NewGuid();
            if(request.QueryStringParameters.ContainsKey("cognito_username"))
            {
                var cognito_username_string = request.QueryStringParameters["cognito_username"];
                cognito_username = new Guid(cognito_username_string);
            }
            var body = Handle(cognito_username);
            return new APIGatewayHttpApiV2ProxyResponse{
                StatusCode = 200,
                Body = JsonSerializer.Serialize(body)
            };
        }

        public List<Host> Handle(Guid cognito_username)
        {
            var connection = new MySqlConnection("server=devdatabasejuly24.cl0k26eoghf5.us-east-1.rds.amazonaws.com;port=3306;database=party;user=cvallat;password=PartyPushProject24!");
            connection.Open();
            try
            {
                //call the stored procedure with parameters
                MySqlCommand cmd = new MySqlCommand("GetHostsFromUser", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@cun", cognito_username);

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
                            cognito_username = reader.GetGuid("cognito_username"),
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
