using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.OperationalInsights;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.Azure.Management.Compute;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using System.Net.Http;
using System.Text;

namespace FunctionApp2
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            try
            {
                MainAsync().Wait();
            }
            catch (Exception e)
            {
                if (e.InnerException != null) { Console.WriteLine("Exception caught.", e.InnerException.Message); }
                else { Console.WriteLine("{0} Exception caught.", e.Message); }
            }
        }
        static async Task MainAsync()
        {
            //Console.WriteLine("Hello World!");
            string workspaceId = System.Environment.GetEnvironmentVariable("workspaceId");
            string subscriptionID = System.Environment.GetEnvironmentVariable("subscriptionID");
            string groupName = System.Environment.GetEnvironmentVariable("groupName");

            string clientId = System.Environment.GetEnvironmentVariable("clientId");
            string clientSecret = System.Environment.GetEnvironmentVariable("clientSecret");
            string domain = System.Environment.GetEnvironmentVariable("domain");

            var authEndpoint = "https://login.microsoftonline.com";
            var tokenAudience = "https://api.loganalytics.io/";

            TimeSpan span = new TimeSpan(0, 36, 0, 0, 0);

            var adSettings = new ActiveDirectoryServiceSettings
            {
                AuthenticationEndpoint = new Uri(authEndpoint),
                TokenAudience = new Uri(tokenAudience),
                ValidateAuthority = true
            };

            try
            {
                var token = GetAuthorizationHeader(clientId, clientSecret);
                var creds = ApplicationTokenProvider.LoginSilentAsync(domain, clientId, clientSecret, adSettings).GetAwaiter().GetResult();

                var client = new OperationalInsightsDataClient(creds);
                client.WorkspaceId = workspaceId;

                string query = "let CPUtable= Perf | where CounterName == \"% Processor Time\" | where ObjectName == \"Processor\" " +
                "| summarize avg(CounterValue) by bin(TimeGenerated, 1hr), Computer " +
                "| project-rename CPU = avg_CounterValue; " +
                "let Idletable = Perf " +
                "| where CounterName == \"% Idle Time\" " +
                "| where ObjectName == \"Processor\" " +
                "| summarize avg(CounterValue) by bin(TimeGenerated, 1hr), Computer " +
                "| project-rename Idle = avg_CounterValue; " +
                "let ReadsTable = Perf " +
                "| where CounterName == \"Disk Reads/sec\" " +
                "| where ObjectName == \"Logical Disk\"" +
                "| summarize avg(CounterValue) by bin(TimeGenerated, 1hr), Computer " +
                "| project-rename Reads = avg_CounterValue; " +
                "let WritesTable = Perf " +
                "| where CounterName == \"Disk Writes/sec\" " +
                "| where ObjectName == \"Logical Disk\" " +
                "| summarize avg(CounterValue) by bin(TimeGenerated, 1hr), Computer " +
                "| project-rename Writes = avg_CounterValue; " +
                "let table1 = CPUtable " +
                "| lookup kind = leftouter Idletable on TimeGenerated,Computer; " +
                "let table2 = table1 " +
                "| lookup kind = leftouter ReadsTable on TimeGenerated,Computer; " +
                "table2 " +
                "| lookup kind = leftouter WritesTable on TimeGenerated,Computer";

                var queryResults = client.Query(query, span);

                HashSet<string> unused = await ValidateVM(queryResults, token, groupName, subscriptionID);
                string responseString = string.Join(", ", unused);
                Console.WriteLine(responseString);
                if (responseString.Length > 0)
                {
                    SendEmail(responseString).Wait();
                    Console.WriteLine("Sent Email");
                }
            }
            catch (Exception e)
            {
                if (e.InnerException != null) { throw new InvalidOperationException(e.InnerException.Message); }
                else { throw new InvalidOperationException(e.Message); }
            }
        }

        public static async Task<string> SendEmail(string response)
        {
            try
            {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(System.Environment.GetEnvironmentVariable("address"));

                HttpRequestMessage request1 = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress);
                request1.Content = new StringContent($"{{\"Machines\" : \"{response}\"}}", Encoding.UTF8, "application/json");
                var result = await client.SendAsync(request1);
                if (!result.IsSuccessStatusCode)
                {
                    throw new HttpRequestException("Http request not Valid");
                }
                return result.ReasonPhrase;
            }
            catch (Exception e)
            {
                if (e.InnerException != null) { throw new InvalidOperationException(e.InnerException.Message); }
                else { throw new InvalidOperationException(e.Message); }
            }
        }

        public static async Task<HashSet<string>> ValidateVM(Microsoft.Azure.OperationalInsights.Models.QueryResults queryResults, string token, string group, string subscriptionID)
        {
            HashSet<string> unused = new HashSet<string>();
            HashSet<string> used = new HashSet<string>();
            double thres_CPU = Convert.ToDouble(System.Environment.GetEnvironmentVariable("CPU"));
            double thres_Reads = Convert.ToDouble(System.Environment.GetEnvironmentVariable("Reads"));
            double thres_Writes = Convert.ToDouble(System.Environment.GetEnvironmentVariable("Writes"));
            double thres_Idle = Convert.ToDouble(System.Environment.GetEnvironmentVariable("Idle"));

            try
            {
                for (var i = 0; i < queryResults.Tables[0].Rows.Count; i++)
                {
                    string VMname = queryResults.Tables[0].Rows[i][1];
                    string status = await GetVMstatus(token, VMname, group, subscriptionID);

                    if (status == "true")
                    {
                        double CPU = Convert.ToDouble(queryResults.Tables[0].Rows[i][2]);
                        double Reads = Convert.ToDouble(queryResults.Tables[0].Rows[i][4]);
                        double Writes = Convert.ToDouble(queryResults.Tables[0].Rows[i][5]);
                        double Idle = Convert.ToDouble(queryResults.Tables[0].Rows[i][3]);

                        if ((CPU > thres_CPU || Reads > thres_Reads || Writes > thres_Writes) && (Idle < thres_Idle))
                        {
                            used.Add(VMname);
                        }
                        else
                        {
                            unused.Add(VMname);
                        }
                    }
                }
                unused.ExceptWith(used);
                return unused;
            }
            catch (Exception e)
            {
                if (e.InnerException != null) { throw new InvalidOperationException(e.InnerException.Message); }
                else { throw new InvalidOperationException(e.Message); }
            }
        }

        public static async Task<string> GetVMstatus(string token, string name, string group, string subscriptionID)
        {
            var credential = new TokenCredentials(token);
            var computeManagementClient = new ComputeManagementClient(credential);
            computeManagementClient.SubscriptionId = subscriptionID;
            try
            {
                var a1 = await computeManagementClient.VirtualMachines.ListAllWithHttpMessagesAsync("false");
                var vmResult = await computeManagementClient.VirtualMachines.InstanceViewWithHttpMessagesAsync(group, name);
                foreach (var i in vmResult.Body.Statuses)
                {
                    if (i.DisplayStatus.ToLower() == "vm deallocated")
                    {
                        return "false";
                    }
                    //Console.WriteLine(i.DisplayStatus);
                }
                return "true";
            }
            catch (Exception e)                 //catch block captures a non existing VM condition
            {
                //Console.WriteLine("{0} Exception caught.", e.Message);
                return "false";
            }
        }

        private static string GetAuthorizationHeader(string clientId, string clientSecret)
        {
            try
            {
                string domain = System.Environment.GetEnvironmentVariable("domain");
                ClientCredential cc = new ClientCredential(clientId, clientSecret);
                var context = new AuthenticationContext("https://login.windows.net/" + domain);
                var result = context.AcquireTokenAsync("https://management.azure.com/", cc);

                if (result == null)
                {
                    throw new InvalidOperationException("Failed to obtain the JWT token");
                }
                string token = result.Result.AccessToken;

                return token;
            }
            catch (Exception e)
            {
                if (e.InnerException != null) { throw new InvalidOperationException(e.InnerException.Message); }
                else { throw new InvalidOperationException(e.Message); }
            }
        }
    }
}

