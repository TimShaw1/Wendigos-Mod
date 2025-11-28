using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.IO.Compression;
using System.Buffers;
using Unity.Collections;
using Newtonsoft.Json;
using UnityEngine.XR;
using System.Net;
using System.Security.Cryptography;
using UnityEditor;
using System.Collections.Concurrent;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;
using NAudio.Wave;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.GUI;

// StartOfRound requires adding the game's Assembly-CSharp to dependencies

namespace Wendigos
{

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, "1.0.10")]
    public class Plugin : BaseUnityPlugin
    {
        public class WendigosNetworkManager : NetworkBehaviour
        {
            public static WendigosNetworkManager Instance { get; private set; }

            /// <summary>
            /// For most cases, you want to register once your NetworkBehaviour's
            /// NetworkObject (typically in-scene placed) is spawned.
            /// </summary>
            public override void OnNetworkSpawn()
            {
                base.OnNetworkSpawn();
            }

            internal static void ClientConnectInitializer(Scene sceneName, LoadSceneMode sceneEnum)
            {
                if (((Scene)(sceneName)).name == "SampleSceneRelay")
                {
                    GameObject val = new GameObject("WendigosMessageHandler");
                    val.AddComponent<WendigosNetworkManager>();
                    val.AddComponent<NetworkObject>();

                    PropertyInfo item = typeof(NetworkObject).GetProperty("NetworkObjectId", BindingFlags.Instance | BindingFlags.Public);
                    WriteToConsole("" + (item == null));
                    item.SetValue(val.GetComponent<NetworkObject>(), (System.UInt64)(127));
                    WriteToConsole("NETWORK MANAGER ID IS " + val.GetComponent<NetworkObject>().NetworkObjectId);

                    FieldInfo item2 = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    WriteToConsole("" + (item2 == null));
                    item2.SetValue(val.GetComponent<NetworkObject>(), (System.UInt32)(127));
                    //DontDestroyOnLoad(val);

                }
            }

            private void Awake()
            {
                Instance = this;
            }

            public override void OnNetworkDespawn()
            {
                if (IsServer)
                {
                    sharedMaskedClientDict.Clear();
                }

            }

            [ClientRpc]
            public void SetMaskedSuitClientRpc(string maskedId, int suitid)
            {
                try
                {
                    maskedInstanceLookup[maskedId].SetSuit(suitid);
                }
                catch (Exception ex)
                {
                    WriteToConsole(ex.Message);
                }
            }

            [ServerRpc(RequireOwnership = false)]
            public void AddToMaskedClientDictServerRpc(string maskedID, ulong clientID)
            {
                AddToMaskedClientDictClientRpc(maskedID, clientID);
            }

            [ClientRpc]
            public void AddToMaskedClientDictClientRpc(string maskedID, ulong clientID)
            {
                WriteToConsole("Trying to add masked to masked_client_dict");
                sharedMaskedClientDict[maskedID] = clientID;
                WriteToConsole($"added masked {maskedID} to masked_client_dict");
            }

            [ClientRpc]
            public void ShareVoiceIDClientRpc(ulong clientID, string VoiceID)
            {
                if (!clientVoiceIDLookup.ContainsKey(clientID))
                {
                    clientVoiceIDLookup.Add(clientID, VoiceID);
                    WriteToConsole("Client adding " + clientID + " " + VoiceID);
                }
            }

            [ClientRpc]
            public void InitAzureClientRpc()
            {
                WriteToConsole("AZURE MANAGER IS: " + AzureSTT.manager);
                if (enable_realtime_responses.Value && AzureSTT.manager == null)
                {
                    AzureSTT.num_gens = 0;
                    AzureSTT.Init(Azure_api_key.Value, Azure_region.Value, Azure_language.Value);
                }

                if (enable_realtime_responses.Value)
                    AzureSTT.StartSpeechTranscription(ChatGPT_prompt.Value);
            }

            [ServerRpc(RequireOwnership = false)]
            public void ShareAudioDataServerRpc(float[] mp3Data, string MaskedID, ServerRpcParams serverRpcParams = default)
            {
                ulong senderClientId = serverRpcParams.Receive.SenderClientId;

                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Except(new[] { senderClientId }).ToList()
                    }
                };

                PlayAudioDataClientRpc(mp3Data, MaskedID, clientRpcParams);
            }

            [ClientRpc]
            public void PlayAudioDataClientRpc(float[] mp3Data, string MaskedID, ClientRpcParams clientRpcParams = default)
            {
                var masked = maskedInstanceLookup[MaskedID];
                var identifier = masked.GetComponent<MaskedEnemyIdentifier>();
                //WriteToConsole("2");
                var queue = identifier.audioQueue;
                queue.Enqueue(mp3Data);
            }

        }

        public class WendigosLog
        {
            public bool generation_successful { get; set; }

            public DateTime last_successful_generation { get; set; }

            public string message { get; set; }

            public WendigosLog()
            {
                generation_successful = false;
                last_successful_generation = DateTime.MinValue;
                message = string.Empty;
            }

            public void Load()
            {
                if (File.Exists(assembly_path + "\\WendigosLog.json"))
                {
                    WendigosLog oldLog = ReadFromJsonFile<WendigosLog>(assembly_path + "\\WendigosLog.json");
                    generation_successful = oldLog.generation_successful;
                    last_successful_generation = oldLog.last_successful_generation;
                    message = oldLog.message;
                }
            }

            public void Save()
            {
                WriteToJsonFile<WendigosLog>(assembly_path + "\\WendigosLog.json", this);
            }
        }

        public static string[] LanguagesList = { 
                "en", "es", "fr", "de", "it", "pt", 
                "pl", "tr", "ru", "nl", "cs", "ar",
                "zh-cn", "ja", "hu", "ko", "hi"
            };
        public enum Languages
        {
            English,
            Spanish,
            French,
            German,
            Italian,
            Portuguese,
            Polish,
            Turkish,
            Russian,
            Dutch,
            Czech,
            Arabic,
            Chinese,
            Japanese,
            Hungarian,
            Korean,
            Hindi

        }

        public readonly struct LineType
        {
            public static readonly string Idle = "idle";
            public static readonly string Nearby = "Nearby";
            public static readonly string Chasing = "Chasing";
            public static readonly string Damaged = "Damaged";

            public LineType() { }
        }


        static void WriteToConsole(string output)
        {
            Console.WriteLine("Wendigos: " + output);
        }

        private static void Open_YT_URL()
        {
            UnityEngine.Application.OpenURL("https://www.youtube.com/@Tim-Shaw");
        }

        static string CalculateMainHash(string filename)
        {
            WriteToConsole("Calculating hash...");
            using (var hasher = SHA512.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = hasher.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        static string MAIN_HASH_VALUE = "20ca39002a389704d5499df0f522848ec21fe724f8d13de830d596f28df69a7ae860aa4bb58e0b7ddbefcdf3e96b902fc2f98fca37777a4bf08de15af231f36e";
        static bool main_downloaded = false;
        private static async Task download_main_exe()
        {
            if (File.Exists(assembly_path + "\\main.exe"))
            {
                WriteToConsole(CalculateMainHash(assembly_path + "\\main.exe"));
                if (CalculateMainHash(assembly_path + "\\main.exe").Equals(MAIN_HASH_VALUE))
                {
                    WriteToConsole("Valid main.exe");
                    main_downloaded = true;
                    sentenceTypesCompleted++;
                    return;
                }
                else
                {
                    WriteToConsole("INVALID main.exe already downloaded. Redownloading...");
                    File.Delete(assembly_path + "\\main.exe");
                    await download_main_exe();
                    return;
                }
            }

            WriteToConsole("Downloading main.exe for voice generation");

            using (WebClient wc = new WebClient())
            {
                //wc.Headers.Add("a", "a");
                try
                {
                    wc.DownloadFile("https://github.com/TimShaw1/Wendigos-Mod/releases/download/v0.1.0/main.exe", assembly_path + "\\main.exe");
                }
                catch (Exception ex)
                {
                    WriteToConsole(ex.Message);
                }
            }

            if (File.Exists(assembly_path + "\\main.exe"))
            {
                main_downloaded = true;
                sentenceTypesCompleted++;
                WriteToConsole("main.exe finished downloading");
                if (CalculateMainHash(assembly_path + "\\main.exe").Equals(MAIN_HASH_VALUE))
                {
                    WriteToConsole("Valid main.exe");
                    return;
                }
                else
                {
                    WriteToConsole("INVALID main.exe");
                    File.Delete(assembly_path + "\\main.exe");
                }
            }
            else
                WriteToConsole("main.exe failed to download");
        }

        /// <summary>
        /// Launch main.exe with args.
        /// This will DELETE any pre-existing folder \\audio_output\\player0\\{file_name}.
        /// </summary>
        static void GeneratePlayerSentences(string file_name, string sentences_file_path)
        {
            // Player0 only for now
            File.WriteAllText(assembly_path + "\\player_sentences\\player0_sentences.txt", File.ReadAllText(sentences_file_path));
            WriteToConsole("wrote to sentences text file");


            if (Directory.Exists(assembly_path + $"\\audio_output\\player0\\{file_name}"))
            {
                Directory.Delete(assembly_path + $"\\audio_output\\player0\\{file_name}", true);
                WriteToConsole($"deleted old wav files for {file_name}");
            }
            Directory.CreateDirectory(assembly_path + $"\\audio_output\\player0\\{file_name}");
            WriteToConsole($"created directory \\audio_output\\player0\\{file_name}");

            while (!main_downloaded)
                continue;


            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "cmd.exe";
            startInfo.WorkingDirectory = assembly_path;
            //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = $"/C (set PYTORCH_JIT=0)&(main.exe {file_name} {LanguagesList[((int)voice_language.Value)]})";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;

            try
            {
                // Start the process with the info we specified.
                // Call WaitForExit and then the using statement will close.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    WriteToConsole("started process");
                    exeProcess.OutputDataReceived += (sender, args) =>
                    {
                        WriteToConsole($"received output: {args.Data}");
                        if (args.Data.Contains("[y/n]"))
                        {
                            WriteToConsole("WAITING FOR MODEL DOWNLOAD ... (1.75gb)");

                            exeProcess.StandardInput.WriteLine("y");
                        }
                    };
                    exeProcess.ErrorDataReceived += (sender, args) => WriteToConsole(args.Data);
                    exeProcess.BeginOutputReadLine();
                    exeProcess.BeginErrorReadLine();
                    WriteToConsole($"LOADING MODEL {LanguagesList[((int)voice_language.Value)]}...");
                    exeProcess.WaitForExit();
                }
            }
            catch
            {
                // Log error.
            }

            File.Delete(assembly_path + "\\player_sentences\\player0_sentences.txt");
            WriteToConsole("deleted temporary sentences text file");
        }

        static async void GeneratePlayerSentencesElevenlabs(string file_name, string sentences_file_path)
        {
            WriteToConsole("IN ELEVENLABS GEN");

            if (Directory.Exists(assembly_path + $"\\audio_output\\player0\\{file_name}"))
            {
                Directory.Delete(assembly_path + $"\\audio_output\\player0\\{file_name}", true);
                WriteToConsole($"deleted old wav files for {file_name}");
            }
            Directory.CreateDirectory(assembly_path + $"\\audio_output\\player0\\{file_name}");
            WriteToConsole($"created directory \\audio_output\\player0\\{file_name}");

            string[] readText = File.ReadAllLines(sentences_file_path);
            int i = 0;
            int completedCounter = 0;
            foreach (string s in readText)
            {
                //Task.Factory.StartNew(() => elevenlabs_client.RequestAudio(s, elevenlabs_voice_id.Value, file_name + "0_line", assembly_path + $"\\audio_output\\player0\\{file_name}\\"));
                ElevenLabs.RequestAudio(
                    s, 
                    elevenlabs_voice_id.Value, 
                    file_name + "0_line", 
                    assembly_path + $"\\audio_output\\player0\\{file_name}\\", 
                    i,
                    result => completedCounter++
                );
                i++;
            }

            // Wait for each generation to complete
            while (i != completedCounter) continue;
        }

        static bool doneGenerating = false;
        static int sentenceTypesCompleted = 0;
        static void GenerateAllPlayerSentences(bool new_player_audio = false)
        {
            if (doneGenerating)
            {
                WriteToConsole("Already Generated");
                return;
            }

            string old_log_message = log.message;

            log.generation_successful = false;
            log.message = "Not finished generating player sentences";
            log.Save();

            bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
            bool new_idle, new_nearby, new_chasing, new_damaged;
            new_idle = new_nearby = new_chasing = new_damaged = new_player_audio;

            if (elevenlabs_enabled.Value)
            {
                found_sample_audio = true;
                WriteToConsole("ELEVENLABS ENABLED");
                if (old_log_message != "Elevenlabs")
                    new_idle = new_nearby = new_chasing = new_damaged = true;
            }
            else
            {
                if (old_log_message == "Elevenlabs")
                    new_idle = new_nearby = new_chasing = new_damaged = true;
            }

            if (!File.Exists(config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt"))
            {
                File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt",
                    "Help me\n" +
                    "Stop Sign Over Here\n" +
                    "Where is everyone?"
                    );
            }

            if (found_sample_audio && isFileChanged(config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt"))
            {
                new_idle = true;
            }
            if (new_idle)
            {
                WriteToConsole($"generating idle sentences");
                if (!elevenlabs_enabled.Value)
                    GeneratePlayerSentences("idle", config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt");
                else
                    GeneratePlayerSentencesElevenlabs("idle", config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt");
                sentenceTypesCompleted++;
                log.last_successful_generation = DateTime.Now;
            }


            if (!File.Exists(config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt"))
            {
                File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt",
                    "What's up?\n" +
                    "Find anything?\n" +
                    "haha yeah"
                );
            }
            if (found_sample_audio && isFileChanged(config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt"))
            {
                new_nearby = true;
            }
            if (new_nearby)
            {
                WriteToConsole($"generating nearby sentences");
                if (!elevenlabs_enabled.Value)
                    GeneratePlayerSentences("nearby", config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt");
                else
                    GeneratePlayerSentencesElevenlabs("nearby", config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt");
                sentenceTypesCompleted++;
                log.last_successful_generation = DateTime.Now;
            }


            if (!File.Exists(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt"))
            {
                File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt",
                "wait come back\n" +
                "where are you going?\n" +
                "bye"
                );
            }
            if (found_sample_audio && isFileChanged(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt"))
            {
                new_chasing = true;
            }
            if (new_chasing)
            {
                WriteToConsole($"generating chasing sentences");
                if (!elevenlabs_enabled.Value)
                    GeneratePlayerSentences("chasing", config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt");
                else
                    GeneratePlayerSentencesElevenlabs("chasing", config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt");
                sentenceTypesCompleted++;
                log.last_successful_generation = DateTime.Now;
            }

            if (!File.Exists(config_path + "Wendigos\\player_sentences\\player0_damaged_sentences.txt"))
            {
                File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_damaged_sentences.txt",
                "Ow stop\n" +
                "Stop I'm real\n" +
                "why"
                );
            }
            if (found_sample_audio && isFileChanged(config_path + "Wendigos\\player_sentences\\player0_damaged_sentences.txt"))
            {
                new_damaged = true;
            }
            if (new_damaged)
            {
                WriteToConsole($"generating damaged sentences");
                if (!elevenlabs_enabled.Value)
                    GeneratePlayerSentences("damaged", config_path + "Wendigos\\player_sentences\\player0_damaged_sentences.txt");
                else
                    GeneratePlayerSentencesElevenlabs("damaged", config_path + "Wendigos\\player_sentences\\player0_damaged_sentences.txt");
                sentenceTypesCompleted++;
                log.last_successful_generation = DateTime.Now;
            }

            log.generation_successful = true;
            if (elevenlabs_enabled.Value)
                log.message = "Elevenlabs";
            else
                log.message = "";
            log.Save();
            doneGenerating = true;
            GeneratePlayerAudioClips();
            sentenceTypesCompleted++;
            WriteToConsole("Finished generating voice lines.");
        }

        private static bool isFileChanged(string path)
        {
            DateTime timestamp = File.GetLastWriteTime(path);

            return timestamp > last_successful_generation;
        }

        public static void WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var contentsToWriteToFile = JsonConvert.SerializeObject(objectToWrite);
                writer = new StreamWriter(filePath, append);
                writer.Write(contentsToWriteToFile);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
                var fileContents = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(fileContents);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        private static ConfigEntry<bool> mod_enabled;
        private static ConfigEntry<bool> need_new_player_audio;
        private static ConfigEntry<Languages> voice_language;
        private static ConfigEntry<uint> talk_probability;
        private static ConfigEntry<bool> elevenlabs_enabled;
        private static ConfigEntry<string> elevenlabs_api_key;
        public static ConfigEntry<string> elevenlabs_voice_id;
        public static ConfigEntry<float> elevenlabs_voice_volume_boost;
        private static ConfigEntry<string> ChatGPT_api_key;
        private static ConfigEntry<string> ChatGPT_model;
        private static ConfigEntry<string> ChatGPT_prompt;
        private static ConfigEntry<string> Azure_api_key;
        private static ConfigEntry<string> Azure_region;
        private static ConfigEntry<string> Azure_language;
        private static ConfigEntry<bool> optimize_for_speed;
        private static ConfigEntry<bool> enable_realtime_responses;
        private static ConfigEntry<string> player_name;

        static System.Random serverRand = new System.Random();

        public static Dictionary<string, ulong> sharedMaskedClientDict = new Dictionary<string, ulong>();
        public static Dictionary<ulong, string> clientVoiceIDLookup = new Dictionary<ulong, string>();

        Harmony harmonyInstance = new Harmony("wendigos-instance");

        private static string config_path;
        public static string assembly_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // used to track if we need to generate new audio files
        private static DateTime last_successful_generation;
        private static WendigosLog log = new WendigosLog();

        internal static string mic_name;

        static AudioClip mic_audio_clip;

        static Dictionary<string, List<AudioClip>> myClips = new Dictionary<string, List<AudioClip>>();


        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            mod_enabled = Config.Bind<bool>(
                "General",
                "Enable mod?",
                false,
                "Enables the mod. If disabled, you can only hear other people's voices. Your voice will not be cloned."
                );

            need_new_player_audio = Config.Bind<bool>(
                "General",
                "Record new player sample audio?",
                true,
                "(Local AI model ONLY) Whether the record audio prompt should show up. Enable if you want to re-record your sample audio for the local ai"
                );

            voice_language = Config.Bind<Languages>(
                "General",
                "Language",
                Languages.English,
                "(Local AI model ONLY) What language the voice generator should use."
                );

            talk_probability = Config.Bind<uint>(
                "General",
                "Talk Probability",
                10,
                new ConfigDescription(
                "How likely (as a percentage) a masked talking is.",
                new AcceptableValueRange<uint>(0, 100))
                );

            elevenlabs_enabled = Config.Bind<bool>(
                "Elevenlabs",
                "Enabled",
                false,
                "Whether to use elevenlabs for ai voice generation"
                );

            if (elevenlabs_enabled.Value)
                need_new_player_audio.Value = false;

            elevenlabs_api_key = Config.Bind<string>(
                "Elevenlabs",
                "API key",
                "",
                "Your elevenlabs API key. Do NOT add extra characters like \""
                );

            elevenlabs_voice_id = Config.Bind<string>(
                "Elevenlabs",
                "Voice id",
                "",
                "Your elevenlabs voice id"
                );

            elevenlabs_voice_volume_boost = Config.Bind<float>(
                "Elevenlabs",
                "Masked Voice Volume Boost",
                1f,
                new ConfigDescription(
                "How much to boost the masked voices",
                new AcceptableValueRange<float>(0f,4f)
                )
                );

            ChatGPT_api_key = Config.Bind<string>(
                "ChatGPT",
                "API key",
                "",
                "Your ChatGPT API key. Do NOT add extra characters like \""
                );

            ChatGPT_model = Config.Bind<string>(
                "ChatGPT",
                "Model",
                "gpt-5-nano",
                "Which gpt model to use. Defaults to gpt-5-nano."

                );

            ChatGPT_prompt = Config.Bind<string>(
                "ChatGPT",
                "Prompt",
                "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.",
                "The prompt given to ChatGPT to determine what to say."
                );

            Azure_api_key = Config.Bind<string>(
                "Azure",
                "API key",
                "",
                "Your Azure API key. Do NOT add extra characters like \""
                );

            Azure_region = Config.Bind<string>(
                "Azure",
                "Region",
                "canadacentral",
                "Your Azure region"
                );

            Azure_language = Config.Bind<string>(
                "Azure",
                "Language",
                "en-US",
                "Your desired speech recognition language, list of supported languages can be found here: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=stt"
                );

            optimize_for_speed = Config.Bind<bool>(
                "Experimental",
                "Optimize Elevenlabs for Speed",
                false,
                "(English ONLY) Enable if you want extremely fast voice generation. Reduces the quality of the voice clone."
                );

            enable_realtime_responses = Config.Bind<bool>(
                "Experimental",
                "Realtime Responses",
                false,
                "Enables ChatGPT voice line generation so masked can reply in real time. You MUST have Elevenlabs, Azure, and ChatGPT api keys set."
                );

            player_name = Config.Bind<string>(
                "Experimental",
                "Your name",
                "",
                "Your name. Allows ChatGPT to know who is who"
                );

            GUIManager.CreateGUIManagerObject();
            

            // Allow players to hear voices even if mod is disabled
            SceneManager.sceneLoaded += WendigosNetworkManager.ClientConnectInitializer;

            if (!mod_enabled.Value)
            {
                var original = typeof(MaskedPlayerEnemy).GetMethod("Start");
                var postfix = typeof(MaskedStartPatch).GetMethod("Postfix");

                var original2 = typeof(MaskedPlayerEnemy).GetMethod("SetHandsOutClientRpc");
                var prefix = typeof(MaskedPlayerEnemyRemoveHands).GetMethod("Prefix");

                var original3 = typeof(MaskedPlayerEnemy).GetMethod("SetVisibilityOfMaskedEnemy");
                var postfix2 = typeof(MaskedPlayerEnemyVisibilityPatch).GetMethod("Postfix");

                harmonyInstance.Patch(original, postfix: new HarmonyMethod(postfix));
                harmonyInstance.Patch(original2, prefix: new HarmonyMethod(prefix));
                harmonyInstance.Patch(original3, postfix: new HarmonyMethod(postfix2));

                var types = Assembly.GetExecutingAssembly().GetTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                        if (attributes.Length > 0)
                        {
                            method.Invoke(null, null);
                        }
                    }
                }
            }


            if (mod_enabled.Value)
            {
                log.Load();
                log.Save();

                if (log.generation_successful)
                    last_successful_generation = log.last_successful_generation;
                else
                    last_successful_generation = DateTime.MinValue;

                harmonyInstance.PatchAll();

                var types = Assembly.GetExecutingAssembly().GetTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                        if (attributes.Length > 0)
                        {
                            method.Invoke(null, null);
                        }
                    }
                }



                WendigosChatManager.Init(ChatGPT_api_key.Value, ChatGPT_model.Value);
                AzureSTT.player_name = player_name.Value;
                if (optimize_for_speed.Value)
                    ElevenLabs.optimize_for_speed = true;
                try
                {
                    if (elevenlabs_enabled.Value)
                        ElevenLabs.Init(elevenlabs_api_key.Value, elevenlabs_voice_id.Value, elevenlabs_voice_volume_boost.Value);
                }
                catch (Exception ex)
                {
                    WriteToConsole(ex.ToString());
                }


                config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");

                System.IO.Directory.CreateDirectory(config_path + "Wendigos\\player_sentences");
                System.IO.Directory.CreateDirectory(assembly_path + "\\player_sentences");
                System.IO.Directory.CreateDirectory(assembly_path + "\\sample_player_audio");
                System.IO.Directory.CreateDirectory(assembly_path + "\\audio_output");
                System.IO.Directory.CreateDirectory(assembly_path + "\\temp_elevenlabs_lines");
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: Created/found config directories");

                bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: {(found_sample_audio ? "found" : "didn't find")} player sample audio");

                // start generating voice lines async
                doneGenerating = false;
                Task.Factory.StartNew(() => GenerateAllPlayerSentences(false));
            }

        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerDC))]
        class PlayerDCPatch
        {
            static void Prefix(int playerObjectNumber, ulong clientId)
            {

                if (WendigosNetworkManager.Instance.IsServer)
                {
                    var sharedMaskedClientDictCopy = new Dictionary<string, ulong>(sharedMaskedClientDict);

                    foreach (var maskedID in sharedMaskedClientDictCopy.Keys)
                    {
                        if (sharedMaskedClientDict[maskedID] == clientId)
                            sharedMaskedClientDict.Remove(maskedID);
                    }
                }
            }
        }

        public static string GetHashSHA1(byte[] data)
        {
            using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
            {
                return string.Concat(sha1.ComputeHash(data).Select(x => x.ToString("X2")));
            }
        }

        

        public static AudioClip LoadAudioFile(string audioFilePath)
        {
            return LoadWavFile(audioFilePath);
        }

        static AudioClip LoadWavFile(string audioFilePath)
        {
            if (File.Exists(audioFilePath))
            {
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(audioFilePath, AudioType.WAV))
                {
                    request.SendWebRequest();

                    while (request.result == UnityWebRequest.Result.InProgress)
                        continue;
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        WriteToConsole("www.error " + request.error);
                        WriteToConsole(" www.uri " + request.uri);
                        WriteToConsole(" www.url " + request.url);
                        WriteToConsole(" www.result " + request.result);
                        return null;
                    }
                    else
                    {
                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(request);
                        return myClip;
                    }
                }
            }
            WriteToConsole("AUDIO FILE NOT FOUND");
            return null;
        }

        public static MaskedPlayerEnemy GetClosestMasked()
        {
            var allPlayers = FindObjectsOfType<PlayerControllerB>();
            PlayerControllerB localPlayer = null;
            foreach (var player in allPlayers)
            {
                if (player.actualClientId == NetworkManager.Singleton.LocalClientId)
                    localPlayer = player;
            }

            try
            {
                var allMasked = FindObjectsOfType<MaskedPlayerEnemy>();
                WriteToConsole("COUNT: " + allMasked.Length.ToString());
                foreach (var masked in allMasked)
                {
                    var dist = Vector3.Distance(masked.transform.position, localPlayer.transform.position);
                    WriteToConsole("Masked dist is: " + dist);
                    if (dist < 20)
                    {
                        var id = masked.GetComponent<MaskedEnemyIdentifier>().id;
                        if (!sharedMaskedClientDict.Keys.Contains(id))
                            continue;
                        if (masked.isEnemyDead)
                            continue;
                        return masked;
                    }
                }
            } catch (Exception ex)
            {
                WriteToConsole(ex.ToString());
            }
            return null;
        }


        public static void PlayLocalAudioClipAndQueue(MaskedPlayerEnemy __instance, string type)
        {
            if (serverRand.Next(100) >= (100 - talk_probability.Value))
            {
                var clips = myClips[type];
                if (clips.Count > 0)
                {
                    var clip = clips[serverRand.Next(clips.Count)];
                    WriteToConsole("Playing clip type: " + type);
                    StreamingAudioDecoder decoder = new StreamingAudioDecoder();
                    decoder.Feed(clip);

                    List<float> accumulator = new List<float>();

                    // Loop until the decoder runs out of data
                    while (decoder.TryGetSample(out float sample))
                    {
                        accumulator.Add(sample);
                    }

                    WendigosNetworkManager.Instance.ShareAudioDataServerRpc(accumulator.ToArray(), __instance.GetComponent<MaskedEnemyIdentifier>().id);
                }
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.DoAIInterval))]
        class MaskedPlayerEnemyAIPatch
        {
            static void Prefix(MaskedPlayerEnemy __instance)
            {
                if (__instance.isEnemyDead)
                {
                    __instance.agent.speed = 0f;
                    return;
                }

                string thisMaskedID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                ulong MimickingClientID = 0;
                if (!sharedMaskedClientDict.Keys.Contains(thisMaskedID))
                {
                    //WriteToConsole("Masked not in dict");
                    return;
                }
                else
                {
                    MimickingClientID = sharedMaskedClientDict[thisMaskedID];
                }

                // Handle audio only on local client
                if (MimickingClientID != NetworkManager.Singleton.LocalClientId)
                    return;


                switch (__instance.currentBehaviourStateIndex)
                {
                    case 0:
                        // Chasing
                        if (__instance.CheckLineOfSightForClosestPlayer() != null)
                        {
                            // Play clip when can see player
                            if (!enable_realtime_responses.Value)
                            {
                                PlayLocalAudioClipAndQueue(__instance, LineType.Chasing);
                            }
                        }
                        // Nearby
                        else
                        {
                            PlayLocalAudioClipAndQueue(__instance, LineType.Nearby);
                        }

                        break;
                    case 1:
                        break;
                    case 2:
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.SetVisibilityOfMaskedEnemy))]
        class MaskedPlayerEnemyVisibilityPatch
        {
            public static void Postfix(MaskedPlayerEnemy __instance)
            {
                // Hide mask
                if ((bool)Traverse.Create(__instance).Field("enemyEnabled").GetValue())
                {
                    __instance.gameObject.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/spine.004/HeadMaskComedy").gameObject.SetActive(false);
                    __instance.gameObject.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/spine.004/HeadMaskTragedy").gameObject.SetActive(false);
                }
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.SetHandsOutClientRpc))]
        class MaskedPlayerEnemyRemoveHands
        {
            // Hide arms going out
            public static void Prefix(ref bool setOut, MaskedPlayerEnemy __instance)
            {
                setOut = false;
                string type = "chasing";

                string thisMaskedID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                if (!sharedMaskedClientDict.Keys.Contains(thisMaskedID))
                    return;

                ulong MimickingClientID = sharedMaskedClientDict[thisMaskedID];

                // Play clip when setting hands out
                if (!enable_realtime_responses.Value)
                    PlayLocalAudioClipAndQueue(__instance, LineType.Chasing);

            }

        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.HitEnemy))]
        class MaskedPlayerEnemyDamagePatch
        {
            static void Prefix(MaskedPlayerEnemy __instance)
            {
                try
                {
                    string thisMaskedID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                    ulong MimickingClientID = sharedMaskedClientDict[thisMaskedID];

                    // Speak when damaged
                    if (__instance.enemyHP > 0)
                        PlayLocalAudioClipAndQueue(__instance, LineType.Damaged);
                }
                catch
                {

                }
            }
        }

        public class MaskedEnemyIdentifier : MonoBehaviour
        {
            public string id;
            public Queue<float[]> audioQueue = new Queue<float[]>();

            private void OnAudioFilterRead(float[] data, int channels)
            {
                float[] newData;
                if (audioQueue.TryDequeue(out newData))
                {
                    for (int i = 0; i < newData.Length && i < data.Length; i++)
                        data[i] = newData[i];
                }
            }
        }

        static Dictionary<string, MaskedPlayerEnemy> maskedInstanceLookup = new Dictionary<string, MaskedPlayerEnemy>();

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        class MaskedStartPatch
        {
            public static void Postfix(MaskedPlayerEnemy __instance)
            {

                __instance.gameObject.AddComponent<MaskedEnemyIdentifier>();

                // id is starting position since only 1 enemy can spawn per vent
                __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id = __instance.transform.position.ToString();
                string ID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                maskedInstanceLookup.TryAdd(ID, __instance);
                WriteToConsole("Spawned Masked. ID: " + ID);

                AudioStreamer streamer = __instance.gameObject.AddComponent<AudioStreamer>();
                streamer.OnAudioSamplePlayed += (obj, data) => WendigosNetworkManager.Instance.ShareAudioDataServerRpc(data, ID);

                if (WendigosNetworkManager.Instance.IsServer)
                {
                    List<ulong> unassignedClientIDs = new List<ulong>();
                    WriteToConsole("Number of connected clients: " + NetworkManager.Singleton.ConnectedClientsIds.Count);

                    foreach (var clientID in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        if (!sharedMaskedClientDict.Values.Contains(clientID))
                            unassignedClientIDs.Add(clientID);
                    }
                    WriteToConsole("Created unasssigned list");

                    // All clients have been assigned a masked
                    if (unassignedClientIDs.Count == 0)
                        return;

                    ulong randomClientID = unassignedClientIDs[serverRand.Next() % unassignedClientIDs.Count];

                    WendigosNetworkManager.Instance.AddToMaskedClientDictServerRpc(
                                __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id,
                                randomClientID
                            );

                    var players = StartOfRound.Instance.allPlayerScripts;
                    foreach (var player in players)
                    {
                        if (player.actualClientId == randomClientID)
                        {
                            WendigosNetworkManager.Instance.SetMaskedSuitClientRpc(__instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id, player.currentSuitID);
                            break;
                        }
                    }
                }

                WriteToConsole("Finished Spawning Masked");
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
        class RoundManagerSpawnPatch
        {
            static void Prefix()
            {
                GUIManager.CreateGUIManagerObject();
                WriteToConsole("Created GUI Manager");
                WriteToConsole("Chat Manager Object is: " + WendigosChatManager.chatManagerComponent);
                WriteToConsole("Clearing chared masked dict");
                sharedMaskedClientDict.Clear();

                if (NetworkManager.Singleton.IsServer)
                {
                    WendigosNetworkManager.Instance.InitAzureClientRpc();
                }


                if (enable_realtime_responses.Value)
                {
                    
                    if (WendigosChatManager.chatManagerComponent == null)
                        WendigosChatManager.Init(ChatGPT_api_key.Value, ChatGPT_model.Value);
                    try
                    {
                        if (elevenlabs_enabled.Value && ElevenLabs.ttsManagerComponent == null)
                            ElevenLabs.Init(elevenlabs_api_key.Value, elevenlabs_voice_id.Value, elevenlabs_voice_volume_boost.Value);
                    }
                    catch (Exception ex)
                    {
                        WriteToConsole(ex.ToString());
                    }

                    foreach (var key in clientVoiceIDLookup.Keys)
                    {
                        WriteToConsole($"CLIENT IDS: {key} {clientVoiceIDLookup[key]}");
                    }
                    
                }
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
        class RoundManagerEndPatch
        {
            static void Prefix()
            {
                try
                {
                    // reset speech recognition
                    AIManager.Instance.StopSpeechTranscription();
                    
                }
                catch (Exception ex)
                {
                    WriteToConsole(ex.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.LoadSettingsFromPrefs))]
        class IngamePlayerSettingsLoadPatch
        {
            static void Postfix(IngamePlayerSettings __instance)
            {
                mic_name = IngamePlayerSettings.Instance.settings.micDevice;
                WriteToConsole(mic_name);
            }
        }

        [HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.SaveChangedSettings))]
        class IngamePlayerSettingsMicSavePatch
        {
            static void Postfix(IngamePlayerSettings __instance)
            {
                // changes mic to primary mic
                mic_name = IngamePlayerSettings.Instance.settings.micDevice;
                WriteToConsole("Set to " + mic_name);
            }
        }

        static string[] lines_to_read = """
            Prosecutors have opened a massive investigation into allegations of fixing games and illegal betting.
            Different telescope designs perform differently and have different strengths and weaknesses.
            We can continue to strengthen the education of good lawyers.
            Feedback must be timely and accurate throughout the project.
            Humans also judge distance by using the relative sizes of objects.
            Churches should not encourage it or make it look harmless.
            Learn about setting up wireless network configuration.
            You can eat them fresh, cooked or fermented.
            If this is true then those who tend to think creatively really are somehow different.
            She will likely jump for joy and want to skip straight to the honeymoon.
            The sugar syrup should create very fine strands of sugar that drape over the handles.
            But really in the grand scheme of things, this information is insignificant.
            I let the positive overrule the negative.
            He wiped his brow with his forearm.
            Instead of fixing it, they give it a nickname.
            About half the people who are infected also lose weight.
            The second half of the book focuses on argument and essay writing.
            We have the means to help ourselves.
            The large items are put into containers for disposal.
            He loves to watch me drink this stuff.
            Still, it is an odd fashion choice.
            Funding is always an issue after the fact.
            Let us encourage each other.
            Subscribe to @Tim-Shaw on YouTube
            """.Split('\n').OrderBy(a => serverRand.Next()).ToArray();

        public static byte[] ConvertToByteArr(AudioClip clip)
        {
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            int length = samples.Length;
            writer.Write(length);

            foreach (var sample in samples)
            {
                writer.Write(sample);
            }

            return stream.ToArray();
        }

        public static AudioClip LoadAudioClip(byte[] receivedBytes, int sampleRate = 24000)
        {
            float[] samples = new float[receivedBytes.Length / 4]; //size of a float is 4 bytes

            Buffer.BlockCopy(receivedBytes, 0, samples, 0, receivedBytes.Length);

            int channels = 1; //Assuming audio is mono because microphone input usually is

            AudioClip clip = AudioClip.Create("NONAME", samples.Length, channels, sampleRate, false);
            clip.SetData(samples, 0);

            return clip;
        }

        public static void GeneratePlayerAudioClips()
        {
            myClips.Clear();

            // Generate audio clips
            byte count = 0;
            myClips.Add(LineType.Idle, new List<AudioClip>());
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\idle"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "i" + count;
                myClips[LineType.Idle].Add(clip);
                count++;
            }

            count = 0;
            myClips.Add(LineType.Nearby, new List<AudioClip>());
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\nearby"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "n" + count;
                myClips[LineType.Nearby].Add(clip);
                count++;
            }

            count = 0;
            myClips.Add(LineType.Chasing, new List<AudioClip>());
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\chasing"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "c" + count;
                myClips[LineType.Chasing].Add(clip);
                count++;
            }

            count = 0;
            myClips.Add(LineType.Damaged, new List<AudioClip>());
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\damaged"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "d" + count;
                myClips[LineType.Damaged].Add(clip);
                count++;
            }

            WriteToConsole("Generated Player Clips");
            foreach (var key in myClips.Keys)
            {
                WriteToConsole(key + " count: " +  myClips[key].Count);
            }
            
        }

        [HarmonyPatch(typeof(MenuManager), "Start")]
        class MenuManagerPatch
        {
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene)
                {
                    Task.Factory.StartNew(GeneratePlayerAudioClips);
                    if (!elevenlabs_enabled.Value)
                        Task.Factory.StartNew(download_main_exe);
                    return;
                }

                // Show record audio prompt
                __instance.NewsPanel.SetActive(false);
                _ = TimShaw.VoiceBox.GUI.GUIManager.CreateGUIManagerObject();
                if (!File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav") || need_new_player_audio.Value)
                {
                    if (!elevenlabs_enabled.Value)
                    {
                        need_new_player_audio.Value = true;
                        __instance.DisplayMenuNotification($"Press R to record some voice lines.\nSelected Mic is {mic_name}", "[ Close ]");
                        Transform responseButton = __instance.menuNotification.transform.Find("Panel").Find("ResponseButton");
                        responseButton.transform.position = new Vector3(responseButton.transform.position.x, responseButton.transform.position.y - 10, responseButton.transform.position.z);
                    }
                }
                else
                {
                    if (doneGenerating == false)
                    {
                        __instance.DisplayMenuNotification($"Please wait for audio clips to finish generating", "[ close ]");
                        GeneratingAnimation(__instance);
                    }
                }
            }
        }

        static async void GeneratingAnimation(MenuManager __instance)
        {
            string[] characterList = ["/", "-", "\\", "|"];
            __instance.menuNotificationText.text += "[" + sentenceTypesCompleted + "/6] |";
            while (!doneGenerating)
            {
                foreach (string c in characterList)
                {
                    __instance.menuNotificationText.text = __instance.menuNotificationText.text.Remove(__instance.menuNotificationText.text.Length-7);
                    __instance.menuNotificationText.text += "[" + sentenceTypesCompleted + "/6] "+ c;
                    await Task.Delay(200);
                }
            }
        }

        [HarmonyPatch(typeof(MenuManager), "Update")]
        class MenuManagerUpdatePatch
        {
            static int index = 0;
            static bool recorded = false;
            static Task task1 = null;
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene) { return; }
                if (!__instance.menuNotification.activeInHierarchy) { return; }
                if (elevenlabs_enabled.Value) { return; }

                if (!Microphone.IsRecording(mic_name) && !recorded)
                {
                    if (UnityInput.Current.GetKeyUp("R"))
                    {
                        recorded = true;
                        // Get max frequency of mic device
                        int minfreq;
                        int maxfreq;
                        Microphone.GetDeviceCaps(mic_name, out minfreq, out maxfreq);

                        // Max 10 minutes
                        mic_audio_clip = Microphone.Start(mic_name, false, 600, maxfreq);
                        __instance.menuNotificationButtonText.text = "Recording...";
                        __instance.menuNotificationText.text = "Press Q to quit recording\nPress N for next line\n- - "+ (index+1) + "/" +lines_to_read.Length +" - -\n" + lines_to_read[index];
                    }
                }
                else
                {
                    if (UnityInput.Current.GetKeyUp("Q") && need_new_player_audio.Value)
                    {
                        Microphone.End(mic_name);
                        __instance.menuNotificationButtonText.text = "[ don't close ]";
                        __instance.menuNotificationText.text = "Recording stopped.\nPlease wait for audio clips to finish generating ";
                        SavWav.Save(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav", mic_audio_clip, true);
                        doneGenerating = false;
                        if (task1 == null)
                            task1 = Task.Factory.StartNew(() => GenerateAllPlayerSentences(true));
                        need_new_player_audio.Value = false;
                        GeneratingAnimation(__instance);


                    }
                    else if (UnityInput.Current.GetKeyUp("N") && need_new_player_audio.Value)
                    {
                        if (index + 1 < lines_to_read.Length)
                        {
                            index++;
                            __instance.menuNotificationText.text = "Press Q to quit recording\nPress N for next line\n- - " + (index+1) + "/" + lines_to_read.Length + " - -\n" + lines_to_read[index];
                        }
                        else
                        {
                            Microphone.End(mic_name);
                            __instance.menuNotificationButtonText.text = "[ don't close ]";
                            __instance.menuNotificationText.text = "Recording stopped.\nPlease wait for audio clips to finish generating ";
                            SavWav.Save(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav", mic_audio_clip, true);
                            doneGenerating = false;
                            if (task1 == null)
                                task1 = Task.Factory.StartNew(() => GenerateAllPlayerSentences(true));
                            need_new_player_audio.Value = false;
                            GeneratingAnimation(__instance);

                        }
                    }
                }

                if (doneGenerating && !need_new_player_audio.Value)
                {
                    __instance.menuNotificationButtonText.text = "[ close ]";
                    __instance.menuNotificationText.text = "Voice lines finished generating!";
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ShowNameBillboard))]
        class HidePlayerNamePatch
        {
            static void Postfix(PlayerControllerB __instance)
            {
                __instance.usernameAlpha.alpha = 0f;
            }
        }

    }
}