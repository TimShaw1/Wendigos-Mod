using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using System;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Modding;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace Wendigos
{
    class STT
    {
        public static int num_gens = 0;
        public static bool is_init = false;
        public static bool is_recognizing = false;
        public static int num_retries = 0;
        public static string Chat_System_Prompt = "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.";
        public static string player_name = "";
        public static GameObject manager;
        public static GenericSTTServiceConfig config;
        public static Dictionary<string, byte[]> speakingClips = new Dictionary<string, byte[]>();

        public static uint MAX_CLIP_COUNT = 10;
        private static DateTime _sttSessionStartTime;

        private static RollingRecorder recorder;

        public static List<byte[]> recordingBuffer = new List<byte[]>();
        
        private static bool midRequest = false;

        public static void StartSpeechTranscription(string prompt)
        {
            if (!is_init) return;
            Chat_System_Prompt = prompt;
            is_recognizing = true;
            AIManager.Instance.StartSpeechTranscription();
            recorder.StartRecording(Plugin.mic_name);
        }

        public static void StopSpeechTranscription()
        {
            if (!is_init) return;
            is_recognizing = false;
            AIManager.Instance.StopSpeechTranscription();
            recorder.StopRecording();
        }

        public static void Init(string api_key, string region, string language, string deviceName = "Default")
        {
            MAX_CLIP_COUNT = Plugin.max_clip_count.Value;
            if (manager == null || AIManager.Instance == null || AIManager.Instance.SpeechToTextService == null)
            {
                Console.WriteLine("[Wendigos STT] No STT Service has been created. Creating one...");
                if (Plugin.STT_service.Value == "Azure")
                {
                    config = ModdingTools.CreateSTTServiceConfig<AzureSTTServiceConfig>();
                    AzureSTTServiceConfig derivedConfig = config as AzureSTTServiceConfig;
                    derivedConfig.region = region;
                    derivedConfig.language = language;
                    derivedConfig.audioInputDeviceName = deviceName;
                    derivedConfig.requestWordLevelTimestamps = true;

                    Console.WriteLine("[Wendigos STT]: Creating AI manager object for STT service. Disregard \"Service config is null\" errors.");
                    manager = ModdingTools.CreateAIManagerObject(
                        ModdingTools.CreateChatServiceConfig<GenericChatServiceConfig>(),
                        derivedConfig,
                        ModdingTools.CreateTTSServiceConfig<GenericTTSServiceConfig>(),
                        sttKey: api_key
                    );
                }
                else
                {
                    config = ModdingTools.CreateSTTServiceConfig<ElevenlabsSTTServiceConfig>();
                    ElevenlabsSTTServiceConfig derivedConfig = config as ElevenlabsSTTServiceConfig;
                    derivedConfig.language_code = language;
                    derivedConfig.audioInputDeviceName = deviceName;
                    derivedConfig.vad_silence_threshold_secs = 0.65;
                    derivedConfig.include_timestamps = true;
                    derivedConfig.commit_strategy = ElevenlabsSTTCommitStrategy.Vad;
                    derivedConfig.min_silence_duration_ms = 550;

                    Console.WriteLine("[Wendigos STT]: Creating AI manager object for STT service. Disregard \"Service config is null\" errors.");
                    manager = ModdingTools.CreateAIManagerObject(
                        ModdingTools.CreateChatServiceConfig<GenericChatServiceConfig>(),
                        derivedConfig,
                        ModdingTools.CreateTTSServiceConfig<GenericTTSServiceConfig>(),
                        sttKey: api_key
                    );
                }
                Console.WriteLine("[Wendigos STT] STT Service Created.");

                _sttSessionStartTime = DateTime.Now;

                recorder = new RollingRecorder();

            }

            try
            {
                InitCallbacks();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Wendigos STT] ERROR");
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
                if (voice_id == "")
                {
                    voice_id = ElevenLabs.VOICE_ID;
                    Plugin.WendigosNetworkManager.Instance.ShareVoiceIDServerRpc(NetworkManager.Singleton.LocalClientId, voice_id);
                }
            }
            catch
            {
                voice_id = ElevenLabs.VOICE_ID;
            }

            var newConfig = ElevenLabs.ttsManagerComponent.textToSpeechConfig as ElevenlabsTTSServiceConfig;
            newConfig.voiceId = voice_id;
            ElevenLabs.ttsManagerComponent.textToSpeechConfig = newConfig;
            

            WendigosChatManager.SendPromptToChatService(
                Chat_System_Prompt + " A player just spoke to you, saying the following: " + (player_name == "" ? "\n" : "\n" + playerName + ": ") + player_speech,
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closest_masked"></param>
        /// <param name="playerName"></param>
        /// <param name="player_speech">Either the player's spoken speech or context for the AI if not responding</param>
        /// <param name="respondingToPlayer"></param>
        public static void SendToChatAndChooseResponse(MaskedPlayerEnemy closest_masked, string playerName, string player_speech, bool respondingToPlayer = true)
        {
            if (!is_init) return;
            if (speakingClips.Keys.Count == 0) return;
            if (midRequest && !respondingToPlayer) return;

            midRequest = true;

            var masked_id = closest_masked.GetComponent<Plugin.MaskedEnemyIdentifier>().id;

            string choicePrompt;
            if (respondingToPlayer)
            {
                choicePrompt = Chat_System_Prompt + " A player just spoke to you, saying the following: " + (player_name == "" ? "\n" : "\n" + playerName + ": ") + player_speech
                    + "\nYou have The following options to reply back:\n";
            }
            else
            {
                choicePrompt = Chat_System_Prompt;

                if (player_speech == "{DAMAGED}") choicePrompt += " You just got damaged by a player.";

                choicePrompt += " You can see a player in front of you. You have the following options to speak to them:\n";
            }

            string[] choices = new string[speakingClips.Keys.Count];

            int i = 0;
            foreach (var response in speakingClips.Keys)
            {
                choicePrompt += $"{i} -> {response}\n";
                choices[i] = response;
                i++;
            }

            choicePrompt += "Respond ONLY with the corresponding number of the line you would like to say. Do not return anything other than a number.";

            WendigosChatManager.SendPromptToChatService(
                choicePrompt,
                response =>
                {
                    Console.WriteLine("[Wendigos Chat Response]: " + response);
                    int choiceIndex;

                    try
                    {
                        choiceIndex = Convert.ToInt32(new string(response.SkipWhile(c => !char.IsDigit(c))
                             .TakeWhile(c => char.IsDigit(c))
                             .ToArray()));
                    }
                    catch
                    {
                        Console.WriteLine("[Wendigos Chat]: AI is dumb, choosing option 0");
                        choiceIndex = 0;
                    }

                    if (choiceIndex >= 0 && choiceIndex < choices.Length)
                    {
                        Plugin.MainThreadInvoker.Enqueue(() => Plugin.PlayLocalAudioClipAndQueue(closest_masked, choiceIndex));
                    }

                    midRequest = false;
                }
            );
        }

        private static string CleanString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            bool insideParens = false;
            bool insideAsterisks = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Check for Parentheses
                if (c == '(') { insideParens = true; continue; }
                if (c == ')' && insideParens) { insideParens = false; continue; }

                // Check for Asterisks (Treats the first * as open, second as close)
                if (c == '*')
                {
                    insideAsterisks = !insideAsterisks;
                    continue;
                }

                // Only add the character if we aren't inside either pair
                if (!insideParens && !insideAsterisks)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim();
        }

        public static void InitCallbacks()
        {
            Console.WriteLine(Plugin.root_path + "\\core\\qwbarch-NAudioLame");
            NAudio.Lame.LameDLL.LoadNativeDLL(Plugin.root_path + "\\core\\qwbarch-NAudioLame");

            AIManager.Instance.SpeechToTextService.OnRecognized += (s, e) =>
            {
                
                if (e.Result.Text.Length > 0 && e.Result.Reason == STTUtils.VoiceBoxResultReason.RecognizedSpeechWithTimestamps)
                {

                    num_retries = 0;
                    string cleaned_string = CleanString(e.Result.Text);
                    if (cleaned_string == "") return;

                    Plugin.MainThreadInvoker.Enqueue(() =>
                    {
                        Console.WriteLine($"[Wendigos STT] RECOGNIZED: {cleaned_string}");
                        try
                        {
                            // --- CALCULATE ABSOLUTE TIMES ---
                            // Convert Azure Ticks (relative to session start) to Real World Time
                            long offsetTicks = e.Result.OffsetInTicks;
                            //Console.WriteLine(e.Result.OffsetInTicks);
                            //Console.WriteLine(e.Result.Duration);
                            if (offsetTicks < 0) return;
                            TimeSpan offsetSpan = TimeSpan.FromTicks(offsetTicks);

                            DateTime speechAbsStart = _sttSessionStartTime + offsetSpan;
                            TimeSpan duration = e.Result.Duration;
                            if (duration.TotalMilliseconds == 0) return;

                            // --- EXTRACT ---
                            // We pass the Absolute Time. The buffer handles the math.
                            var clip = recorder.GetSegment(speechAbsStart, duration);

                            if (clip != null && clip.Length > 0)
                            {
                                if (speakingClips.Count >= MAX_CLIP_COUNT)
                                {
                                    var keyToRemove = speakingClips.Keys.ToList()[Plugin.serverRand.Next(((int)MAX_CLIP_COUNT))];
                                    speakingClips.Remove(keyToRemove);
                                }

                                speakingClips.TryAdd(cleaned_string, clip);
                                Console.WriteLine("Added clip successfully.");

                                
                            }
                            else
                            {
                                Console.WriteLine("[Wendigos STT] Could not extract audio segment (buffer might be empty or timing mismatch).");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Wendigos STT] Error processing audio clip: {ex}");
                        }

                        var closest_masked = Plugin.GetClosestRegisteredMasked();
                        if (closest_masked == null)
                            return;

                        if (!WendigosChatManager.init_success) return;

                        if (Plugin.enable_realtime_responses.Value)
                        {
                            
                            try
                            {
                                SendToChatAndStreamAudioResponse(closest_masked, player_name, e.Text);

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Wendigos STT] Realtime response error: {ex.ToString()}");
                            }
                        }
                        else
                        {
                            string truncatedText = e.Text;
                            if (truncatedText.Length > 128)
                            {
                                // 1. Calculate the raw starting index for the last 250 chars
                                int startIndex = truncatedText.Length - 128;

                                // 2. Check if we are splitting a word.
                                // We only need to adjust if we are NOT at the start of the string,
                                // the character BEFORE our cut is not a space (meaning the previous word continues),
                                // and the character AT our cut is not a space.
                                if (startIndex > 0 &&
                                    !char.IsWhiteSpace(truncatedText[startIndex - 1]) &&
                                    !char.IsWhiteSpace(truncatedText[startIndex]))
                                {
                                    // Find the next whitespace to skip the current partial word
                                    int nextSpace = truncatedText.IndexOf(' ', startIndex);

                                    if (nextSpace != -1)
                                    {
                                        // Move start index to the character immediately following the space
                                        startIndex = nextSpace + 1;
                                    }
                                }

                                // 3. Cut the string
                                truncatedText = truncatedText.Substring(startIndex);
                            }
                            Plugin.WendigosNetworkManager.Instance.RequestMaskedResponseServerRpc(closest_masked.GetComponent<Plugin.MaskedEnemyIdentifier>().id, player_name, cleaned_string);
                        }
                    });
                }
            };

            AIManager.Instance.SpeechToTextService.OnCanceled += (s, e) =>
            {
                Plugin.MainThreadInvoker.Enqueue(() =>
                {
                    Console.WriteLine($"[Wendigos STT] Speech to Text service cancelled: Reason={e.Reason} Error: {e.ErrorDetails}\nAttempting to reconnect... [{num_retries + 1}/3]");
                    num_retries++;
                    StopSpeechTranscription();
                    StartSpeechTranscription(Chat_System_Prompt);
                });
            };
        }

        public static void ChangeMicDevice(string deviceName)
        {
            if (!is_init) return;
            bool was_recognizing = is_recognizing;
            STT.StopSpeechTranscription();
            if (Plugin.STT_service.Value == "Azure")
                (config as AzureSTTServiceConfig).audioInputDeviceName = deviceName;
            else
                (config as ElevenlabsSTTServiceConfig).audioInputDeviceName = deviceName;
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
