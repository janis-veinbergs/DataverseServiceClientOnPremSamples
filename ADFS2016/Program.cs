using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;
using System.Net.Http.Json;

namespace ADFS2016
{
    class Program
    {
        IConfiguration Configuration { get; }
        static ILogger Logger { get; } = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();

        Program()
        {
            var path = Environment.GetEnvironmentVariable("DATAVERSE_APPSETTINGS") ?? "appsettings.json";
            Configuration = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
        }

        static void Main(string[] args)
        {
            Program app = new();
            var crmUrl = app.Configuration["CrmUrl"] ?? throw new ArgumentNullException("CrmUrl");
            ServiceClient serviceClient = new(new Uri(crmUrl), app.TokenProviderAdfs2016, logger: Logger);
            WhoAmIResponse resp = (WhoAmIResponse)serviceClient.Execute(new WhoAmIRequest());
            Console.WriteLine("User ID is {0}.", resp.UserId);
            var systemuser = serviceClient.Retrieve("systemuser", resp.UserId, new ColumnSet("fullname"));
            Console.WriteLine($"Hello, {systemuser["fullname"]}, where do you want to go today?");

            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
            serviceClient.Dispose();
        }

        /// <summary>
        /// Cache access_token for subsequent requests
        /// </summary>
        string? AccessToken { get; set; }
        async Task<string> TokenProviderAdfs2016(string instanceUri)
        {
            if (AccessToken != null) return AccessToken;
            var resource = new Uri(Configuration["CrmUrl"] ?? throw new ArgumentNullException("CrmUrl")).GetLeftPart(UriPartial.Authority) + "/";
            ///Construct Resource owner password credentials grant request - username/password enought for auth
            HttpClient http = new HttpClient();
            var adfsUrl = Configuration["AdfsUrl"] ?? throw new ArgumentNullException("AdfsUrl");
            var request = new HttpRequestMessage(HttpMethod.Get, adfsUrl + "/oauth2/token");
            request.Headers.Add("Accept", "application/json");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>() {
                { "grant_type", "password" },
                { "client_id", Configuration["AppId"] ?? throw new ArgumentNullException("AppId") },
                //If CrmUrl is https://crm.example.com/org, resource will be https://crm.example.com/. This must match with one of identifiers for relying party for CRM in ADFS when configured either claims auth or IFD.
                //ADFS 2016 requires resurce parameter. ADFS 2019 allows resource within scope parameter. MSAL will use scope parameter.
                { "resource", resource },
                { "scope", "openid" },
                { "username", Configuration["Username"] ?? throw new ArgumentNullException("Username") },
                { "password", Configuration["Password"] ?? throw new ArgumentNullException("Password") },
            });

            var response = await http.SendAsync(request);
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
            AccessToken = token?.access_token ?? throw new Exception("Couldn't get access_token");
            return AccessToken;
        }

        public record TokenResponse
        {
            public string access_token { get; set; }
            public string refresh_token { get; set; }
        }
    }
}