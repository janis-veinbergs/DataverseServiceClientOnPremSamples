using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;

namespace ADFS2019ConnectionString
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
            var connectionString = app.Configuration.GetConnectionString("default");
            ServiceClient serviceClient = new(connectionString, logger: Logger);
            // Send a WhoAmI message request to the Organization service to obtain information about the logged on user.
            WhoAmIResponse resp = (WhoAmIResponse)serviceClient.Execute(new WhoAmIRequest());
            Console.WriteLine("User ID is {0}.", resp.UserId);
            var systemuser = serviceClient.Retrieve("systemuser", resp.UserId, new ColumnSet("fullname"));
            Console.WriteLine($"Hello, {systemuser["fullname"]}, where do you want to go today?");

            // Pause program execution before resource cleanup.
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
            serviceClient.Dispose();
        }
    }
}