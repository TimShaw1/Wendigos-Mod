using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wendigos
{
    public static class ChatManager
    {
        static ChatClient client;
        public static void Init(string api_key)
        {
            try
            {
                client = new(model: "gpt-4o", api_key);
                Console.WriteLine("CHATGPT INIT SUCCESS");
            }
            catch (Exception ex)
            {
                Console.WriteLine("CHATGPT INIT FAILED");
                Console.WriteLine(ex.Message);
            }
        }

        public static string GetResponse(string prompt)
        {
            try
            {
                Console.WriteLine("PROMPTING");
                ChatCompletion completion = client.CompleteChat(prompt);
                return completion.ToString();
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
