using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Modding;
using UnityEngine;
using static TimShaw.VoiceBox.Core.ChatUtils;

namespace Wendigos
{
    public static class WendigosChatManager
    {
        static HttpClient client;
        public static bool init_success = false;
        public static ChatManager chatManagerComponent;
        public static List<ChatUtils.VoiceBoxChatMessage> chats;
        public static void Init(string api_key, string modelToUse)
        {
            try
            {
                if (api_key.Length == 0)
                {
                    throw new ArgumentException("No Chat API key!");
                    return;
                }

                // Create a new GameObject and attach a ChatManager component to it
                GameObject chatManager = new GameObject("wendigosChatManager");
                chatManagerComponent = chatManager.AddComponent<ChatManager>();

                // Create a ChatGPTServiceConfig object and choose a model name
                ChatGPTServiceConfig chatConfig = ModdingTools.CreateChatServiceConfig<ChatGPTServiceConfig>();
                chatConfig.modelName = modelToUse;
                chatConfig.useFunctionInvokation = false;

                // Configure the chat manager with the gemini config. 
                // This also creates the chat manager's ChatService via the ServiceFactory
                ModdingTools.InitChatManagerObject(chatManagerComponent, chatConfig, chatKey: api_key);

                chats = new List<VoiceBoxChatMessage>();

                init_success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CHATGPT INIT FAILED");
                Console.WriteLine(ex.Message);
            }
        }

        public static void SendPromptToChatService(string prompt, Action<string> onSuccess)
        {
            try
            {
                // Add a user chat to the chat history
                var chat = new ChatUtils.VoiceBoxChatMessage(
                    ChatUtils.VoiceBoxChatRole.User,
                    prompt
                );

                var tokenSource = new CancellationTokenSource();

                chats.Clear();      // Simplest approach
                chats.Add(chat);
                chatManagerComponent.SendChatMessage(
                    chats,
                    msg => onSuccess.Invoke(msg.Text),
                    err => Console.WriteLine(err),
                    token: tokenSource.Token
                );

            }
            catch (Exception ex)
            {
                Console.WriteLine("CHAT BROKE");
                Console.WriteLine(ex.ToString()); 
                return; 
            }
        }
    }
}
