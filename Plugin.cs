using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using LC_API;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UIElements;
using UnityEngine.XR;
using static System.Net.Mime.MediaTypeNames;
using Unity.Netcode;
using LC_API.GameInterfaceAPI.Features;
using LC_API.GameInterfaceAPI.Events.Handlers;
using LC_API.ServerAPI;
using System.Runtime.Serialization.Json;
using System.Xml;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using System.IO.Compression;
using LethalNetworkAPI;
using System.Xml.Linq;
using System.Security.Claims;
using System.Buffers;
using static Wendigos.Plugin;
using System.Text;
using Steamworks.ServerList;

// StartOfRound requires adding the game's Assembly-CSharp to dependencies

namespace Wendigos
{
    public class ComparableList<T> : List<T>
    {
        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(ComparableList<T>))
                return false;
            return SequenceEqual((ComparableList<T>)obj);
        }

        public bool SequenceEqual(ComparableList<T> obj)
        {
            if (this.Count != obj.Count)
                return false;
            for (int i = 0; i < this.Count; i++)
            {
                if (!this[i].Equals(obj[i]))
                    return false;
            }
            return true;
        }
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public class WendigosMessageHandler : NetworkBehaviour
        {
            [Tooltip("The name identifier used for this custom message handler.")]
            public static string MessageName = "clipSender";
            private static Dictionary<ulong, List<byte[]>> clipFragmentBuffers = new Dictionary<ulong, List<byte[]>>();
            private static int numberOfFragments = 10;
            public static int maxNumPlayers = 64;
            public static int maxMasked = 10;

            [PublicNetworkVariable]
            public static LethalNetworkVariable<int> randomInt;

            [PublicNetworkVariable]
            public static LethalNetworkVariable<int> randomInt2;

            // { masked_identifier : ready_players }
            //[PublicNetworkVariable]
            //public static LethalNetworkVariable<Dictionary<string, bool[]>> per_masked_ready_dict; // TODO: Multiple masked?

            [PublicNetworkVariable]
            public static LethalNetworkVariable<ComparableList<string>> per_masked_ready_keys;

            // could also be List<ulong> or List<string> with only 64 players and bit magic
            // both could cause desync if written to while another is editing
            [PublicNetworkVariable]
            public static LethalNetworkVariable<ComparableList<bool[]>> per_masked_ready_values;

            // { masked_identifier : player_to_mimic }
            //[PublicNetworkVariable]
            //public static LethalNetworkVariable<Dictionary<string, ulong>> masked_client_dict;

            [PublicNetworkVariable]
            public static LethalNetworkVariable<ComparableList<string>> masked_client_keys;

            [PublicNetworkVariable]
            public static LethalNetworkVariable<ComparableList<ulong>> masked_client_values;

            [PublicNetworkVariable]
            public static LethalNetworkVariable<ComparableList<ulong>> ConnectedClientIDs;


            public static WendigosMessageHandler Instance { get; private set; }


            /// <summary>
            /// For most cases, you want to register once your NetworkBehaviour's
            /// NetworkObject (typically in-scene placed) is spawned.
            /// </summary>
            public override void OnNetworkSpawn()
            {
                base.OnNetworkSpawn();
                //WriteToConsole(NetworkManager.Singleton.LocalClientId.ToString());
                // Both the server-host and client(s) register the custom named message.
                NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, ReceiveMessage);

                if (IsServer)
                {
                    // Server broadcasts to all clients when a new client connects (just for example purposes)
                    NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;

                    // Server inits
                    randomInt = new LethalNetworkVariable<int>("randomInt") { Value = serverRand.Next() };
                    randomInt2 = new LethalNetworkVariable<int>("randomInt") { Value = serverRand.Next() };

                    ConnectedClientIDs = new LethalNetworkVariable<ComparableList<ulong>>("ConnectedClientIDs") { Value = new ComparableList<ulong>() };
                    foreach (var clientID in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        ConnectedClientIDs.Value.Add(clientID);
                    }

                    //per_masked_ready_dict = new LethalNetworkVariable<Dictionary<string, bool[]>>("perMaskedReadyDict");
                    //per_masked_ready_dict.Value = new Dictionary<string, bool[]>();

                    per_masked_ready_keys = new LethalNetworkVariable<ComparableList<string>>("perMaskedReadyKeys") { Value = new ComparableList<string>() };
                    per_masked_ready_values = new LethalNetworkVariable<ComparableList<bool[]>>("perMaskedReadyValues") { Value = new ComparableList<bool[]>()};

                    //masked_client_dict = new LethalNetworkVariable<Dictionary<string, ulong>>("maskedClientDict");
                    //masked_client_dict.Value = new Dictionary<string, ulong>();

                    masked_client_keys = new LethalNetworkVariable<ComparableList<string>>("maskedClientKeys") { Value = new ComparableList<string>() };
                    masked_client_values = new LethalNetworkVariable<ComparableList<ulong>>("maskedClientValues") { Value = new ComparableList<ulong>() };

                    //WriteToConsole(masked_client_dict.Value.ToString());
                    WriteToConsole("Random seed is " + randomInt.Value);
                }
                else
                {
                    // Clients send a unique Guid to the server
                    //SendMessage(Guid.NewGuid());

                    //indexToPlay.OnValueChanged += UpdateIndexValue;
                    //randomValue.OnValueChanged += UpdateRandomValue;

                    // Client inits
                    randomInt = new LethalNetworkVariable<int>("randomInt");
                    randomInt2 = new LethalNetworkVariable<int>("randomInt");
                    ConnectedClientIDs = new LethalNetworkVariable<ComparableList<ulong>>("ConnectedClientIDs");
                    //per_masked_ready_dict = new LethalNetworkVariable<Dictionary<string, bool[]>>("perMaskedReadyDict");
                    per_masked_ready_keys = new LethalNetworkVariable<ComparableList<string>>("perMaskedReadyKeys");
                    per_masked_ready_values = new LethalNetworkVariable<ComparableList<bool[]>>("perMaskedReadyValues");
                    //masked_client_dict = new LethalNetworkVariable<Dictionary<string, ulong>>("maskedClientDict");
                    masked_client_keys = new LethalNetworkVariable<ComparableList<string>>("maskedClientKeys");
                    masked_client_values = new LethalNetworkVariable<ComparableList<ulong>>("maskedClientValues");

                    WriteToConsole("Created Client rand");
                }
            }

            internal static void ClientConnectInitializer(Scene sceneName, LoadSceneMode sceneEnum)
            {
                //IL_001c: Unknown result type (might be due to invalid IL or missing references)
                //IL_0022: Expected O, but got Unknown
                if (((Scene)(sceneName)).name == "SampleSceneRelay")
                {
                    GameObject val = new GameObject("WendigosMessageHandler");
                    val.AddComponent<NetworkObject>();
                    val.AddComponent<WendigosMessageHandler>();
                    WriteToConsole("Made network manager");
                }
            }

            private void Awake()
            {
                Instance = this;
            }

            private void OnClientConnectedCallback(ulong obj)
            {
                //SendMessage(Guid.NewGuid());
                WriteToConsole("Server sending " + get_clips_count() + " clips");
                List<AudioClip> clipsCopy = new List<AudioClip>(audioClips[NetworkManager.Singleton.LocalClientId]);
                foreach (AudioClip clip in clipsCopy)
                {
                    SendFragmentedMessage(ConvertToByteArr(clip));
                }

                if (IsServer)
                {
                    foreach (var clientID in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        if (!ConnectedClientIDs.Value.Contains(clientID))
                            ConnectedClientIDs.Value.Add(clientID);
                    }
                }

            }

            public override void OnNetworkDespawn()
            {
                foreach (var clipList in audioClips.Values)
                    clipList.Clear(); 
                sent_audio_clips = false;
                ConnectedClientIDs.Value.Clear();
                //masked_client_dict.Value.Clear();
                masked_client_keys.Value.Clear();
                masked_client_values.Value.Clear();

                //per_masked_ready_dict.Value.Clear();
                per_masked_ready_keys.Value.Clear();
                per_masked_ready_values.Value.Clear();
                // De-register when the associated NetworkObject is despawned.
                //NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                // Whether server or not, unregister this.
                //NetworkManager.OnClientDisconnectCallback -= OnClientConnectedCallback;

            }

            /// <summary>
            /// Invoked when a custom message of type <see cref="MessageName"/>
            /// </summary>
            private void ReceiveMessage(ulong senderId, FastBufferReader messagePayload)
            {
                byte[] receivedMessageContent;
                messagePayload.ReadValueSafe(out receivedMessageContent);

                if (!clipFragmentBuffers.ContainsKey(senderId))
                {
                    clipFragmentBuffers.Add(senderId, new List<byte[]>());
                }
                clipFragmentBuffers[senderId].Add(receivedMessageContent);
                CombineAudioFragments(senderId);
                

            }

            private void CombineAudioFragments(ulong senderId)
            {
                // Haven't recieved all fragments
                if (clipFragmentBuffers[senderId].Count != numberOfFragments)
                    return;

                // Get size of original audioclip
                int sizeOfFullMessage = 0;
                foreach (var fragment in clipFragmentBuffers[senderId])
                {
                    sizeOfFullMessage += fragment.Length;
                }
                byte[] receivedMessageContent = new byte[sizeOfFullMessage];

                // Block copy fragments into one big byte array
                int totalOffset = 0;
                foreach (var fragment in clipFragmentBuffers[senderId])
                {
                    Buffer.BlockCopy(fragment, 0, receivedMessageContent, totalOffset, fragment.Length);
                    totalOffset += fragment.Length;
                }

                // clear buffer for next clip
                clipFragmentBuffers[senderId].Clear();

                // decompress audioclip
                receivedMessageContent = Decompress(receivedMessageContent);

                AudioClip recievedClip = LoadAudioClip(receivedMessageContent);
                bool doWeHaveTheClip = false;

                if (!audioClips.Keys.Contains(senderId))
                    audioClips.Add(senderId, new List<AudioClip>());

                if (IsServer)
                {
                    WriteToConsole($"Sever received ({receivedMessageContent}) from client ({senderId})");
                    foreach (AudioClip clip in audioClips[senderId])
                    {
                        if (clip.name == recievedClip.name)
                        {
                            WriteToConsole(clip.name);
                            WriteToConsole("We already have this clip!");
                            doWeHaveTheClip = true;
                            WriteToConsole("AudioClip count is now: " + get_clips_count());
                        }
                    }
                    if (!doWeHaveTheClip)
                    {
                        audioClips[senderId].Add(recievedClip);
                        WriteToConsole("Added Clip.");
                        WriteToConsole("AudioClip count is now: " + get_clips_count());
                    }
                }
                else
                {
                    WriteToConsole($"Client received ({receivedMessageContent}) from the server.");
                    foreach (AudioClip clip in audioClips[senderId])
                    {
                        if (clip.name == recievedClip.name)
                        {
                            WriteToConsole("We already have this clip!");
                            doWeHaveTheClip = true;
                            WriteToConsole("AudioClip count is now: " + get_clips_count());
                        }
                    }
                    if (!doWeHaveTheClip)
                    {
                        audioClips[senderId].Add(recievedClip);
                        WriteToConsole("Added Clip.");
                        WriteToConsole("AudioClip count is now: " + get_clips_count());
                    }
                }

                // Cant be async with latecompany
                sort_audioclips();
            }

            /// <summary>
            /// Invoke this with a Guid by a client or server-host to send a
            /// custom named message.
            /// </summary>
            private void SendMessage(byte[] audioClipFragment)
            {
                var messageContent = audioClipFragment;
                //WriteToConsole("Writing message...");

                // Steam has max size of 512kb (C)
                var writer = new FastBufferWriter(512000, Unity.Collections.Allocator.Temp);
                //WriteToConsole("Wrote Message");
                var customMessagingManager = NetworkManager.Singleton.CustomMessagingManager;

                using (writer)
                {
                    //WriteToConsole($"Writing {messageContent.Length} bytes of data...");
                    // Issue is here
                    writer.WriteValueSafe(messageContent);
                    //WriteToConsole("Wrote data");
                    if (NetworkManager.Singleton.IsServer)
                    {
                        // This is a server-only method that will broadcast the named message.
                        // Caution: Invoking this method on a client will throw an exception!
                        //WriteToConsole("Sending Message...");
                        customMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.ReliableFragmentedSequenced);
                        //WriteToConsole("Sent Message");
                    }
                    else
                    {
                        // This is a client or server method that sends a named message to one target destination
                        // (client to server or server to client)
                        //WriteToConsole("Sending Message...");
                        customMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
                        //WriteToConsole("Sent Message");
                    }
                }
            }

            public void SendFragmentedMessage(byte[] audioClip)
            {
                var message = Compress(audioClip);
                if (message.Length > Math.Ceiling(5120000 / (float)numberOfFragments))
                {
                    throw new Exception("clip is too large to send! Try increasing the number of message fragments.");
                }

                int offset = (int)Math.Ceiling(message.Length / (float)numberOfFragments);
                WriteToConsole("Offset size is " + offset);
                List<byte[]> fragments = new List<byte[]>();
                for (int i = 0; i < numberOfFragments; i++)
                {
                    if (i != numberOfFragments - 1)
                    {
                        fragments.Add(new byte[offset]);
                        Buffer.BlockCopy(message, offset * i, fragments[i], 0, offset);
                    }
                    else
                    {
                        fragments.Add(new byte[message.Length - (numberOfFragments - 1) * offset]);
                        Buffer.BlockCopy(message, offset * i, fragments[i], 0, message.Length - (numberOfFragments - 1) * offset);
                    }
                }
                foreach (var fragment in fragments)
                {
                    WriteToConsole("Sending fragment of length " + fragment.Length);
                    SendMessage(fragment);
                }
            }
        }

        static void sort_audioclips()
        {
            foreach (var clipList in audioClips.Values)
            {
                clipList.Sort((c1, c2) => c1.name.CompareTo(c2.name));
            }
        }


        static void WriteToConsole(string output)
        {
            Console.WriteLine("Wendigos: " + output);
        }

        private void Open_YT_URL()
        {
            UnityEngine.Application.OpenURL("https://www.youtube.com/@Tim-Shaw");
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


            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "cmd.exe";
            startInfo.WorkingDirectory = assembly_path;
            //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = $"/C (set PYTORCH_JIT=0)&(main.exe {file_name})";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;

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
                    WriteToConsole("LOADING MODEL...");
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

        static void GenerateAllPlayerSentences()
        {
            bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
            bool new_idle, new_nearby, new_chasing;
            new_idle = new_nearby = new_chasing = false;

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
                WriteToConsole($"generating idle sentences");
            }
            if (new_idle) 
                GeneratePlayerSentences("idle", config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt");


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
                WriteToConsole($"generating nearby sentences");
            }
            if (new_nearby)
                GeneratePlayerSentences("nearby", config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt");


            if (!File.Exists(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt"))
            {
                File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt",
                "wait come back\n" +
                "where are you going?\n" +
                "AAAAAAAAAAAAAAAAAAA"
                );
            }
            if (found_sample_audio && isFileChanged(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt"))
            {
                new_chasing = true;
                WriteToConsole($"generating chasing sentences");
            }
            if (new_chasing)
                GeneratePlayerSentences("chasing", config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt");

            WriteToConsole("Finished generating voice lines.");
        }

        private static bool isFileChanged(string path)
        {
            DateTime timestamp = File.GetLastWriteTime(path);

            return timestamp > main_last_accessed;
        }

        private static ConfigEntry<bool> need_new_player_audio;
        static System.Random serverRand = new System.Random();
        static int maskedIdCounter = 0;

        public static List<PlayerControllerB> deadPlayers = new List<PlayerControllerB>();
        Harmony harmonyInstance = new Harmony("my-instance");

        private static string config_path;
        private static string assembly_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // used to track if we need to generate new audio files
        private static DateTime main_last_accessed = File.GetLastAccessTime(assembly_path + "\\main.exe");

        internal static string mic_name;

        internal static ulong steamID;

        static AudioClip ac;

        static List<AudioClip> myClips = new List<AudioClip>();
        public static Dictionary<ulong, List<AudioClip>> audioClips = new Dictionary<ulong, List<AudioClip>>() { { 0, new List<AudioClip>() } };

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            harmonyInstance.PatchAll();
            SceneManager.sceneLoaded += WendigosMessageHandler.ClientConnectInitializer;

            need_new_player_audio = Config.Bind<bool>(
                "General",
                "Record new player sample audio?",
                true,
                "Whether the record audio prompt should show up"
                );

            config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");

            System.IO.Directory.CreateDirectory(config_path + "Wendigos\\player_sentences");
            System.IO.Directory.CreateDirectory(assembly_path + "\\player_sentences");
            System.IO.Directory.CreateDirectory(assembly_path + "\\sample_player_audio");
            System.IO.Directory.CreateDirectory(assembly_path + "\\audio_output");
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: Created/found config directories");

            bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: {(found_sample_audio ? "found" : "didn't find")} player sample audio");

            // start generating voice lines async
            Task.Factory.StartNew(GenerateAllPlayerSentences);

            //maskedEnemies = UnityEngine.Object.FindObjectsOfType<MaskedPlayerEnemy>(false).ToList();

            WriteToConsole("AudioClip dict is: " + audioClips.ToString());

        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerDC))]
        class PlayerDCPatch
        {
            static void Prefix(int playerObjectNumber, ulong clientId)
            {
                WriteToConsole($"Clearing {clientId}'s audio clips");
                audioClips[clientId].Clear();
                WriteToConsole($"Removed {audioClips[clientId].Count} Clips");
                WriteToConsole("AudioClip count is now " + get_clips_count());

                if (WendigosMessageHandler.Instance.IsServer)
                {
                    WendigosMessageHandler.ConnectedClientIDs.Value.Remove(clientId);
                    var client_index = WendigosMessageHandler.masked_client_values.Value.IndexOf(clientId);

                    // remove client
                    WendigosMessageHandler.masked_client_values.Value.Remove(clientId);

                    // remove masked associated with client
                    WendigosMessageHandler.masked_client_keys.Value.RemoveAt(client_index);


                    // When player leaves, they are always ready
                    for (var masked_index = 0; masked_index < WendigosMessageHandler.per_masked_ready_keys.Value.Count; masked_index++)
                    {
                        // Set disconnected client to ready for all masked
                        WendigosMessageHandler.per_masked_ready_values.Value[masked_index][clientId] = true;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Update))]
        class MaskedPlayerEnemyUpdatePatch
        {
            static void Prefix()
            {
                StartOfRound startOfRound = StartOfRound.Instance;

                //var currentLevel = RoundManager.Instance.currentLevel;

                //RoundManager.Instance.currentLevel.Enemies.Add(new SpawnableEnemyWithRarity());
                

                var players = startOfRound.allPlayerScripts;

                //AudioSource voice = MaskedPlayerEnemy.voice;

                // Get all dead players
                foreach (var player in players)
                {
                    if (!deadPlayers.Contains(player) && player.isPlayerDead)
                    {
                        deadPlayers.Add(player);
                        WriteToConsole("player died -- ID: " + player.playerSteamId + " -- name: " + player.name);
                    }
                }

            }
        }

        [HarmonyPatch(typeof(StartOfRound), "OnLocalDisconnect")]
        class DisconnectPatch
        {
            static void Postfix() 
            {
                foreach (var clipList in audioClips.Values)
                    clipList.Clear();
                sent_audio_clips = false;
            }
        }

        public static string GetHashSHA1(byte[] data)
        {
            using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
            {
                return string.Concat(sha1.ComputeHash(data).Select(x => x.ToString("X2")));
            }
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
                        // Slow hash
                        myClip.name = GetHashSHA1(ConvertToByteArr(myClip));
                        return myClip;
                    }
                }
            }
            WriteToConsole("AUDIO FILE NOT FOUND");
            return null;
        }

        static void TryToPlayAudio(AudioClip clip, MaskedPlayerEnemy __instance)
        {
            try
            {
                if (clip && !__instance.creatureVoice.isPlaying)
                    __instance.creatureVoice.PlayOneShot(clip);
            }
            catch (Exception e)
            {
                WriteToConsole("Playing audio failed: " + e.Message + ": " + e.Source);
            }
        }

        static string GetPathOfWav(string type, int lineNum = -1)
        {
            if (lineNum < 0)
            {
                System.Random random = new System.Random();
                lineNum = random.Next(CountFilesInDir(GetPathOfType(type)));
            }
            return assembly_path + "\\audio_output" + $"\\player0\\{type}\\{type}0_line" + lineNum + ".wav";
        }

        static string GetPathOfType(string type)
        {
            return assembly_path + "\\audio_output" + $"\\player0\\{type}";
        }

        static int CountFilesInDir(string path)
        {
            return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Length;
        }

        static bool are_all_ready(string MaskedID)
        {
            bool ready = true;
            var masked_ready_index = WendigosMessageHandler.per_masked_ready_keys.Value.IndexOf(MaskedID);
            foreach(bool readyVal in WendigosMessageHandler.per_masked_ready_values.Value[masked_ready_index])
            {
                ready = ready && readyVal;
            }
            return ready;
        }

        static void prep_server(string MaskedID)
        {
            if (are_all_ready(MaskedID))
            {
                WendigosMessageHandler.randomInt.Value = serverRand.Next();
                WendigosMessageHandler.randomInt2.Value = serverRand.Next();
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

                // dict synced on client
                WriteToConsole("masked_client_keys count: " + WendigosMessageHandler.masked_client_keys.Value.Count.ToString());
                WriteToConsole("masked_client_values count: " + WendigosMessageHandler.masked_client_values.Value.Count.ToString());
                WriteToConsole("per_masked_ready_keys count: " + WendigosMessageHandler.per_masked_ready_keys.Value.Count.ToString());
                WriteToConsole("per_masked_ready_values count: " + WendigosMessageHandler.per_masked_ready_values.Value.Count.ToString());

                string[] types = ["idle", "nearby", "chasing"];
                string type = types[serverRand.Next(types.Length)];

                string thisMaskedID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                if (!WendigosMessageHandler.masked_client_keys.Value.Contains(thisMaskedID))
                    return;

                var masked_client_index = WendigosMessageHandler.masked_client_keys.Value.IndexOf(thisMaskedID);

                ulong MimickingClientID = WendigosMessageHandler.masked_client_values.Value[masked_client_index];

                var masked_ready_index = WendigosMessageHandler.per_masked_ready_keys.Value.IndexOf(thisMaskedID);

                if (!__instance.creatureVoice.isPlaying)
                    WendigosMessageHandler.per_masked_ready_values.Value[masked_ready_index][NetworkManager.Singleton.LocalClientId] = true;
                else
                    WendigosMessageHandler.per_masked_ready_values.Value[masked_ready_index][NetworkManager.Singleton.LocalClientId] = false;

                /* worked with this, should work without
                int count = 0;
                foreach (bool ready in WendigosMessageHandler.ready_players.Value)
                {
                    if (!ready)
                        count++;
                }
                WriteToConsole("READY COUNT: " +  count); 
                */

                switch (__instance.currentBehaviourStateIndex)
                {
                    case 0:
                        if (__instance.CheckLineOfSightForClosestPlayer() != null)
                        {
                            if (WendigosMessageHandler.Instance.IsServer)
                                prep_server(thisMaskedID);

                            if (WendigosMessageHandler.randomInt.Value % 10 == 0 
                                && !__instance.creatureVoice.isPlaying 
                                && are_all_ready(thisMaskedID))
                            {
                                int indexToPlay = WendigosMessageHandler.randomInt2.Value % audioClips[MimickingClientID].Count;
                                WriteToConsole("Playing Index " + indexToPlay);
                                TryToPlayAudio(audioClips[MimickingClientID][indexToPlay], __instance);
                            }
                        }
                        else
                        {
                            if (WendigosMessageHandler.Instance.IsServer)
                                prep_server(thisMaskedID);
                            if (WendigosMessageHandler.randomInt.Value % 10 == 0
                                && !__instance.creatureVoice.isPlaying
                                && are_all_ready(thisMaskedID))
                            {
                                int indexToPlay = WendigosMessageHandler.randomInt2.Value % audioClips[MimickingClientID].Count;
                                WriteToConsole("Playing Index " + indexToPlay);
                                TryToPlayAudio(audioClips[MimickingClientID][indexToPlay], __instance);
                            }
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
            static void Postfix(MaskedPlayerEnemy __instance)
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
            static void Prefix(ref bool setOut, MaskedPlayerEnemy __instance)
            {
                setOut = false;
                string type = "chasing";

                string thisMaskedID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                if (!WendigosMessageHandler.masked_client_keys.Value.Contains(thisMaskedID))
                    return;

                var masked_client_index = WendigosMessageHandler.masked_client_keys.Value.IndexOf(thisMaskedID);

                ulong MimickingClientID = WendigosMessageHandler.masked_client_values.Value[masked_client_index];

                var masked_ready_index = WendigosMessageHandler.per_masked_ready_keys.Value.IndexOf(thisMaskedID);

                if (!__instance.creatureVoice.isPlaying)
                    WendigosMessageHandler.per_masked_ready_values.Value[masked_ready_index][NetworkManager.Singleton.LocalClientId] = true;
                else
                    WendigosMessageHandler.per_masked_ready_values.Value[masked_ready_index][NetworkManager.Singleton.LocalClientId] = false;

                //TODO: null reference?
                if (WendigosMessageHandler.Instance.IsServer)
                    prep_server(thisMaskedID);

                if (WendigosMessageHandler.randomInt.Value % 10 == 0
                    && !__instance.creatureVoice.isPlaying
                    && are_all_ready(thisMaskedID))
                {
                    int indexToPlay = WendigosMessageHandler.randomInt2.Value % audioClips[MimickingClientID].Count;
                    WriteToConsole("Playing Index " + indexToPlay);
                    TryToPlayAudio(audioClips[MimickingClientID][indexToPlay], __instance);
                }

            }

        }

        public class MaskedEnemyIdentifier : MonoBehaviour
        {
            public string id;
        }

        // Also finishKillAnimation? -- Start should run regardless tho
        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        class MaskedStartPatch
        {
            static void Postfix(MaskedPlayerEnemy __instance)
            {

                __instance.gameObject.AddComponent<MaskedEnemyIdentifier>();

                // id is starting position since only 1 enemy can spawn per vent
                __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id = __instance.transform.position.ToString();
                WriteToConsole("Spawned Masked. ID: " + __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id);

                if (WendigosMessageHandler.Instance.IsServer)
                {
                    List<ulong> unassignedClientIDs = new List<ulong>();
                    WriteToConsole(WendigosMessageHandler.ConnectedClientIDs.Value.ToString());
                    WriteToConsole("Keys " + WendigosMessageHandler.masked_client_keys.Value.ToString());
                    WriteToConsole("Values " + WendigosMessageHandler.masked_client_values.Value.ToString());

                    foreach (var clientID in WendigosMessageHandler.ConnectedClientIDs.Value)
                    {
                        if (!WendigosMessageHandler.masked_client_values.Value.Contains(clientID))
                            unassignedClientIDs.Add(clientID);
                    }
                    WriteToConsole("Created unasssigned list");

                    // All clients have been assigned a masked
                    if (unassignedClientIDs.Count == 0)
                        return;

                    ulong randomClientID = unassignedClientIDs[serverRand.Next() % unassignedClientIDs.Count];

                    WendigosMessageHandler.masked_client_keys.Value.Add(__instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id);
                    WendigosMessageHandler.masked_client_values.Value.Add(randomClientID);

                    WriteToConsole("added masked to masked_client_dict");

                    WendigosMessageHandler.per_masked_ready_keys.Value.Add(__instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id);
                    WendigosMessageHandler.per_masked_ready_values.Value.Add(new bool[WendigosMessageHandler.maxNumPlayers]);

                    WriteToConsole("added masked to per_masked_ready_dict");

                    var masked_ready_index = WendigosMessageHandler.per_masked_ready_keys.Value.IndexOf(__instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id);
                    for (int i = 0; i < WendigosMessageHandler.maxNumPlayers; i++)
                    {
                        WendigosMessageHandler.per_masked_ready_values.Value[masked_ready_index][i] = true;
                    }
                    WriteToConsole("Set all values to true");
                }

                WriteToConsole("Finished Spawning Masked");

                // dict synced on client
                WriteToConsole("masked_client_keys count: " + WendigosMessageHandler.masked_client_keys.Value.Count.ToString());
                WriteToConsole("masked_client_values count: " + WendigosMessageHandler.masked_client_values.Value.Count.ToString());
                WriteToConsole("per_masked_ready_keys count: " + WendigosMessageHandler.per_masked_ready_keys.Value.Count.ToString());
                WriteToConsole("per_masked_ready_values count: " + WendigosMessageHandler.per_masked_ready_values.Value.Count.ToString());
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

            // Slow hash
            AudioClip clip = AudioClip.Create(GetHashSHA1(receivedBytes), samples.Length, channels, sampleRate, false);
            clip.SetData(samples, 0);

            return clip;
        }

        public static void GeneratePlayerAudioClips()
        {
            // Generate audio clips
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\idle"))
            {
                AudioClip clip = LoadWavFile(line);
                myClips.Add(clip);
            }
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\nearby"))
            {
                AudioClip clip = LoadWavFile(line);
                myClips.Add(clip);
            }
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\chasing"))
            {
                AudioClip clip = LoadWavFile(line);
                myClips.Add(clip);
            }
            WriteToConsole("Generated Player Clips. Count: " + myClips.Count);
        }

        [HarmonyPatch(typeof(MenuManager), "Start")]
        class MenuManagerPatch
        {
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene)
                {
                    Task.Factory.StartNew(GeneratePlayerAudioClips);
                    return;
                }
                try
                {
                    steamID = Steamworks.SteamClient.SteamId.Value;                
                }
                catch {
                    steamID = 1;
                }

                // Show record audio prompt
                __instance.NewsPanel.SetActive(false);
                if (!File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav") || need_new_player_audio.Value)
                {
                    __instance.DisplayMenuNotification($"Press R to record some voice lines.\nSelected Mic is {mic_name}", "[ Done ]");
                    Transform responseButton = __instance.menuNotification.transform.Find("Panel").Find("ResponseButton");
                    responseButton.transform.position = new Vector3(responseButton.transform.position.x, responseButton.transform.position.y - 10, responseButton.transform.position.z);
                }
            }
        }

        [HarmonyPatch(typeof(MenuManager), "Update")]
        class MenuManagerUpdatePatch
        {
            static int index = 0;
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene) { return; }
                if (!__instance.menuNotification.activeInHierarchy) { return; }

                if (!Microphone.IsRecording(mic_name))
                {
                    if (UnityInput.Current.GetKeyUp("R"))
                    {
                        // Get max frequency of mic device
                        int minfreq;
                        int maxfreq;
                        Microphone.GetDeviceCaps(mic_name, out minfreq, out maxfreq);

                        ac = Microphone.Start(mic_name, false, 100, maxfreq);
                        __instance.menuNotificationButtonText.text = "Recording...";
                        __instance.menuNotificationText.text = "Press S to finish recording\nPress N for next line\n- - - - -\n" + lines_to_read[index];
                    }
                }
                else
                {
                    if (UnityInput.Current.GetKeyUp("S"))
                    {
                        Microphone.End(mic_name);
                        __instance.menuNotificationButtonText.text = "[ done ]";
                        __instance.menuNotificationText.text = "Recording stopped";
                        ac = SavWav.TrimSilence(ac, 0.01f);
                        need_new_player_audio.Value = false;
                        SavWav.Save(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav", ac);
                        Task.Factory.StartNew(GenerateAllPlayerSentences);


                    }
                    else if (UnityInput.Current.GetKeyUp("N"))
                    {
                        if (index + 1 < lines_to_read.Length)
                        {
                            index++;
                            __instance.menuNotificationText.text = "Press S to finish recording\nPress N for next line\n- - - - -\n" + lines_to_read[index];
                        }
                        else 
                        {
                            Microphone.End(mic_name);
                            __instance.menuNotificationButtonText.text = "[ done ]";
                            __instance.menuNotificationText.text = "Recording stopped";
                            need_new_player_audio.Value = false;
                            ac = SavWav.TrimSilence(ac, 0.01f);
                            SavWav.Save(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav", ac);
                            Task.Factory.StartNew(GenerateAllPlayerSentences);

                        }
                    }
                }
            }
        }

        public static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }
            return output.ToArray();
        }
        static bool sent_audio_clips = false;

        public static int get_clips_count()
        {
            int clips_count = 0;
            foreach (var audioList in audioClips.Values)
            {
                clips_count += audioList.Count;
            }
            return clips_count;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        class PlayerConnectPatch
        {
            static void Postfix()
            {
                if (!sent_audio_clips)
                {
                    if (!audioClips.Keys.Contains(NetworkManager.Singleton.LocalClientId))
                        audioClips.Add(NetworkManager.Singleton.LocalClientId, new List<AudioClip>());

                    foreach (AudioClip clip in myClips)
                    {
                        audioClips[NetworkManager.Singleton.LocalClientId].Add(clip);
                        byte[] audioData = ConvertToByteArr(clip);
                        WendigosMessageHandler.Instance.SendFragmentedMessage(audioData);
                    }

                    var clips_count = get_clips_count();
                    WriteToConsole("Sent " + clips_count + " Clips");


                    //WriteToConsole("Clips count: " + SoundTool.networkedClips.Count);
                    sent_audio_clips = true;

                    WriteToConsole(WendigosMessageHandler.ConnectedClientIDs.Value.Count.ToString());

                    WriteToConsole("Test connected list: " + WendigosMessageHandler.per_masked_ready_values.Value.Count.ToString());
                    WriteToConsole("Test connected list 2: " + WendigosMessageHandler.per_masked_ready_values.Value.Count.ToString());
                }
                
            }
        }

    }
}
