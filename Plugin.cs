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
using Steamworks;
using Unity.Collections;
using Newtonsoft.Json;

// StartOfRound requires adding the game's Assembly-CSharp to dependencies

namespace Wendigos
{

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public class WendigosMessageHandler : NetworkBehaviour
        {
            public static string MessageName = "clipSender";
            private static Dictionary<ulong, List<byte[]>> clipFragmentBuffers = new Dictionary<ulong, List<byte[]>>();
            private static int numberOfFragments = 1;
            public static bool isEveryoneReady = false;

            public static List<ulong> ConnectedClientIDs;
            public static WendigosMessageHandler Instance { get; private set; }

            public NetworkList<FixedString128Bytes> clipNamesArr;

            /// <summary>
            /// For most cases, you want to register once your NetworkBehaviour's
            /// NetworkObject (typically in-scene placed) is spawned.
            /// </summary>
            public override void OnNetworkSpawn()
            {
                base.OnNetworkSpawn();
                // Both the server-host and client(s) register the custom named message.
                NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, ReceiveMessage);

                
                ConnectedClientIDs = new List<ulong>() { 0 };

                if (IsServer)
                {
                    // Server broadcasts to all clients when a new client connects 
                    NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;

                    foreach (AudioClip clip in myClips)
                    {
                        clipNamesArr.Add((FixedString128Bytes)("0" + clip.name));
                    }
                }


                if (!audioClips.Keys.Contains(NetworkManager.Singleton.LocalClientId))
                    audioClips.Add(NetworkManager.Singleton.LocalClientId, new List<AudioClip>());

                foreach (AudioClip clip in myClips)
                {
                    audioClips[NetworkManager.Singleton.LocalClientId].Add(clip);
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
                    //DontDestroyOnLoad(val);
                }
            }

            private void Awake()
            {
                Instance = this;
                clipNamesArr = new NetworkList<FixedString128Bytes>();
            }

            private void OnClientConnectedCallback(ulong obj)
            {
                if (IsServer)
                {
                    //SendMessage(Guid.NewGuid());
                    WriteToConsole("Server sending " + get_clips_count() + " clips");


                    foreach (ulong connectedClient in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        if (connectedClient == obj) continue;

                        // Send ALL clips the server has to new client
                        try
                        {
                            List<AudioClip> clipsCopy = new List<AudioClip>(audioClips[connectedClient]);

                            var task = SendClipListAsync(clipsCopy, obj, true, originClient: connectedClient);
                            //task.Wait();
                        }
                        catch { continue; }
                    }
                }
                else
                {
                    List<AudioClip> clipsCopy = new List<AudioClip>(audioClips[NetworkManager.Singleton.LocalClientId]);

                    // Send client's clips
                    var task = SendClipListAsync(clipsCopy, obj, false, true, NetworkManager.Singleton.LocalClientId);
                    //task.Wait();
                }


            }

            public override void OnNetworkDespawn()
            {
                foreach (var clipList in audioClips.Values)
                    clipList.Clear();
                sent_localID = false;
                ConnectedClientIDs.Clear();

                if (IsServer)
                {
                    sharedMaskedClientDict.Clear();
                    serverReadyDict.Clear();
                }

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
                if (clipFragmentBuffers[senderId].Count == numberOfFragments)
                    Task.Factory.StartNew(() => CombineAudioFragments(senderId));


            }

