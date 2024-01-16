# Auth Series #4 - Call ASP.NET Core API Protected By Azure AD/Microsoft Entra ID via Console App Using Device Code Flow

![2024 01 16 15H57 24](assets/2024-01-16_15h57_24.png)

This is 4th tutorial of the **Auth Series**. In tutorial, we are going to make a simple 
console application built on top of .NET 7x and will call the api protected by Azure AD/Microsoft Entra ID.
I encourage you to follow 2nd tutorial in here: [Auth Series #2 - Protect ASP.NET Core Api with Azure Entra ID and Access It via Postman](https://github.com/mirzaevolution/Uptec-Protected-Web-Api).
The 2nd tutorial is about a setup for the api to be protected by Azure AD/Microsoft Entra ID. 
Also, it's optional to read the 3rd tutorial that talks about calling the api via console app 
using **Client Credentials Flow** in here: [Auth Series #3 - Call ASP.NET Core API Protected by Azure AD/Microsoft Entra ID via Console Client Credentials Flow](https://github.com/mirzaevolution/Uptec-Call-Protected-Api-Client-Credentials).

![2024 01 16 15H44 13](assets/2024-01-16_15h44_13.gif)

The console application we'd like to build is almost the same like in the 3rd tutorial. 
The difference is, we will make use of device code flow and this flow is public client application not 
confidential client application like in the 3rd tutorial.

So we have to make sure to turn on the **Allow Public Client Flow** in the Azure Portal.

**Requirements:**

- Framework: .NET 7x Console Project
- Nuget: Microsoft.Identity.Client and Microsoft.Identity.Client.Extensions.Msal

The **Microsoft.Identity.Client.Extensions.Msal** library is used to store & retrieve the access token 
we have obtained. So, the next calls (as long as the token doesn't expire) don't require calling the token endpoint again. 
This way, it will speed-up the application authentication process.

Let's start for the 1st step.

### 1. Enable "Allow Public Client Flow"


If you follow our previous tutorial, we have created two new app registrations:

 - uptec-auth-api: This app registration used by our protected WeatherForecast api
 - uptec-auth-api-caller: This app registration used client apps to call the protected api

Now, we need to go to **uptec-auth-api-caller** app registration to enable the Allow Public Client Flow option.

![2024 01 16 10H32 21](assets/2024-01-16_10h32_21.png)

On the selected app registration, don't forget to take a note these following things:
 
 - Client Id
 - Tenant Id
 - Scopes

![2024 01 16 10H35 13](assets/2024-01-16_10h35_13.png)

If you follow previous tutorials, we have added a permission for **Access.Read** scope from **uptec-auth-api** 
app registration. Now, go to API Permissions, click the Access.Read permission, copy the scope permission there.

**NB: If you don't see what shown in below screenshot, make sure you follow our 2nd tutorial.**

![2024 01 16 10H43 26](assets/2024-01-16_10h43_26.png)


### 2. Create Console Application

Create default console application in Visual Studio, and give the name as you wish.

![2024 01 16 10H30 43](assets/2024-01-16_10h30_43.png)

![2024 01 16 10H30 59](assets/2024-01-16_10h30_59.png)

![2024 01 16 10H31 07](assets/2024-01-16_10h31_07.png)

Once created, open the **Manage Nuget Package**, and install these packages:

 - Microsoft.Identity.Client
 - Microsoft.Identity.Client.Extensions.Msal

![2024 01 16 10H44 32](assets/2024-01-16_10h44_32.png)


### 3. Implement The Code

On the **Program.cs**, add these namespaces:

```
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Net.Http.Headers;

```

Inside the Program class, add the following private members. 
Some of them are related to Client Id, Tenant Id and Scopes we saw earlier.

```

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
```

![2024 01 16 15H47 00](assets/2024-01-16_15h47_00.png)

Ok, now we need to add a method to initiate the login. This method will prepare from 
creating the storage cache helper until the **PublicClientApplication** initialization.
Because this app uses Device Code flow, we should use public client option and no need for Client Secret.

The **StorageCreationPropertiesBuilder** and **MsalCacheHelper** are responsible to store and retrive saved token 
we gain using **PublicClientApplication** instance to a file in our system. In our sample, we name it: **'token.cache'**.



```
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
```

![2024 01 16 15H47 19](assets/2024-01-16_15h47_19.png)

Add the InitiateLogin method to static constructor for the Program class. By doing this, 
once the program starts, the InitiateLogin will be called automatically.

```
        static Program()
        {
            InitiateLogin().Wait();
        }
```

![2024 01 16 15H47 27](assets/2024-01-16_15h47_27.png)

The next one is GetAccessToken method. This is a crucial method as it will be called multiple times. 
The logic is, we will try to acquire the token silently (from the cache). If the token exists and doesn't expire, 
we will use it. But, if token doesn't exist or expire, we will do a login process using Device Code Flow mechanism.

```
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
                    Console.WriteLine("Unknown error occurred");
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
```

![2024 01 16 15H48 31](assets/2024-01-16_15h48_31.png)


The next methods will be GetHttpClient and InvokeApiEndpoint methods. 
Here, we will fetch api endpoint and utilize the access token. 

```
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
```
![2024 01 16 15H48 50](assets/2024-01-16_15h48_50.png)

![2024 01 16 15H48 59](assets/2024-01-16_15h48_59.png)

Last method, we will add logic inside the Main method to choose whether or not we will fetch the api or quit. 
This way, we can test to fetch again and again to see that in the next fetch, we don't need to login anymore (still using the token cache).

```
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
```

![2024 01 16 15H49 15](assets/2024-01-16_15h49_15.png)


### 4. Test The Application

Before we test our console client app, we should run our previous WeatherForecast protected api. 
To do so, open the project in Visual Studio, and run it.

![2024 01 16 15H50 12](assets/2024-01-16_15h50_12.png)

![2024 01 16 15H50 34](assets/2024-01-16_15h50_34.png)


Now, we can run our console app. Choose the **'1'** to fetch the token and api.

![2024 01 16 15H50 49](assets/2024-01-16_15h50_49.png)

![2024 01 16 15H51 58](assets/2024-01-16_15h51_58.png)

Once you hit the Enter, you will be given a link and a code. You need to copy past the link in the browser 
and also in the text box of webpage, enter the code you get from console.

![2024 01 16 15H56 02](assets/2024-01-16_15h56_02.png)

![2024 01 16 15H56 43](assets/2024-01-16_15h56_43.png)

![2024 01 16 15H57 05](assets/2024-01-16_15h57_05.png)

If nothing goes wrong, you will see that our console client app can fetch the api successfully.

![2024 01 16 15H57 24](assets/2024-01-16_15h57_24.png)

Now, try to enter **'1'** again and you'll notice that we don't need to login again even 
if you close the app and run it again. 

![2024 01 16 15H57 31](assets/2024-01-16_15h57_31.png)

![2024 01 16 15H44 13](assets/2024-01-16_15h44_13.gif)


On Windows OS, to check the token cache file, you can go to Local folder like in the screenshot below.


![2024 01 16 15H57 55](assets/2024-01-16_15h57_55.png)

Ok, i think that's all for this tutorial. The next topic we will talk about calling protected api via 
web application.

Thank you.


> Sample project: https://github.com/mirzaevolution/Uptec-Calls-Protected-Api-Device-Code-Flow


Regards,

**Mirza Ghulam Rasyid**