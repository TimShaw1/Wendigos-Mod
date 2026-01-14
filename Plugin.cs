using BepInEx;
using BepInEx.Configuration;
using Dissonance;
using GameNetcodeStuff;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.GUI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// StartOfRound requires adding the game's Assembly-CSharp to dependencies

namespace Wendigos
{

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, "2.0.1")]
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

                NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;

                ShareVoiceIDServerRpc(NetworkManager.Singleton.LocalClientId, TTS_voice_id.Value);
                ShareNameServerRpc(NetworkManager.Singleton.LocalClientId, player_name.Value);
            }

            private void OnClientConnectedCallback(ulong obj)
            {
                if (IsServer)
                {
                    foreach (ulong connectedClient in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        foreach (var key in clientVoiceIDLookup.Keys.ToArray())
                        {
                            ShareVoiceIDClientRpc(key, clientVoiceIDLookup[key]);
                        }

                        foreach (var key in clientNameLookup.Keys.ToArray())
                        {
                            ShareNameClientRpc(key, clientNameLookup[key]);
                        }
                    }

                    WriteToConsole(clientVoiceIDLookup.Values.ToArray().ToString());
                    WriteToConsole(clientNameLookup.Values.ToArray().ToString());

                    if (enable_config_sync.Value)
                        PromptToShareDataWithClients();

                }
            }

            internal static void ClientConnectInitializer(Scene sceneName, LoadSceneMode sceneEnum)
            {
                if (((Scene)(sceneName)).name == "SampleSceneRelay")
                {
                    var objectIdHash = BitConverter.ToUInt32(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes($"{PluginInfo.PLUGIN_GUID}.MyNetworkPrefab")));

                    GameObject val = new GameObject("WendigosMessageHandler");
                    val.AddComponent<WendigosNetworkManager>();
                    val.AddComponent<NetworkObject>();

                    PropertyInfo item = typeof(NetworkObject).GetProperty("NetworkObjectId", BindingFlags.Instance | BindingFlags.Public);
                    WriteToConsole("" + (item == null));
                    item.SetValue(val.GetComponent<NetworkObject>(), (System.UInt64)(objectIdHash));
                    WriteToConsole("NETWORK MANAGER ID IS " + val.GetComponent<NetworkObject>().NetworkObjectId);

                    FieldInfo item2 = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    WriteToConsole("" + (item2 == null));
                    item2.SetValue(val.GetComponent<NetworkObject>(), (System.UInt32)(objectIdHash));
                    //DontDestroyOnLoad(val);

                }
            }

            private void Awake()
            {
                if (Instance != null)
                {
                    Destroy(this);
                    return;
                }

                Instance = this;
            }

            private void Update()
            {
                while (MainThreadInvoker._actions.TryDequeue(out var action))
                {
                    action();
                }
            }

            public override void OnNetworkDespawn()
            {
                STT.StopSpeechTranscription();
                WendigosChatManager.chats.Clear();

                sharedMaskedClientDict.Clear();
                clientNameLookup.Clear();
                clientVoiceIDLookup.Clear();
                STT.speakingClips.Clear();

                NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;

            }
            #region RPCS

            [ClientRpc]
            public void SetMaskedSuitClientRpc(string maskedId, int suitid)
            {
                try
                {
                    Task.Run(() => WaitThenSetSuit(maskedId, suitid));
                }
                catch (Exception ex)
                {
                    WriteToConsole("Error trying to set masked suit: " + ex.Message);
                }
            }

            private async Task WaitThenSetSuit(string maskedId, int suitid)
            {
                int attempts = 0;
                while (attempts < 3 && !maskedInstanceLookup.Keys.Contains(maskedId)) { await Task.Delay(100); attempts++; }
                maskedInstanceLookup[maskedId].SetSuit(suitid);
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
                if (sharedMaskedClientDict.TryAdd(maskedID, clientID))
                    WriteToConsole($"Added masked {maskedID} to masked_client_dict"); 
                else
                    WriteToConsole($"Failed to add masked {maskedID} to masked_client_dict");
            }

            [ServerRpc(RequireOwnership = false)]
            public void ShareVoiceIDServerRpc(ulong clientID, string VoiceID)
            {
                if (!clientVoiceIDLookup.ContainsKey(clientID))
                {
                    clientVoiceIDLookup.Add(clientID, VoiceID);
                    WriteToConsole("Server added " + clientID + " " + VoiceID);
                }
                else
                {
                    if (clientVoiceIDLookup[clientID] == "") clientVoiceIDLookup[clientID] = VoiceID;
                    WriteToConsole("Server has " + clientID + " " + VoiceID);
                }
                ShareVoiceIDClientRpc(clientID, VoiceID);
            }

            [ClientRpc]
            public void ShareVoiceIDClientRpc(ulong clientID, string VoiceID)
            {
                if (!clientVoiceIDLookup.ContainsKey(clientID))
                {
                    clientVoiceIDLookup.Add(clientID, VoiceID);
                    WriteToConsole("Client added " + clientID + " " + VoiceID);
                }
                else
                {
                    if (clientVoiceIDLookup[clientID] == "") clientVoiceIDLookup[clientID] = VoiceID;
                    WriteToConsole("Client has " + clientID + " " + VoiceID);
                }
            }

            [ServerRpc(RequireOwnership = false)]
            public void ShareNameServerRpc(ulong clientID, string name)
            {
                if (!clientNameLookup.ContainsKey(clientID))
                {
                    clientNameLookup.Add(clientID, name);
                    WriteToConsole("Server added " + clientID + " " + name);
                }
                else
                {
                    WriteToConsole("Server has " + clientID + " " + name);
                }
            }

            [ClientRpc]
            public void ShareNameClientRpc(ulong clientID, string name)
            {
                if (!clientNameLookup.ContainsKey(clientID))
                {
                    clientNameLookup.Add(clientID, name);
                    WriteToConsole("Client added " + clientID + " " + name);
                }
                else
                {
                    WriteToConsole("Client has " + clientID + " " + name);
                }
            }

            [ClientRpc]
            public void InitSTTClientRpc()
            {
                WriteToConsole("STT MANAGER IS: " + STT.manager);
                if (STT.manager == null)
                {
                    STT.num_gens = 0;
                    STT.Init(ChooseApiKey(STT_api_key.Value, temp_STT_api_key), STT_region.Value, STT_language.Value, mic_name);
                }

                STT.StartSpeechTranscription(Chat_prompt.Value);
            }

            [ServerRpc(RequireOwnership = false)]
            public void ShareAudioDataServerRpc(byte[] audioData, string MaskedID, ServerRpcParams serverRpcParams = default)
            {
                ulong senderClientId = serverRpcParams.Receive.SenderClientId;

                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Except(new[] { senderClientId }).ToList()
                    }
                };

                PlayAudioDataClientRpc(audioData, MaskedID, clientRpcParams);
            }

            [ClientRpc]
            public void PlayAudioDataClientRpc(byte[] audioData, string MaskedID, ClientRpcParams clientRpcParams = default)
            {
                var masked = maskedInstanceLookup[MaskedID];
                var identifier = masked.GetComponent<MaskedEnemyIdentifier>();
                //WriteToConsole("2");
                if (identifier != null)
                {
                    var audioComponent = identifier.child.GetComponent<MaskedAudioComponent>();
                    audioComponent.audioQueue.Feed(audioData);
                }
            }

            [ServerRpc(RequireOwnership = false)]
            public void RequestMaskedResponseServerRpc(string maskedID, string playerName, string playerSpeech, bool respondingToPlayer = true)
            {
                var clientID = Plugin.sharedMaskedClientDict[maskedID];

                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = [clientID]
                    }
                };

                RequestMaskedResponseClientRpc(maskedID, playerName, playerSpeech, respondingToPlayer, clientRpcParams);
            }

            [ClientRpc]
            public void RequestMaskedResponseClientRpc(string maskedID, string playerName, string playerSpeech, bool respondingToPlayer = true, ClientRpcParams clientParams = default)
            {
                STT.SendToChatAndChooseResponse(maskedInstanceLookup[maskedID], playerName, playerSpeech, respondingToPlayer);
            }

            private void PromptToShareDataWithClients()
            {
                var confirmationGUI = SimpleConfirmationGUI.CreateConfirmationGUI(
                    "Do you want to share your Wendigos config with all clients (including API keys?). Close this window to decline.",
                    false
                    );

                confirmationGUI.onButtonClicked = () =>
                {
                    if (IsServer) ShareConfigToClientsServerRpc();
                };
            }

            [ServerRpc(RequireOwnership = false)]
            public void ShareConfigToClientsServerRpc(ServerRpcParams serverRpcParams = default)
            {
                ulong senderClientId = serverRpcParams.Receive.SenderClientId;

                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Except(new[] { senderClientId }).ToList()
                    }
                };

                // Send data to non-host clients
                RecieveConfigClientRpc(
                    ChatServiceProvider.Value,
                    Chat_api_key.Value,
                    Chat_model.Value,
                    Chat_prompt.Value,

                    enable_realtime_responses.Value,

                    max_clip_count.Value,
                    talk_probability.Value,

                    STT_service.Value,
                    STT_api_key.Value,
                    STT_region.Value,
                    STT_language.Value,

                    TTS_enabled.Value,
                    TTS_api_key.Value,
                    clientRpcParams
                );
            }

            [ClientRpc]
            public void RecieveConfigClientRpc(
                // Chat
                string chatServiceProvider, 
                string chatApiKey, 
                string chatModel, 
                string prompt, 

                // Experimental
                bool realtimeResponses,

                // General
                uint maxClips,
                uint talkProbability,
                
                // STT
                string sttService,
                string sttApiKey,
                string region,
                string language,
                
                // TTS
                bool ttsEnabled,
                string ttsApiKey,
                ClientRpcParams clientParams = default
            )
            {
                string guiPrompt = "Host has requested to sync Wendigos config with you!";
                if (TTS_api_key.Value != "")
                    guiPrompt += " You already have a TTS api key set so TTS options will be skipped.";

                if (STT_api_key.Value != "")
                    guiPrompt += " You already have an STT api key set so STT options will be skipped.";

                if (Chat_api_key.Value != "")
                    guiPrompt += " You already have a Chat api key set so Chat options will be skipped.";

                guiPrompt += " Are you sure you want to continue?";
                var confirmationGUI = SimpleConfirmationGUI.CreateConfirmationGUI(guiPrompt, true);

                confirmationGUI.onButtonClicked = () =>
                {
                    if (Chat_api_key.Value == "")
                    {
                        ChatServiceProvider.Value = chatServiceProvider;
                        //Chat_api_key.Value = chatApiKey;
                        temp_Chat_api_key = chatApiKey;
                        Chat_model.Value = chatModel;
                        Chat_prompt.Value = prompt;
                        WendigosChatManager.Init(ChooseApiKey(Chat_api_key.Value, temp_Chat_api_key), Chat_model.Value, ChatServiceProvider.Value);
                    }

                    enable_realtime_responses.Value = realtimeResponses;

                    max_clip_count.Value = maxClips;
                    talk_probability.Value = talkProbability;

                    if (STT_api_key.Value == "")
                    {
                        STT_service.Value = sttService;
                        //STT_api_key.Value = sttApiKey;
                        temp_STT_api_key = sttApiKey;
                        STT_region.Value = region;
                        STT_language.Value = language;
                    }

                    if (TTS_api_key.Value == "")
                    {
                        TTS_enabled.Value = ttsEnabled;
                        //TTS_api_key.Value = ttsApiKey;
                        temp_TTS_api_key = ttsApiKey;
                    }

                    if (ElevenLabs.ttsManagerComponent == null)
                    {
                        if (TTS_enabled.Value || enable_realtime_responses.Value)
                            ElevenLabs.Init(ChooseApiKey(TTS_api_key.Value, temp_TTS_api_key), TTS_voice_id.Value, TTS_voice_volume.Value);
                    }
                };
            }
            #endregion

        }

        public class MainThreadInvoker
        {
            public static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

            public static void Enqueue(Action action)
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));

                _actions.Enqueue(action);
            }
        }

        #region CONFIG

        public static System.Random serverRand = new System.Random();

        // --------------- CONFIG ---------------
        private static ConfigEntry<bool> mod_enabled;
        public static ConfigEntry<uint> max_clip_count;
        private static ConfigEntry<uint> talk_probability;
        private static ConfigEntry<bool> TTS_enabled;
        private static ConfigEntry<string> TTS_api_key;
        public static ConfigEntry<string> TTS_voice_id;
        public static ConfigEntry<float> TTS_voice_volume;
        public static ConfigEntry<string> player_voice_description;
        private static ConfigEntry<string> ChatServiceProvider;
        private static ConfigEntry<string> Chat_api_key;
        private static ConfigEntry<string> Chat_model;
        private static ConfigEntry<string> Chat_prompt;
        public static ConfigEntry<string> STT_service;
        private static ConfigEntry<string> STT_api_key;
        private static ConfigEntry<string> STT_region;
        private static ConfigEntry<string> STT_language;
        public static ConfigEntry<bool> enable_realtime_responses;
        private static ConfigEntry<bool> enable_config_sync;
        public static ConfigEntry<string> player_name;

        // --------------- LOOKUPS -----------------
        public static Dictionary<string, ulong> sharedMaskedClientDict = new Dictionary<string, ulong>();
        public static Dictionary<ulong, string> clientNameLookup = new Dictionary<ulong, string>();
        public static Dictionary<ulong, string> clientVoiceIDLookup = new Dictionary<ulong, string>();
        static Dictionary<string, MaskedPlayerEnemy> maskedInstanceLookup = new Dictionary<string, MaskedPlayerEnemy>();

        // --------------- MISC -----------------

        Harmony harmonyInstance = new Harmony("wendigos-instance");

        private static string config_path;
        public static string root_path = BepInEx.Paths.BepInExRootPath;
        public static string assembly_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        internal static string mic_name;

        public static string temp_TTS_api_key = "";
        public static string temp_Chat_api_key = "";
        public static string temp_STT_api_key = "";
        #endregion

        #region UTILITY_FUNCTIONS
        static void WriteToConsole(string output)
        {
            Console.WriteLine("[Wendigos Log] " + output);
        }

        private static void Open_YT_URL()
        {
            UnityEngine.Application.OpenURL("https://www.youtube.com/@Tim-Shaw");
        }

        public static string ChooseApiKey(string configApiKey, string tempApiKey)
        {
            return configApiKey == "" ? tempApiKey : configApiKey;
        }

        public static MaskedPlayerEnemy GetClosestRegisteredMasked()
        {
            PlayerControllerB localPlayer = StartOfRound.Instance.localPlayerController;

            try
            {
                var allMasked = FindObjectsOfType<MaskedPlayerEnemy>();
                WriteToConsole("COUNT: " + allMasked.Length.ToString());
                foreach (var masked in allMasked)
                {
                    var dist = Vector3.Distance(masked.eye.position, localPlayer.gameplayCamera.transform.position);
                    WriteToConsole("Masked dist is: " + dist);
                    if (masked.CheckLineOfSightForClosestPlayer(80, 50, 30) != null)
                    {
                        var id = masked.GetComponent<MaskedEnemyIdentifier>().id;
                        if (!sharedMaskedClientDict.Keys.Contains(id))
                            continue;
                        if (masked.isEnemyDead)
                            continue;
                        return masked;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToConsole(ex.ToString());
            }
            return null;
        }

        public static void TeleportMaskedToLocalPlayer(MaskedPlayerEnemy __instance)
        {
            Task.Run(async () => {
                await Task.Delay(3000);
                MainThreadInvoker.Enqueue(() =>
                {
                    MethodInfo dynMethod = __instance.GetType().GetMethod("TeleportMaskedEnemyAndSync",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    dynMethod.Invoke(__instance, new object[] { RoundManager.Instance.GetNavMeshPosition(StartOfRound.Instance.localPlayerController.transform.position), true });
                }
                );
            });
        }


        public static void PlayLocalAudioClipAndQueue(MaskedPlayerEnemy __instance, int clipChoice = -1)
        {
            MaskedEnemyIdentifier identifier = __instance.GetComponent<MaskedEnemyIdentifier>();

            var audioComponent = identifier.child.GetComponent<MaskedAudioComponent>();
            var clips = STT.speakingClips.Values.ToList();
            WriteToConsole("Clips count: " + clips.Count.ToString());
            if (clips.Count > 0)
            {
                byte[] clip;
                if (clipChoice == -1)
                    clip = clips[serverRand.Next(clips.Count)];
                else
                    clip = clips[clipChoice];
                    
                WriteToConsole($"Playing clip {(clipChoice == -1 ? "random" : clipChoice)}. Length: {clip.Length}");

                // __instance.creatureVoice.Play();
                identifier.child.GetComponent<MaskedAudioComponent>().audioQueue.Feed(clip);
                // Configuration
                const int MAX_CHUNK_SIZE = 32768;
                int clipPosition = 0;
                int totalLength = clip.Length;
                    

                // Loop until we have processed the entire clip
                while (clipPosition < totalLength)
                {
                    // 1. Calculate the size of the current chunk
                    // It will be MAX_CHUNK_SIZE, unless we are at the very end and have less than MAX_CHUNK_SIZE remaining.
                    int currentChunkSize = System.Math.Min(MAX_CHUNK_SIZE, totalLength - clipPosition);

                    // 2. Create the chunk buffer
                    byte[] chunk = new byte[currentChunkSize];

                    // 3. Copy data from the main clip into the chunk
                    // specific arguments: (source, sourceIndex, destination, destIndex, length)
                    System.Array.Copy(clip, clipPosition, chunk, 0, currentChunkSize);

                    // --- YOUR CALLS BELOW ---

                    // 2. Feed the LOCAL Audio Engine
                    // (We pass the chunk directly here so the local playback happens in sync with the network send)
                    //identifier.child.GetComponent<MaskedAudioComponent>().audioQueue.Feed(chunk);

                    // 3. Send Network RPC
                    // (We pass the chunk instead of the whole accumulator)
                    WendigosNetworkManager.Instance.ShareAudioDataServerRpc(chunk, identifier.id);

                    // ------------------------

                    // Advance the position
                    clipPosition += currentChunkSize;
                }

                WriteToConsole("Sent audio data");
                
            }

            return;
        }

        public static void InitMaskedAudio(MaskedPlayerEnemy __instance)
        {
            var identifier = __instance.gameObject.AddComponent<MaskedEnemyIdentifier>();

            GameObject maskedAudioStreamer = new GameObject("MaskedAudioStreamer");
            maskedAudioStreamer.transform.position = __instance.transform.position;
            maskedAudioStreamer.transform.parent = __instance.transform;

            identifier.child = maskedAudioStreamer;

            // id is starting position since only 1 enemy can spawn per vent
            identifier.id = __instance.transform.position.ToString();
            string ID = identifier.id;
            maskedInstanceLookup.TryAdd(ID, __instance);
            WriteToConsole("Spawned Masked. ID: " + ID);

            AudioStreamer streamer = maskedAudioStreamer.AddComponent<AudioStreamer>();

            __instance.creatureVoice.GetComponent<OccludeAudio>().CopyOcclusion(maskedAudioStreamer);

            MaskedAudioComponent audioComponent = maskedAudioStreamer.AddComponent<MaskedAudioComponent>();

            // Autoclone
            if (STT.speakingClips.Count >= STT.MAX_CLIP_COUNT && TTS_voice_id.Value == "")
            {
                // Hack - init to access ttsManagerComponent to call CloneVoiceAndGetVoiceIDAsync
                if (ElevenLabs.ttsManagerComponent == null)
                    ElevenLabs.Init(ChooseApiKey(TTS_api_key.Value, temp_TTS_api_key), TTS_voice_id.Value, TTS_voice_volume.Value);

                TTS_voice_id.Value = "pending";

                ElevenLabs.ttsManagerComponent.CloneVoiceAndGetVoiceIDAsync(
                        STT.speakingClips.Values.ToList(),
                        player_name.Value == "" ? "WendigosClone" : player_name.Value + "WendigosClone",
                        player_voice_description.Value,
                        false
                ).ContinueWith(
                    async(result) =>
                    {
                        TTS_voice_id.Value = result.Result;
                        MainThreadInvoker.Enqueue(() => {
                            // Properly init now
                            WriteToConsole("Cloned player voice. VoiceID: " + result.Result);
                            WendigosNetworkManager.Instance.ShareVoiceIDServerRpc(NetworkManager.Singleton.LocalClientId, result.Result);
                            ElevenLabs.Init(ChooseApiKey(TTS_api_key.Value, temp_TTS_api_key), TTS_voice_id.Value, TTS_voice_volume.Value);
                            ElevenLabs.ttsManagerComponent.TextToSpeechService.OnAudioDataReceived += (obj, data) =>
                            {
                                identifier.audioNetworkQueue.Enqueue(data);
                            };
                        });
                    }
                );
            }
            else
            {

                ElevenLabs.ttsManagerComponent?.TextToSpeechService.OnAudioDataReceived += (obj, data) =>
                {
                    identifier.audioNetworkQueue.Enqueue(data);
                };
            }

        }
        #endregion

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            #region CONFIG_BINDING
            mod_enabled = Config.Bind<bool>(
                "General",
                "Enable mod?",
                true,
                "Enables the mod. If disabled, you can only hear other people's voices. Your voice will not be recorded."
                );

            max_clip_count = Config.Bind<uint>(
                "General",
                "Max voice clips",
                10,
                new ConfigDescription(
                "The maximum number of voice recordings the mod will keep in memory at a time.",
                new AcceptableValueRange<uint>(1, 30))
                );

            talk_probability = Config.Bind<uint>(
                "General",
                "Talk Probability",
                10,
                new ConfigDescription(
                "How likely (as a percentage) a masked talking is.",
                new AcceptableValueRange<uint>(0, 100))
                );

            TTS_enabled = Config.Bind<bool>(
                "TTS",
                "Enabled",
                false,
                "Whether to use TTS service (Elevenlabs) for ai voice generation"
                );

            TTS_api_key = Config.Bind<string>(
                "TTS",
                "API key",
                "",
                "Your TTS (Elevenlabs) API key. Do NOT add extra characters like \""
                );

            TTS_voice_id = Config.Bind<string>(
                "TTS",
                "Voice id",
                "",
                "Your TTS (Elevenlabs) voice id"
                );

            TTS_voice_volume = Config.Bind<float>(
                "TTS",
                "Masked Voice Volume",
                1f,
                new ConfigDescription(
                "How loud the masked voices are",
                new AcceptableValueRange<float>(0f,1f)
                )
                );

            player_voice_description = Config.Bind<string>(
                "TTS",
                "Voice Description",
                "",
                "A description of your voice (eg 'A high-energy male Canadian voice'). Used to improve auto voice clone quality."
                );

            ChatServiceProvider = Config.Bind<string>(
                "Chat",
                "Chat Service Provider",
                "Gemini",
                new ConfigDescription(
                    "Which chat service provider to use. Allowed values are ChatGPT, Gemini, Claude, and Ollama",
                    new AcceptableValueList<string>("ChatGPT", "Gemini", "Claude", "Ollama")
                )
            );

            Chat_api_key = Config.Bind<string>(
                "Chat",
                "API key",
                "",
                "Your Chat API key. Do NOT add extra characters like \""
                );

            Chat_model = Config.Bind<string>(
                "Chat",
                "Model",
                "gemini-2.5-flash-lite",
                "Which chat model to use. Defaults to gemini-2.5-flash-lite."

                );

            Chat_prompt = Config.Bind<string>(
                "Chat",
                "Prompt",
                "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.",
                "The prompt given to the Chat service to determine what to say."
                );

            STT_service = Config.Bind<string>(
                "STT",
                "Service",
                "Azure",
                new ConfigDescription(
                    "Which STT service provider to use. Allowed values are Azure and Elevenlabs",
                    new AcceptableValueList<string>("Azure", "Elevenlabs")
                )
                );

            STT_api_key = Config.Bind<string>(
                "STT",
                "API key",
                "",
                "Your STT API key. Do NOT add extra characters like \""
                );

            STT_region = Config.Bind<string>(
                "STT",
                "Region",
                "canadacentral",
                "(Azure Only) Your Azure region"
                );

            STT_language = Config.Bind<string>(
                "STT",
                "Language",
                "en-US",
                "Your desired speech recognition language, Azure: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=stt or " +
                "Elevenlabs: https://en.wikipedia.org/wiki/List_of_ISO_639_language_codes"
                );

            enable_realtime_responses = Config.Bind<bool>(
                "Experimental",
                "Realtime Responses",
                false,
                "Enables Chat voice line generation so masked can reply in real time. You MUST have TTS, STT, and Chat api keys set. IMPORTANT: if you set this and " +
                "dont set a VoiceID, your voice will be automatically be cloned by Elevenlabs."
                );

            enable_config_sync = Config.Bind<bool>(
                "Experimental",
                "Config sync prompt",
                false,
                "(Host only) Enables showing a popup each time a player connects that allows you to sync your config with all clients."
                );

            player_name = Config.Bind<string>(
                "Experimental",
                "Your name",
                "",
                "Your name. Allows Chat service to know who is who. Needed for autoclone."
                );
            #endregion

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
            else
            {

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


                WendigosChatManager.Init(ChooseApiKey(Chat_api_key.Value, temp_Chat_api_key), Chat_model.Value, ChatServiceProvider.Value);

                STT.player_name = player_name.Value;
                try
                {
                    if (TTS_enabled.Value || enable_realtime_responses.Value)
                        ElevenLabs.Init(ChooseApiKey(TTS_api_key.Value, temp_TTS_api_key), TTS_voice_id.Value, TTS_voice_volume.Value);
                }
                catch (Exception ex)
                {
                    WriteToConsole(ex.ToString());
                }


                config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");
            }

        }

        #region MASKED_CLASSES
        public class MaskedEnemyIdentifier : MonoBehaviour
        {
            public string id;
            public GameObject child;
            public ConcurrentQueue<byte[]> audioNetworkQueue = new ConcurrentQueue<byte[]>();

            private void Update()
            {
                if (audioNetworkQueue.TryDequeue(out byte[] data))
                {
                    // Configuration
                    const int MAX_CHUNK_SIZE = 32768;
                    int clipPosition = 0;
                    int totalLength = data.Length;
                    var audioComponent = child.GetComponent<MaskedAudioComponent>();

                    // Loop until we have processed the entire clip
                    while (clipPosition < totalLength)
                    {
                        // 1. Calculate the size of the current chunk
                        // It will be MAX_CHUNK_SIZE, unless we are at the very end and have less than MAX_CHUNK_SIZE remaining.
                        int currentChunkSize = System.Math.Min(MAX_CHUNK_SIZE, totalLength - clipPosition);

                        // 2. Create the chunk buffer
                        byte[] chunk = new byte[currentChunkSize];

                        // 3. Copy data from the main clip into the chunk
                        // specific arguments: (source, sourceIndex, destination, destIndex, length)
                        System.Array.Copy(data, clipPosition, chunk, 0, currentChunkSize);

                        // --- YOUR CALLS BELOW ---

                        // Send Network RPC
                        WendigosNetworkManager.Instance.ShareAudioDataServerRpc(chunk.ToArray(), id);

                        // ------------------------

                        // Advance the position
                        clipPosition += currentChunkSize;
                    }
                }
            }
        }

        public class MaskedAudioComponent : MonoBehaviour
        {
            // CHANGE 1: Use ConcurrentQueue for thread safety
            // CHANGE 2: Queue individual floats, not arrays
            public StreamingAudioDecoder audioQueue = new StreamingAudioDecoder(48000, 2);

            private AudioSource _audioSource;
            private AudioClip _streamingClip;

            private int SampleRate = 48000;
            private int Channels = 2;

            public void Awake()
            {
                var voiceBoxAudioSource = GetComponent<AudioSource>();

                if (voiceBoxAudioSource != null)
                    transform.parent.GetComponent<MaskedPlayerEnemy>().creatureVoice.CopyTo(voiceBoxAudioSource);

                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                transform.parent.GetComponent<MaskedPlayerEnemy>().creatureVoice.CopyTo(_audioSource);



            }

            private void Start()
            {
                audioQueue.Reset();
                SampleRate = AudioSettings.outputSampleRate;
                Channels = (AudioSettings.speakerMode == AudioSpeakerMode.Mono) ? 1 : 2;

                _streamingClip = AudioClip.Create("NetworkStream", SampleRate, Channels, SampleRate, true, OnAudioRead);

                _audioSource.clip = _streamingClip;
                _audioSource.loop = true;

                if (!_audioSource.isPlaying)
                    _audioSource.Play();
            }

            private void OnAudioRead(float[] data)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (audioQueue.TryGetSample(out float sample))
                    {
                        data[i] = sample;
                    }
                    else
                    {
                        // Fill with silence to avoid noise/glitches.
                        data[i] = 0f;
                    }
                }
            }
        }
        #endregion

        #region PATCHES

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
                        var closest_player = __instance.CheckLineOfSightForClosestPlayer();
                        if (closest_player != null)
                        {
                            // Play clip when can see player
                            if (!enable_realtime_responses.Value && serverRand.Next(100) >= (100 - talk_probability.Value) && Vector3.Distance(closest_player.gameplayCamera.transform.position, __instance.eye.position) > 15)
                            {
                                WendigosNetworkManager.Instance.RequestMaskedResponseServerRpc(thisMaskedID, "", "", false);
                            }
                        }
                        // Nearby
                        else
                        {
                            if (serverRand.Next(100) >= (100 - talk_probability.Value))
                                PlayLocalAudioClipAndQueue(__instance);
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
                    if (!enable_realtime_responses.Value && serverRand.Next(3) == 0)
                    {
                        WendigosNetworkManager.Instance.RequestMaskedResponseServerRpc(thisMaskedID, "", "{DAMAGED}", false);
                    }
                }
                catch
                {

                }
            }
        }
        

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        class MaskedStartPatch
        {
            public static void Postfix(MaskedPlayerEnemy __instance)
            {
                InitMaskedAudio(__instance);

                if (WendigosNetworkManager.Instance.IsServer)
                {
                    // For testing
                    TeleportMaskedToLocalPlayer(__instance);

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

                // Tell clients to start STT (clients with realtime disabled ignore this!)
                if (NetworkManager.Singleton.IsServer)
                {
                    WendigosNetworkManager.Instance.InitSTTClientRpc();
                }
                    
                if (WendigosChatManager.chatManagerComponent == null)
                    WendigosChatManager.Init(ChooseApiKey(Chat_api_key.Value, temp_Chat_api_key), Chat_model.Value, ChatServiceProvider.Value);
                try
                {
                    if ((TTS_enabled.Value || enable_realtime_responses.Value) && ElevenLabs.ttsManagerComponent == null)
                        ElevenLabs.Init(ChooseApiKey(TTS_api_key.Value, temp_TTS_api_key), TTS_voice_id.Value, TTS_voice_volume.Value);
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

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
        class RoundManagerEndPatch
        {
            static void Prefix()
            {
                try
                {
                    // reset speech recognition and chat history
                    STT.StopSpeechTranscription();
                    WendigosChatManager.chats.Clear();
                    
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
                STT.ChangeMicDevice(mic_name);
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
                STT.ChangeMicDevice(mic_name);
                WriteToConsole("Set to " + mic_name);
            }
        }

        [HarmonyPatch(typeof(MenuManager), "Start")]
        class MenuManagerPatch
        {
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene)
                {
                    return;
                }

                _ = TimShaw.VoiceBox.GUI.GUIManager.CreateGUIManagerObject();

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
#endregion
    }
}
