using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace LambdaLayerCommonFunctions
{
    public class DatabaseConnection
    {
        public async Task<string> GetDatabaseSecret()
        {
            string secretName = "test/mysqlconnection";
            string region = "us-east-1";

            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            GetSecretValueRequest request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
            };

            GetSecretValueResponse response;

            try
            {
                response = await client.GetSecretValueAsync(request);
            }
            catch (Exception e)
            {
                throw e;
            }

            return response.SecretString;
        }
    }
}