            private async Task CombineAudioFragments(ulong senderId)
            {
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
                ulong realSenderId = 0;
                byte firstChar = receivedMessageContent[7];
                byte num = receivedMessageContent[6];

                string clipN = "" + Convert.ToChar(firstChar) + num;

                WriteToConsole("ClipN is " + clipN);

                // convert first 8 bytes to ulong
                for (int i = 0; i < 6; i++)
                {
                    realSenderId |= (ulong)receivedMessageContent[i] << (i * 8);
                }
                print("Sender ID is: " + senderId);
                print("Real sender ID is: " + realSenderId);

                // remove sender id header
                byte[] receivedMessageContentNoHeader = new byte[receivedMessageContent.Length - 8];
                Buffer.BlockCopy(receivedMessageContent, 8, receivedMessageContentNoHeader, 0, receivedMessageContentNoHeader.Length);

                AudioClip recievedClip = LoadAudioClip(receivedMessageContentNoHeader);
                recievedClip.name = clipN;
                bool doWeHaveTheClip = false;

                if (!audioClips.Keys.Contains(realSenderId))
                    audioClips.Add(realSenderId, new List<AudioClip>());

                if (IsServer)
                {
                    WriteToConsole($"Sever received ({receivedMessageContentNoHeader}) from client ({realSenderId})");
                    foreach (AudioClip clip in audioClips[realSenderId])
                    {
                        if (clip.name == recievedClip.name || senderId == 0)
                        {
                            WriteToConsole(clip.name);
                            WriteToConsole("We already have this clip!");
                            doWeHaveTheClip = true;
                            WriteToConsole("AudioClip count is now: " + get_clips_count());
                        }
                    }
                    if (!doWeHaveTheClip)
                    {
                        audioClips[realSenderId].Add(recievedClip);

                        // only update clips for server
                        clipNamesArr.Add((FixedString128Bytes)("" + realSenderId + recievedClip.name));
                        WriteToConsole("Added Clip.");
                        WriteToConsole("AudioClip count is now: " + get_clips_count());
                    }
                }
                else
                {
                    WriteToConsole($"Client received ({receivedMessageContentNoHeader}) from the server.");
                    foreach (AudioClip clip in audioClips[realSenderId])
                    {
                        if (clip.name == recievedClip.name || realSenderId == NetworkManager.Singleton.LocalClientId)
                        {
                            WriteToConsole("We already have this clip!");
                            doWeHaveTheClip = true;
                            WriteToConsole("AudioClip count is now: " + get_clips_count());
                        }
                    }
                    if (!doWeHaveTheClip)
                    {
                        audioClips[realSenderId].Add(recievedClip);
                        WriteToConsole("Added Clip.");
                        WriteToConsole("AudioClip count is now: " + get_clips_count());
                    }
                }
            }

            /// <summary>
            /// Invoke this with a Guid by a client or server-host to send a
            /// custom named message.
            /// </summary>
            private void SendMessage(byte[] audioClipFragment, ulong destClient = 0, bool specificClient = false)
            {
                var messageContent = audioClipFragment;
                //WriteToConsole("Writing message...");
                specificClient = false;

                // Steam has max size of 512kb (C)
                var writer = new FastBufferWriter(messageContent.Length, Unity.Collections.Allocator.Temp, 512000);
                //WriteToConsole("Wrote Message");
                var customMessagingManager = NetworkManager.Singleton.CustomMessagingManager;

                using (writer)
                {
                    //WriteToConsole($"Writing {messageContent.Length} bytes of data...");
                    // Issue is here
                    writer.WriteValueSafe(messageContent);
                    //WriteToConsole("Wrote data");

                    if (specificClient)
                    {
                        customMessagingManager.SendNamedMessage(MessageName, destClient, writer, NetworkDelivery.ReliableFragmentedSequenced);
                    }
                    else
                    {

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
            }

            public void SendFragmentedMessage(AudioClip audioClip, ulong destClient = 0, bool specificClient = false, ulong originClient = 0)
            {
                print("Compressing...");
                var message = Compress(ConvertToByteArr(audioClip), originClient, audioClip.name);
                //var message = audioClip;
                WriteToConsole($"Sending message of length {message.Length}");
                if (message.Length > Math.Ceiling(512000 * (float)numberOfFragments))
                {
                    throw new Exception("clip is too large to send! Try increasing the number of message fragments.");
                }

                int offset = (int)Math.Ceiling(message.Length / (float)numberOfFragments);
                //WriteToConsole("Offset size is " + offset);
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
                    SendMessage(fragment, destClient, specificClient);
                }
            }

            public async Task SendClipListAsync(List<AudioClip> clips, ulong destClient = 0, bool specificClient = false, bool shouldSync = false, ulong originClient = 0)
            {
                foreach (var clip in clips)
                {
                    WriteToConsole("Sending " + originClient + "'s clips");
                    SendFragmentedMessage(clip, destClient, specificClient, originClient);

                    // Wait so steam doesnt lump all messages together and yell at me
                    await Task.Delay(200);
                }
                if (IsServer && specificClient)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { destClient }
                        }
                    };

