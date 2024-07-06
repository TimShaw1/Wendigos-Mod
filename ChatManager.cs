using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Wendigos
{
    public static class ChatManager
    {
        static HttpClient client;
        public static bool init_success = false;
        public static void Init(string api_key)
        {
            try
            {
                if (api_key.Length == 0)
                {
                    throw new ArgumentException("No ChatGPT API key!");
                    return;
                }
                client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {api_key}");
                Console.WriteLine("CHATGPT INIT SUCCESS");
                init_success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CHATGPT INIT FAILED");
                Console.WriteLine(ex.Message);
            }
        }

        public static string SendPromptToChatGPT(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 200
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var task = client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                task.Wait();
                var response = task.Result;
                response.EnsureSuccessStatusCode();

                var task2 = response.Content.ReadAsStringAsync();
                task2.Wait();
                var responseContent = task2.Result;
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);

                Console.WriteLine("MESSAGE RECIEVED");
                return jsonResponse.choices[0].message.content;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CHAT BROKE");
                Console.WriteLine(ex.ToString()); 
                return ""; 
            }
        }
    }
}
