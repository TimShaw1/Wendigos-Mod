using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using LC_API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

// StartOfRound requires adding the game's Assembly-CSharp to dependencies

namespace Wendigos
{

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public class FakePlayer
        {
            public PlayerControllerB playerControllerB;
            public bool isControlled = false;

            public FakePlayer(PlayerControllerB pcB)
            {
                playerControllerB = pcB;
            }
        }

        /// <summary>
        /// Launch main.exe with args.
        /// This will DELETE any pre-existing folder \\audio_output\\player0\\{file_name}.
        /// </summary>
        static void GeneratePlayerSentences(string file_name, string sentences_file_path)
        {
            // Player0 only for now
            File.WriteAllText(assembly_path + "\\player_sentences\\player0_sentences.txt", File.ReadAllText(sentences_file_path));
            Console.WriteLine("wrote to sentences text file");


            if (Directory.Exists(assembly_path + $"\\audio_output\\player0\\{file_name}"))
            {
                Directory.Delete(assembly_path + $"\\audio_output\\player0\\{file_name}", true);
                Console.WriteLine($"deleted old wav files for {file_name}");
            }
            Directory.CreateDirectory(assembly_path + $"\\audio_output\\player0\\{file_name}");
            Console.WriteLine($"created directory \\audio_output\\player0\\{file_name}");


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
                    Console.WriteLine("started process");
                    exeProcess.OutputDataReceived += (sender, args) =>
                    {
                        Console.WriteLine($"received output: {args.Data}");
                        if (args.Data.Contains("[y/n]"))
                        {
                            Console.WriteLine("WAITING FOR MODEL DOWNLOAD ... (1.75gb)");

                            exeProcess.StandardInput.WriteLine("y");
                        }
                    };
                    exeProcess.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
                    exeProcess.BeginOutputReadLine();
                    exeProcess.BeginErrorReadLine();
                    Console.WriteLine("reading process");
                    exeProcess.WaitForExit();
                }
            }
            catch
            {
                // Log error.
            }

            File.Delete(assembly_path + "\\player_sentences\\player0_sentences.txt");
            Console.WriteLine("deleted temporary sentences text file");
        }

        static void GenerateAllPlayerSentences()
        {
            GeneratePlayerSentences("idle", config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt");
            GeneratePlayerSentences("nearby", config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt");
            GeneratePlayerSentences("chasing", config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt");
        }

        private bool isFileChanged(string path)
        {
            DateTime timestamp = File.GetLastWriteTime(path);

            return timestamp > main_last_accessed;
        }

        public static List<PlayerControllerB> deadPlayers = new List<PlayerControllerB>();
        Harmony harmonyInstance = new Harmony("my-instance");
        public static List<MaskedPlayerEnemy> maskedEnemies;

        private static string config_path;
        private static string assembly_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // used to track if we need to generate new audio files
        private static DateTime main_last_accessed = File.GetLastAccessTime(assembly_path + "\\main.exe");

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            harmonyInstance.PatchAll();

            Console.WriteLine(Config.ConfigFilePath.Replace("Wendigos.cfg", ""));

            config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");

            System.IO.Directory.CreateDirectory(config_path + "Wendigos\\player_sentences");
            System.IO.Directory.CreateDirectory(assembly_path + "\\player_sentences");
            System.IO.Directory.CreateDirectory(assembly_path + "\\sample_player_audio");
            System.IO.Directory.CreateDirectory(assembly_path + "\\audio_output");
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: Created/found config directories");

            bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: {(found_sample_audio ? "found" : "didn't find")} player sample audio");

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
                GeneratePlayerSentences("idle", config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt");
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: generated idle sentences");
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
                GeneratePlayerSentences("nearby", config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt");
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: generated nearby sentences");
            }

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
                GeneratePlayerSentences("chasing", config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt");
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: generated chasing sentences");
            }

            maskedEnemies = UnityEngine.Object.FindObjectsOfType<MaskedPlayerEnemy>(false).ToList();

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
                        Console.WriteLine("player died -- ID: " + player.playerSteamId + " -- name: " + player.name);
                    }
                }

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
                        Console.WriteLine("www.error " + request.error);
                        Console.WriteLine(" www.uri " + request.uri);
                        Console.WriteLine(" www.url " + request.url);
                        Console.WriteLine(" www.result " + request.result);
                        return null;
                    }
                    else
                    {
                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(request);
                        return myClip;
                    }
                }
            }
            Console.WriteLine("AUDIO FILE NOT FOUND");
            return null;
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.DoAIInterval))]
        class MaskedPlayerEnemyAIPatch
        {
            static void Prefix(MaskedPlayerEnemy __instance)
            {
                var rand = new System.Random();
                string[] types = { "idle", "nearby", "chasing"};
                string type = types[rand.Next(types.Length)];

                if (rand.Next(10) % 10 == 0)
                {
                    var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\audio_output" + $"\\player0\\{type}\\{type}0_line" + rand.Next(0, 3) + ".wav";
                    try
                    {
                        // Sandworm bug? Avoid sandworm if necessary
                        AudioClip clip = LoadWavFile(path);
                        if (clip && !__instance.creatureVoice.isPlaying)
                            __instance.creatureVoice.PlayOneShot(clip);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Playing audio failed: " + e.Message + ": " + e.Source);
                    }
                }
            }
        }


    }
}
