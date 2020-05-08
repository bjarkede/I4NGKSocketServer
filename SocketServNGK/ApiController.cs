using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Json;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace SocketServNGK
{
    class ApiController
    {
        private HttpClient client;
        private String address;
        public ApiController(String ApiKey, String URI)
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("ApiKey", ApiKey);
            address = URI;
        }

        public async Task<Tuple<bool, string>> GetUpdate()
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(address);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                return Tuple.Create(true, responseBody);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return Tuple.Create(false, "");
            }
        }

        public async Task<bool> PostUpdate(String data)
        {
            try
            {
                var content = new StringContent(data, Encoding.UTF8, "application/json"); // We ready the content for the Web API.
                HttpResponseMessage response = await client.PostAsync("https://localhost:44328/api/WeatherObservation/", content);
                response.EnsureSuccessStatusCode();

                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public async Task<String> GetJWT(string username, string password)
        {
            try
            {
                string json = "{\"Password\":\"123\",\"Username\":\"hello\"}";
                var details = JsonObject.Parse(json);

                details["Password"] = password;
                details["Username"] = username;

                var content = new StringContent(details.ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("https://localhost:44328/api/User/Login", content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                return responseBody;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return "";
            }
        }

        public async Task<bool> GetPermission(string JWT)
        {
            try
            {
                // @TODO 
                // We need to post our JWT and get permission to get authed by the web api.\
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JWT);

                HttpResponseMessage response = await client.GetAsync("https://localhost:44328/api/User/GrantPermission");
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                bool result = false;

                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }

        }
    }
}
