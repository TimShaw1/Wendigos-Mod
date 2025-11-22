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
        public static string Chat_System_Prompt = "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.";
        public static string player_name = "";
        public static GameObject manager;

        public static void StartSpeechTranscription(string prompt)
        {
            Chat_System_Prompt = prompt;
            AIManager.Instance.StartSpeechTranscription();
        }

        public static void Init(string api_key, string region, string language)
        {

            if (AIManager.Instance == null || AIManager.Instance.SpeechToTextService == null)
            {
                Console.WriteLine("No STT Service has been created. Creating one...");
                manager = ModdingTools.CreateAIManagerObject<GeminiServiceConfig, AzureSTTServiceConfig, ElevenlabsTTSServiceConfig>(sttKey: api_key);

            }

            try
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

                            WendigosChatManager.SendPromptToChatService(
                                Chat_System_Prompt + (player_name == "" ? "\n" : "\n" + player_name + ": ") + e.Result.Text,
                                response => 
                                { 
                                    Console.WriteLine("RESPONSE: " + response);

                                    var masked_id = closest_masked.GetComponent<Plugin.MaskedEnemyIdentifier>().id;
                                    string voice_id;
                                    try
                                    {
                                        var client = Plugin.sharedMaskedClientDict[masked_id];
                                        voice_id = Plugin.clientVoiceIDLookup[client];
                                    }
                                    catch
                                    {
                                        voice_id = ElevenLabs.VOICE_ID;
                                    }


                                    // Overlap handled in this function
                                    ElevenLabs.StreamAudio(
                                        response, 
                                        closest_masked.GetComponent<AudioStreamer>()
                                    );

                                    // Have closest masked to player play the new audio file
                                }
                            );

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"GETRESPONSE BROKE: {ex.ToString()}");
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("STT BROKE");
                Console.WriteLine(ex.ToString());
            }

            is_init = true;
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
