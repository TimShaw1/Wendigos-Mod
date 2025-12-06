using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using System;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Modding;
using Unity.Netcode;
using UnityEngine;

namespace Wendigos
{
    class AzureSTT
    {
        public static int num_gens = 0;
        public static bool is_init = false;
        public static bool is_recognizing = false;
        public static string Chat_System_Prompt = "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.";
        public static string player_name = "";
        public static GameObject manager;
        public static AzureSTTServiceConfig config;

        public static void StartSpeechTranscription(string prompt)
        {
            if (!is_init) return;
            Chat_System_Prompt = prompt;
            is_recognizing = true;
            AIManager.Instance.StartSpeechTranscription();
        }

        public static void StopSpeechTranscription()
        {
            if (!is_init) return;
            is_recognizing = false;
            AIManager.Instance.StopSpeechTranscription();
        }

        public static void Init(string api_key, string region, string language, string deviceName = "Default")
        {

            if (manager == null || AIManager.Instance == null || AIManager.Instance.SpeechToTextService == null)
            {
                Console.WriteLine("No STT Service has been created. Creating one...");
                config = ModdingTools.CreateSTTServiceConfig<AzureSTTServiceConfig>();
                config.region = region;
                config.language = language;
                config.audioInputDeviceName = deviceName;
                manager = ModdingTools.CreateAIManagerObject(
                    ModdingTools.CreateChatServiceConfig<GenericChatServiceConfig>(),
                    config,
                    ModdingTools.CreateTTSServiceConfig<GenericTTSServiceConfig>(),
                    sttKey: api_key
                );

            }

            try
            {
                InitCallbacks();
            }
            catch (Exception ex)
            {
                Console.WriteLine("STT BROKE");
                Console.WriteLine(ex.ToString());
            }

            is_init = true;
        }

        public static void SendToChatAndStreamAudioResponse(MaskedPlayerEnemy closest_masked, string playerName, string player_speech)
        {
            if (!is_init) return;
            string voice_id;
            var masked_id = closest_masked.GetComponent<Plugin.MaskedEnemyIdentifier>().id;
            try
            {
                var client = Plugin.sharedMaskedClientDict[masked_id];
                voice_id = Plugin.clientVoiceIDLookup[client];
            }
            catch
            {
                voice_id = ElevenLabs.VOICE_ID;
            }

            var newConfig = ElevenLabs.ttsManagerComponent.textToSpeechConfig as ElevenlabsTTSServiceConfig;
            newConfig.voiceId = voice_id;
            ElevenLabs.ttsManagerComponent.textToSpeechConfig = newConfig;

            WendigosChatManager.SendPromptToChatService(
                Chat_System_Prompt + (player_name == "" ? "\n" : "\n" + playerName + ": ") + player_speech,
                response =>
                {
                    //Console.WriteLine("RESPONSE: " + response);

                    ElevenLabs.StreamAudio(
                        response,
                        voice_id,
                        closest_masked.GetComponent<Plugin.MaskedEnemyIdentifier>().child.GetComponent<AudioStreamer>()
                    );

                    num_gens++;
                }
            );
        }

        public static void InitCallbacks()
        {
            AIManager.Instance.SpeechToTextService.OnRecognizing += (s, e) =>
            {
                //Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
            };

            AIManager.Instance.SpeechToTextService.OnRecognized += (s, e) =>
            {

                if (e.Result.Text.Length > 0)
                {
                    Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                    var closest_masked = Plugin.GetClosestMasked();
                    if (closest_masked == null || closest_masked.creatureVoice.isPlaying)
                        return;
                    try
                    {
                        if (!WendigosChatManager.init_success) return;

                        SendToChatAndStreamAudioResponse(closest_masked, player_name, e.Result.Text);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"GETRESPONSE BROKE: {ex.ToString()}");
                    }
                }
            };
        }

        public static void ChangeMicDevice(string deviceName)
        {
            if (!is_init) return;
            bool was_recognizing = is_recognizing;
            AzureSTT.StopSpeechTranscription();
            config.audioInputDeviceName = deviceName;
            AIManager.Instance.SpeechToTextService.Initialize(config);
            //InitCallbacks();

            if (was_recognizing)
            {
                StartSpeechTranscription(Chat_System_Prompt);
            }
        }

        static void GetAudioDevices(string[] args)
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var endpoint in
            enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                Console.WriteLine("{0} ({1})", endpoint.FriendlyName, endpoint.ID);
            }
        }
    }
}
