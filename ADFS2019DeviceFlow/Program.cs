using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;
using static System.Formats.Asn1.AsnWriter;

namespace ADFS2019DeviceFlow
{
    class Program
    {
        IConfiguration Configuration { get; }
        static ILogger Logger { get; } = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
        IPublicClientApplication Auth { get; }

        Program()
        {
            var path = Environment.GetEnvironmentVariable("DATAVERSE_APPSETTINGS") ?? "appsettings.json";
            Configuration = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
            Auth = PublicClientApplicationBuilder.Create(Configuration["AppId"] ?? throw new ArgumentNullException("AppId"))
                .WithAdfsAuthority(Configuration["AdfsUrl"] ?? throw new ArgumentNullException("AdfsUrl"))
                .Build();
        }

        static void Main(string[] args)
        {
            Program app = new();
            var crmUrl = app.Configuration["CrmUrl"] ?? throw new ArgumentNullException("CrmUrl");
            ServiceClient serviceClient = new(new Uri(crmUrl), app.TokenProviderDeviceFlow, logger: Logger);
            WhoAmIResponse resp = (WhoAmIResponse)serviceClient.Execute(new WhoAmIRequest());
            Console.WriteLine("User ID is {0}.", resp.UserId);
            var systemuser = serviceClient.Retrieve("systemuser", resp.UserId, new ColumnSet("fullname"));
            Console.WriteLine($"Hello, {systemuser["fullname"]}, where do you want to go today?");

            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
            serviceClient.Dispose();
        }


        async Task<string> TokenProviderDeviceFlow(string instanceUri)
        {
            var accounts = await Auth.GetAccountsAsync();
            //If CrmUrl is https://crm.example.com/org, resource will be https://crm.example.com/. This must match with one of identifiers for relying party for CRM in ADFS when configured either claims auth or IFD.
            var scope = new Uri(Configuration["CrmUrl"] ?? throw new ArgumentNullException("CrmUrl")).GetLeftPart(UriPartial.Authority) + "/";
            try
            {
                // All AcquireToken* methods store the tokens in the cache, so check the cache first
                var result = await Auth.AcquireTokenSilent(new[] { scope }, accounts.FirstOrDefault()).ExecuteAsync();
                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                var result = await Auth.AcquireTokenWithDeviceCode(new[] { scope }, (deviceCodeResult) =>
                {
                    Console.WriteLine(deviceCodeResult.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();
                return result.AccessToken;
            }
        }
    }
}