                    // Send connected client's clips to server
                    SendServerMyClipsClientRpc(clientRpcParams);
                }
                else if (!IsServer && shouldSync)
                {
                    var clips_count = get_clips_count();
                    WriteToConsole("Sent " + clips_count + " Clips");

                    // Send all new clips to everyone
                    BroadcastAllNewClipsServerRpc(originClient);
                }
            }

            [ServerRpc(RequireOwnership = false)]
            public void UpdateClientListServerRpc(ulong newClient)
            {
                UpdateClientListClientRpc(newClient);
            }

            [ClientRpc]
            public void UpdateClientListClientRpc(ulong newClient)
            {
                if (!ConnectedClientIDs.Contains(newClient))
                    ConnectedClientIDs.Add(newClient);
                WriteToConsole("New ClientID list is: [" + string.Join(",", ConnectedClientIDs.Select(x => x.ToString()).ToArray()) + "]");
            }

            // ISSUE HERE
            [ServerRpc(RequireOwnership = false)]
            public void BroadcastAllNewClipsServerRpc(ulong senderID)
            {
                if (!audioClips.Keys.Contains(senderID))
                {
                    WriteToConsole("Client " + senderID + " has not synced yet. Requesting sync...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderID }
                        }
                    };
                    SendServerMyClipsClientRpc(clientRpcParams);
                }
                else
                {

                    WriteToConsole("Broadcasting " + senderID + "'s clips - " + audioClips[senderID].Count);
                    List<AudioClip> clipsCopy = new List<AudioClip>(audioClips[senderID]);
                    // Send new clips to everyone
                    var task = SendClipListAsync(clipsCopy, originClient: senderID);
                    //task.Wait();
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderID }
                        }
                    };
                    //var task2 = waitForMSeconds(200);
                    //task2.Wait();
                    ValidateClipsClientRpc(clientRpcParams);
                }
            }

            [ClientRpc]
            public void SendServerMyClipsClientRpc(ClientRpcParams p = default)
            {
                // Send server client's clips and tell it to sync them with everyone
                var task = SendClipListAsync(myClips, shouldSync:true, originClient:NetworkManager.Singleton.LocalClientId);
                //task.Wait();
                
            }

            public async Task waitForMSeconds(int Mseconds)
            {
                await Task.Delay(Mseconds);
            }

            public async Task askServerResendList(List<(ulong COrID, FixedString128Bytes cname)> clipTuples)
            {
                foreach (var tup in clipTuples)
                {
                    AskServerResendClipServerRpc(tup.COrID, tup.cname, NetworkManager.Singleton.LocalClientId);
                    await Task.Delay(200);
                }

            }

            [ClientRpc]
            public void ValidateClipsClientRpc(ClientRpcParams param = default)
            {
                // pass 1 - validate server got all of our clips
                try
                {
                    List<FixedString128Bytes> allClipNames = new List<FixedString128Bytes>();
                    List<AudioClip> missingClips = new List<AudioClip>();
                    foreach (var originId in audioClips.Keys)
                    {
                        bool resend = false;
                        foreach (var clip in audioClips[originId])
                        {
                            allClipNames.Add((FixedString128Bytes)("" + originId + clip.name));
                            if (clipNamesArr.Contains((FixedString128Bytes)("" + originId + clip.name)))
                                continue;

                            // send server missing clip
                            WriteToConsole("Client resending " + originId + clip.name);
                            missingClips.Add(clip);
                            //SendClipListAsync(new List<AudioClip>() { clip }, 0, false, originClient: originId);
                            //SendFragmentedMessage(clip, 0, false, originId);
                            //var task = waitForMSeconds(200);
                            //task.Wait();
                            resend = true;
                        }
                        if (resend)
                        {
                            SendClipListAsync(missingClips, 0, false, true, originId);
                        }
                    }

                    List<(ulong COrID, FixedString128Bytes cname)> clipTuples = new List<(ulong COrID, FixedString128Bytes cname)>();
                    // pass 2 - validate we got all clips from server
                    foreach (var clipName in clipNamesArr)
                    {
                        if (!allClipNames.Contains(clipName))
                        {
                            ulong clipOrigID = 0;
                            foreach (var c in clipName.ToString())
                            {
                                if (c >= 'a' && c <= 'z')
                                    break;
                                clipOrigID *= 10;
                                clipOrigID += (ulong)(c - '0');
                            }
                            clipTuples.Add((clipOrigID, (FixedString128Bytes)(clipName.ToString().Substring(1))));
                            //AskServerResendClipServerRpc(clipOrigID, (FixedString128Bytes)(clipName.ToString().Substring(1)), NetworkManager.Singleton.LocalClientId);
                            //var task = waitForMSeconds(200);
                            //task.Wait();
                        }
                    }
                    askServerResendList(clipTuples);
                }
                catch
                {
                    WriteToConsole("SYNC ERROR");
                }
            }

            [ServerRpc(RequireOwnership = false)]
            public void AskServerResendClipServerRpc(ulong originClipId, FixedString128Bytes name, ulong senderID)
            {
                WriteToConsole("Resending...");
                List<AudioClip> missingClips = new List<AudioClip>();   
                foreach (var clip in audioClips[originClipId])
                {
                    if (clip.name == name)
                    {
                        WriteToConsole("Server Resending " + originClipId + clip.name);
                        missingClips.Add(clip);
                        
                        //SendFragmentedMessage(clip, senderID, false, originClipId);
                        //var task = waitForMSeconds(200);
                        //task.Wait();
                    }
                }
                if (missingClips.Count > 0)
                {
                    SendClipListAsync(missingClips, senderID, false, originClient: originClipId);
                }
            }

            [ServerRpc(RequireOwnership = false)]
            public void TellServerReadyToSendServerRpc(string maskedID, bool ready, ServerRpcParams serverRpcParams = default)
            {
                WriteToConsole("Server got ready signal");
                var clientId = serverRpcParams.Receive.SenderClientId;
                WriteToConsole("Recieved " + ready + " from " + clientId);

                try
                {
                    if (!serverReadyDict[maskedID].ContainsKey(clientId))
                    {
                        serverReadyDict[maskedID].Add(clientId, ready);
                    }
                    else
                    {
                        serverReadyDict[maskedID][clientId] = ready;
                    }
                }
                catch (Exception e)
                {
                    WriteToConsole("ERROR HERE");
                    WriteToConsole(e.Message);
                    foreach (var id in serverReadyDict.Keys)
                        WriteToConsole("" + id);
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
                sharedMaskedClientDict[maskedID] = clientID;
                WriteToConsole($"added masked {maskedID} to masked_client_dict");
            }

            [ClientRpc]
            public void SortAudioClipsClientRpc()
            {
                // Cant be async with latecompany
                sort_audioclips();
            }

            [ServerRpc(RequireOwnership = false)]
            public void TryPlayAudioServerRpc(ulong MimickingID, string maskedID)
            {
                bool ready = true;
                foreach (bool clientReady in serverReadyDict[maskedID].Values)
                {
                    ready = clientReady & ready;
                }

                if (ready)
                {
                    if (serverRand.Next() % 10 == 0)
                    {
                        WriteToConsole("Trying to play audio");
                        int indexToPlay = serverRand.Next() % audioClips[MimickingID].Count;
                        PlayAudioClientRpc(MimickingID, indexToPlay, maskedID);
                    }
                }
            }

            [ClientRpc]
            public void PlayAudioClientRpc(ulong MimickingID, int indexToPlay, string maskedID)
            {
                WriteToConsole($"Masked {maskedID} playing {MimickingID}[{indexToPlay}] - {audioClips[MimickingID][indexToPlay].name}");
                TryToPlayAudio(audioClips[MimickingID][indexToPlay], maskedID);
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

        static void sort_audioclips()
        {
            foreach (var clipList in audioClips.Values)
            {
                clipList.Sort((c1, c2) => c1.name.CompareTo(c2.name));
            }
            WriteToConsole("sorted");
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

            var elevenlabs_client = new ElevenLabs(elevenlabs_api_key.Value);

            string[] readText = File.ReadAllLines(sentences_file_path);
            int i = 0;
            foreach (string s in readText)
            {
                //Task.Factory.StartNew(() => elevenlabs_client.RequestAudio(s, elevenlabs_voice_id.Value, file_name + "0_line", assembly_path + $"\\audio_output\\player0\\{file_name}\\"));
                await elevenlabs_client.RequestAudio(s, elevenlabs_voice_id.Value, file_name + "0_line", assembly_path + $"\\audio_output\\player0\\{file_name}\\", i);
                i++;
            }
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

            log.generation_successful = false;
            log.message = "Not finished generating player sentences";
            log.Save();

            bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
            bool new_idle, new_nearby, new_chasing;
            new_idle = new_nearby = new_chasing = new_player_audio;

            if (elevenlabs_enabled.Value)
            {
                // TODO - switch to true
                found_sample_audio = true;
                WriteToConsole("ELEVENLABS ENABLED");
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

            log.generation_successful = true;
            log.Save();
            doneGenerating = true;
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
        private static ConfigEntry<bool> elevenlabs_enabled;
        private static ConfigEntry<string> elevenlabs_api_key;
        private static ConfigEntry<string> elevenlabs_voice_id;
        static System.Random serverRand = new System.Random();
        private static Dictionary<string, Dictionary<ulong, bool>> serverReadyDict = new Dictionary<string, Dictionary<ulong, bool>>();
        private static Dictionary<string, ulong> sharedMaskedClientDict = new Dictionary<string, ulong>();

        public static List<PlayerControllerB> deadPlayers = new List<PlayerControllerB>();
        Harmony harmonyInstance = new Harmony("my-instance");

        private static string config_path;
        private static string assembly_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // used to track if we need to generate new audio files
        private static DateTime last_successful_generation;
        private static WendigosLog log = new WendigosLog();

        internal static string mic_name;

        internal static ulong steamID;

        static AudioClip mic_audio_clip;

        static List<AudioClip> myClips = new List<AudioClip>();
        public static Dictionary<ulong, List<AudioClip>> audioClips = new Dictionary<ulong, List<AudioClip>>() { { 0, new List<AudioClip>() } };

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            mod_enabled = Config.Bind<bool>(
                "General",
                "Enable mod?",
                false,
                "Enables the mod"
                );

            need_new_player_audio = Config.Bind<bool>(
                "General",
                "Record new player sample audio?",
                true,
                "Whether the record audio prompt should show up"
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
                "Your elevenlabs API key"
                );

            elevenlabs_voice_id = Config.Bind<string>(
                "Elevenlabs",
                "Voice id",
                "",
                "Your elevenlabs voice id"
                );

            if (mod_enabled.Value)
            {
                log.Load();
                log.Save();

                if (log.generation_successful)
                    last_successful_generation = log.last_successful_generation;
                else
                    last_successful_generation = DateTime.MinValue;

                harmonyInstance.PatchAll();
                SceneManager.sceneLoaded += WendigosMessageHandler.ClientConnectInitializer;

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


                config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");

                System.IO.Directory.CreateDirectory(config_path + "Wendigos\\player_sentences");
                System.IO.Directory.CreateDirectory(assembly_path + "\\player_sentences");
                System.IO.Directory.CreateDirectory(assembly_path + "\\sample_player_audio");
                System.IO.Directory.CreateDirectory(assembly_path + "\\audio_output");
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
                WriteToConsole($"Clearing {clientId}'s audio clips");
                WriteToConsole($"Removed {audioClips[clientId].Count} Clips");
                audioClips[clientId].Clear();
                WriteToConsole("AudioClip count is now " + get_clips_count());

                if (WendigosMessageHandler.Instance.IsServer)
                {
                    WendigosMessageHandler.ConnectedClientIDs.Remove(clientId);

                    var sharedMaskedClientDictCopy = new Dictionary<string, ulong>(sharedMaskedClientDict);

                    foreach (var maskedID in sharedMaskedClientDictCopy.Keys)
                    {
                        if (sharedMaskedClientDict[maskedID] == clientId)
                            sharedMaskedClientDict.Remove(maskedID);
                    }

                    // When player leaves, they are always ready
                    foreach (var maskedID in serverReadyDict.Keys)
                    {
                        serverReadyDict[maskedID][clientId] = true;
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

                // TODO: Killed player can see mask mesh on new masked, new masked isnt playing audio


                var players = startOfRound.allPlayerScripts;

                //AudioSource voice = MaskedPlayerEnemy.voice;

                // Get all dead players
                foreach (var player in players)
                {
                    if (!deadPlayers.Contains(player) && player.isPlayerDead)
                    {
                        deadPlayers.Add(player);
                        WriteToConsole("player died -- ID: " + player.actualClientId + " -- name: " + player.name);
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
                sent_localID = false;
            }
        }

        public static string GetHashSHA1(byte[] data)
        {
            using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
            {
                return string.Concat(sha1.ComputeHash(data).Select(x => x.ToString("X2")));
            }
        }

        static AudioClip LoadAudioFile(string audioFilePath)
        {
            if (elevenlabs_enabled.Value)
                return LoadMP3File(audioFilePath);
            else
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

        static AudioClip LoadMP3File(string audioFilePath)
        {
            if (File.Exists(audioFilePath))
            {
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(audioFilePath, AudioType.MPEG))
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

        static void TryToPlayAudio(AudioClip clip, string maskedID)
        {
            try
            {
                WendigosMessageHandler.Instance.TellServerReadyToSendServerRpc(maskedID, false);
                var __instance = maskedInstanceLookup[maskedID];
                if (clip && !__instance.creatureVoice.isPlaying)
                {
                    __instance.creatureVoice.PlayOneShot(clip);
                    waitThenSayReady(__instance, maskedID);
                }
            }
            catch (Exception e)
            {
                WriteToConsole("Playing audio failed: " + e.Message + ": " + e.Source);
            }
        }

        static async void waitThenSayReady(MaskedPlayerEnemy __instance, string maskedID)
        {
            WriteToConsole("WAITING");
            while (__instance.creatureVoice.isPlaying)
                await Task.Delay(10);

            WriteToConsole("SAYING READY");
            WendigosMessageHandler.Instance.TellServerReadyToSendServerRpc(maskedID, true);
            return;

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

                string[] types = ["idle", "nearby", "chasing"];
                string type = types[serverRand.Next(types.Length)];

                string thisMaskedID = __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id;
                ulong MimickingClientID = 0;
                if (!sharedMaskedClientDict.Keys.Contains(thisMaskedID))
                {
                    WriteToConsole("Masked not in dict");
                    return;
                }
                else
                {
                    MimickingClientID = sharedMaskedClientDict[thisMaskedID];
                }


                switch (__instance.currentBehaviourStateIndex)
                {
                    case 0:
                        if (__instance.CheckLineOfSightForClosestPlayer() != null)
                        {

                            WendigosMessageHandler.Instance.TryPlayAudioServerRpc(MimickingClientID, thisMaskedID);
                        }
                        else
                        {
                            WendigosMessageHandler.Instance.TryPlayAudioServerRpc(MimickingClientID, thisMaskedID);
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
                if (!sharedMaskedClientDict.Keys.Contains(thisMaskedID))
                    return;

                ulong MimickingClientID = sharedMaskedClientDict[thisMaskedID];

                WendigosMessageHandler.Instance.TryPlayAudioServerRpc(MimickingClientID, thisMaskedID);

            }

        }

        public class MaskedEnemyIdentifier : MonoBehaviour
        {
            public string id;
        }

        static Dictionary<string, MaskedPlayerEnemy> maskedInstanceLookup = new Dictionary<string, MaskedPlayerEnemy>();

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        class MaskedStartPatch
        {
            static void Postfix(MaskedPlayerEnemy __instance)
            {

                __instance.gameObject.AddComponent<MaskedEnemyIdentifier>();

                // id is starting position since only 1 enemy can spawn per vent
                __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id = __instance.transform.position.ToString();
                maskedInstanceLookup.TryAdd(__instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id, __instance);
                WriteToConsole("Spawned Masked. ID: " + __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id);

                if (WendigosMessageHandler.Instance.IsServer)
                {
                    List<ulong> unassignedClientIDs = new List<ulong>();
                    WriteToConsole(WendigosMessageHandler.ConnectedClientIDs.ToString());

                    foreach (var clientID in WendigosMessageHandler.ConnectedClientIDs)
                    {
                        if (!sharedMaskedClientDict.Values.Contains(clientID))
                            unassignedClientIDs.Add(clientID);
                    }
                    WriteToConsole("Created unasssigned list");

                    // All clients have been assigned a masked
                    if (unassignedClientIDs.Count == 0)
                        return;

                    ulong randomClientID = unassignedClientIDs[serverRand.Next() % unassignedClientIDs.Count];

                    WendigosMessageHandler.Instance.AddToMaskedClientDictServerRpc(
                                __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id,
                                randomClientID
                            );

                    // clientID add and check is done in network manager
                    var result = serverReadyDict.TryAdd(
                            __instance.gameObject.GetComponent<MaskedEnemyIdentifier>().id,
                            new Dictionary<ulong, bool>()
                        );

                    if (!result)
                        WriteToConsole("Failed to add masked");

                    // TODO: Set masked suit to player's suit
                    // __instance.SetSuit();
                    WriteToConsole("added masked to per_masked_ready_dict");
                }

                WriteToConsole("Finished Spawning Masked");
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
        class RoundManagerSpawnPatch
        {
            static void Prefix()
            {
                WriteToConsole("Clearing chared masked dict");
                serverReadyDict.Clear();
                sharedMaskedClientDict.Clear();

                WriteToConsole("Sorting Audioclips");
                sort_audioclips();
                if (NetworkManager.Singleton.IsServer)
                    WendigosMessageHandler.Instance.SortAudioClipsClientRpc();
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
            byte count = 0;
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\idle"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "i" + count;
                myClips.Add(clip);
                count++;
            }

            count = 0;
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\nearby"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "n" + count;
                myClips.Add(clip);
                count++;
            }

            count = 0;
            foreach (string line in Directory.GetFiles(assembly_path + "\\audio_output\\player0\\chasing"))
            {
                AudioClip clip = LoadAudioFile(line);
                clip.name = "c" + count;
                myClips.Add(clip);
                count++;
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
                catch
                {
                    steamID = 1;
                }

                // Show record audio prompt
                __instance.NewsPanel.SetActive(false);
                if (!File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav") || need_new_player_audio.Value)
                {
                    need_new_player_audio.Value = true;
                    __instance.DisplayMenuNotification($"Press R to record some voice lines.\nSelected Mic is {mic_name}", "[ Close ]");
                    Transform responseButton = __instance.menuNotification.transform.Find("Panel").Find("ResponseButton");
                    responseButton.transform.position = new Vector3(responseButton.transform.position.x, responseButton.transform.position.y - 10, responseButton.transform.position.z);
                }
                else
                {
                    if (doneGenerating == false)
                    {
                        __instance.DisplayMenuNotification($"Please wait for audio clips to finish generating", "[ close ]");
                    }
                }
            }
        }

        static async void GeneratingAnimation(MenuManager __instance)
        {
            string[] characterList = ["/", "-", "\\", "|"];
            __instance.menuNotificationText.text += "[" + sentenceTypesCompleted + "/3] |";
            while (!doneGenerating)
            {
                foreach (string c in characterList)
                {
                    __instance.menuNotificationText.text = __instance.menuNotificationText.text.Remove(__instance.menuNotificationText.text.Length-7);
                    __instance.menuNotificationText.text += "[" + sentenceTypesCompleted + "/3] "+ c;
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

        public static byte[] Compress(byte[] data, ulong realID, string name = "b0")
        {
            ulong firstChar = Convert.ToUInt64(name[0]);
            ulong num = Convert.ToUInt64(name.Substring(1));

            realID |= firstChar << 7 * 8;
            realID |= num << 6 * 8;

            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
                dstream.Write(BitConverter.GetBytes(realID), 0, 8);
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
        static bool sent_localID = false;

        public static int get_clips_count()
        {
            int clips_count = 0;
            string outputString = "";
            foreach (var audioListKey in audioClips.Keys)
            {
                clips_count += audioClips[audioListKey].Count;
                outputString += "{";
                foreach (var clip in audioClips[audioListKey])
                {
                    outputString += audioListKey + ":" + clip.name + ", ";
                }
                outputString += "} -- ";
            }
            print(outputString);
            return clips_count;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ShowNameBillboard))]
        class HidePlayerNamePatch
        {
            static void Postfix(PlayerControllerB __instance)
            {
                __instance.usernameAlpha.alpha = 0f;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        class PlayerConnectPatch
        {
            static void Postfix()
            {
                if (!sent_localID)
                {

                    WendigosMessageHandler.Instance.UpdateClientListServerRpc(NetworkManager.Singleton.LocalClientId);

                    if (!audioClips.Keys.Contains(NetworkManager.Singleton.LocalClientId))
                        audioClips.Add(NetworkManager.Singleton.LocalClientId, new List<AudioClip>());

                    //WriteToConsole("Clips count: " + SoundTool.networkedClips.Count);
                    sent_localID = true;
                }

            }
        }

    }
}