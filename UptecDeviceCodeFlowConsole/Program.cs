using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Net.Http.Headers;

namespace UptecDeviceCodeFlowConsole
{

    public class Program
    {

        #region Private Members

        //client id of the caller (uptec-auth-api-caller)
        private static readonly string _clientId = "CALLER_CLIENT_ID";

        //tenant id of the caller (uptec-auth-api-caller)
        private static readonly string _tenantId = "CALLER_TENANT_ID";

        //list of scopes we have defined. In our example only one scope: Access.Read
        private static readonly string[] _scopes =
        {
            "api://3a9b9211-6791-4992-b779-bb05935f708b/Access.Read"
        };

        //storage token cache using MSAL extension library
        private static StorageCreationProperties? _tokenCacheStorage = null;

        //cache helper to register and refresh the token
        private static MsalCacheHelper? _cacheHelper = null;

        //public client app instance for getting the access token from token endpoint
        private static IPublicClientApplication? _app = null;

        //web api base address
        private static readonly string _apiBaseAddress = "https://localhost:8181";


        #endregion

        static async Task InitiateLogin()
        {
            _tokenCacheStorage =
                new StorageCreationPropertiesBuilder("token.cache", MsalCacheHelper.UserRootDirectory)
                .Build();

            _cacheHelper = await MsalCacheHelper.CreateAsync(_tokenCacheStorage);

            try
            {
                _cacheHelper.VerifyPersistence();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while verifying cache persistence. Skipping this process.");
                Console.WriteLine(ex.ToString());
            }

            _app = PublicClientApplicationBuilder
                .Create(clientId: _clientId)
                .WithTenantId(tenantId: _tenantId)
                .WithDefaultRedirectUri()
                .Build();

            _cacheHelper.RegisterCache(_app.UserTokenCache);

        }

        static Program()
        {
            InitiateLogin().Wait();
        }

        static async Task<string> GetAccessToken()
        {
            string accessToken = string.Empty;
            IEnumerable<IAccount>? accounts = await _app.GetAccountsAsync();
            AuthenticationResult? result = default(AuthenticationResult);
            try
            {
                result = await _app
                    .AcquireTokenSilent(scopes: _scopes, account: accounts?.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (Exception ex)
            {
                if (ex is MsalUiRequiredException)
                {
                    result = await _app.AcquireTokenWithDeviceCode(scopes: _scopes, (codeResult) =>
                    {
                        Console.WriteLine("Requesting token...");
                        Console.WriteLine(codeResult.Message);
                        return Task.CompletedTask;
                    }).ExecuteAsync();
                }
                else
                {
                    Console.WriteLine("Unknown error occured");
                    Console.WriteLine(ex.ToString());
                }
            }
            finally
            {
                if (result != null)
                {
                    accessToken = result.AccessToken;
                    Console.WriteLine("Successfully authenticated!");
                }
                else
                {
                    throw new Exception("Failed to authenticate. Please check the client id, tenant id and scopes!");
                }
            }
            return accessToken;
        }

        static async Task<HttpClient> GetHttpClient()
        {
            string accessToken = await GetAccessToken();
            var client = new HttpClient
            {
                BaseAddress = new Uri(_apiBaseAddress)
            };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            return client;

        }

        static async Task InvokeApiEndpoint()
        {
            var client = await GetHttpClient();
            string path = "/WeatherForecast";
            Console.WriteLine($"\nCalling {path}....");
            string response = await client.GetStringAsync(path);
            Console.WriteLine("Response:");
            Console.WriteLine(response);
        }


        static void Main(string[] args)
        {
            string? input;

            while (true)
            {
                Console.Write("\nPress 1 to invoke api endpoint or 2 to quit: ");
                input = Console.ReadLine()?.Trim();
                if (int.TryParse(input, out int result))
                {
                    switch (result)
                    {
                        case 1:
                            InvokeApiEndpoint().GetAwaiter().GetResult();
                            break;
                        case 2:
                            return;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input!");
                }
            }
        }
    }
}