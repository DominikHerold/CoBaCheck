using System;
using System.Net.Http;
using System.Text;
using System.Threading;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

namespace CongstarBalanceCheck
{
    public class Program
    {
        private static Timer Timer;

        private static string UserName;
        private static string Password;
        private static string ContractId;
        private static string PushOverToken;
        private static string PushOverUser;

        private static string CurrentBalance = "foo";

        private static void Main()
        {
            Console.WriteLine("Start");

            //var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName).AddJsonFile("appsettings.json", false).Build();
            //var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", false).Build();
            var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", false).Build();
            UserName = configuration["UserName"];
            Password = configuration["Password"];
            ContractId = configuration["ContractId"];
            PushOverToken = configuration["PushOverToken"];
            PushOverUser = configuration["PushOverUser"];

            Timer = new Timer((int)TimeSpan.FromHours(5).TotalMilliseconds, false);
            Timer.Elapsed += CheckBalance;

            CheckBalance(null, null);

            Console.ReadLine();
            Timer.Dispose();
        }

        private static string GetOauth()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var client = new HttpClient(handler);
            var toSend = @"{""username"":""" + UserName + @""",""password"":""" + Password + @""",""defaultRedirectUrl"":""/meincongstar"",""targetPageUrlOrId"":""""}";
            var response = client.PostAsync("https://www.congstar.de/api/auth/login", new StringContent(toSend, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            var isSetCookie = response.Headers.TryGetValues("Set-Cookie", out var setCookies);

            if (!isSetCookie || setCookies == null)
                throw new Exception("Can not authenticate");

            foreach (var setCookie in setCookies)
            {
                if (!setCookie.StartsWith("OAuth="))
                    continue;

                var endIndex = setCookie.IndexOf(';');
                var authCookie = setCookie.Substring(0, endIndex);
                return authCookie;
            }

            throw new Exception("Can not authenticate");
        }

        private static string GetContent()
        {
            using (var webClient = new HttpClient())
            {
                HttpResponseMessage response;
                do
                {
                    var oauth = GetOauth();
                    webClient.DefaultRequestHeaders.Clear();
                    webClient.DefaultRequestHeaders.Add("Cookie", oauth);
                    response = webClient.GetAsync($"https://www.congstar.de/customer-contracts/api/contracts/{ContractId}/balance").GetAwaiter().GetResult();
                    Console.WriteLine(response.StatusCode);
                    Console.WriteLine(response.IsSuccessStatusCode);
                }
                while (!response.IsSuccessStatusCode && ThreadSleep15Minutes());
                
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                return content;
            }
        }

        private static bool ThreadSleep15Minutes()
        {
            Thread.Sleep(TimeSpan.FromMinutes(15));

            return true;
        }

        private static void SendToPushoverApi(string balance)
        {
            var client = new HttpClient();
            var toSend = $"token={PushOverToken}&user={PushOverUser}&message=Congstar from {CurrentBalance} to {balance}&title=Congstar";
            var now = DateTime.Now;
            if (now.Hour >= 22 || now.Hour < 7)
                toSend = $"{toSend}&sound=none";

            client.PostAsync("https://api.pushover.net/1/messages.json", new StringContent(toSend, Encoding.UTF8, "application/x-www-form-urlencoded")).GetAwaiter().GetResult();
        }

        private static void CheckBalance(object sender, EventArgs e)
        {
            try
            {
                CheckBalance();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Timer.Start();
            }
        }

        private static void CheckBalance()
        {
            var contentResult = GetContent();
            Console.WriteLine(contentResult);
            var content = JsonConvert.DeserializeObject<Rootobject>(contentResult);
            if (content.Value == CurrentBalance || string.IsNullOrEmpty(content.Value))
                return;

            SendToPushoverApi(content.Value);
            CurrentBalance = content.Value;
        }
    }
}
