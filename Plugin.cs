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
            public int playerSuitID;


            public FakePlayer(PlayerControllerB pcB)
            {
                playerControllerB = pcB;
                playerSuitID = playerControllerB.currentSuitID;
            }
        }

        static void WriteToConsole(string output)
        {
            Console.WriteLine("Wendigos: " + output);
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

        static void GenerateAllPlayerSentences(bool new_idle, bool new_nearby, bool new_chasing)
        {
            if (new_idle) 
                GeneratePlayerSentences("idle", config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt");

            if (new_nearby)
                GeneratePlayerSentences("nearby", config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt");

            if (new_chasing)
                GeneratePlayerSentences("chasing", config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt");

            WriteToConsole("Finished generating voice lines.");
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

        internal static string mic_name;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            harmonyInstance.PatchAll();

            WriteToConsole(Config.ConfigFilePath.Replace("Wendigos.cfg", ""));

            config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");

            System.IO.Directory.CreateDirectory(config_path + "Wendigos\\player_sentences");
            System.IO.Directory.CreateDirectory(assembly_path + "\\player_sentences");
            System.IO.Directory.CreateDirectory(assembly_path + "\\sample_player_audio");
            System.IO.Directory.CreateDirectory(assembly_path + "\\audio_output");
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: Created/found config directories");

            bool found_sample_audio = File.Exists(assembly_path + "\\sample_player_audio\\sample_player0_audio.wav");
            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: {(found_sample_audio ? "found" : "didn't find")} player sample audio");

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
                new_nearby = true;
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
                new_chasing=true;
                Logger.LogInfo($"{PluginInfo.PLUGIN_GUID}: generated chasing sentences");
            }

            // start generating voice lines async
            Task.Factory.StartNew(() => GenerateAllPlayerSentences(new_idle, new_nearby, new_chasing));

            //maskedEnemies = UnityEngine.Object.FindObjectsOfType<MaskedPlayerEnemy>(false).ToList();

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

        static void TryToPlayAudio(string type, MaskedPlayerEnemy __instance)
        {
            try
            {
                string path = GetPathOfWav(type);
                // Sandworm bug? Avoid sandworm if necessary
                AudioClip clip = LoadWavFile(path);
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

                var rand = new System.Random();
                string[] types = ["idle", "nearby", "chasing"];
                string type = types[rand.Next(types.Length)];


                switch (__instance.currentBehaviourStateIndex)
                {
                    case 0:
                        if (__instance.CheckLineOfSightForClosestPlayer() != null)
                        {
                            TryToPlayAudio("nearby", __instance);
                        }
                        else
                        {
                            TryToPlayAudio("idle", __instance);
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
                
                TryToPlayAudio(type, __instance);

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

        [HarmonyPatch(typeof(MenuManager), "Start")]
        class MenuManagerPatch
        {
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene) 
                {
                    return;
                }

                __instance.NewsPanel.SetActive(false);
                __instance.DisplayMenuNotification("Press 'r' to record some voice lines", "[ Done ]");
                __instance.menuNotification.transform.Find("ResponseButton").gameObject.SetActive(false);
            }
        }

        static AudioClip ac;
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
            """.Split('\n');

        [HarmonyPatch(typeof(MenuManager), "Update")]
        class MenuManagerUpdatePatch
        {
            static void Postfix(MenuManager __instance)
            {
                if (__instance.isInitScene) { return; }

                if (!Microphone.IsRecording(mic_name))
                {
                    if (UnityInput.Current.GetKeyDown("R"))
                    {
                        // Get max frequency of mic device
                        int minfreq;
                        int maxfreq;
                        Microphone.GetDeviceCaps(mic_name, out minfreq, out maxfreq);

                        ac = Microphone.Start(mic_name, false, 100, maxfreq);
                        __instance.menuNotificationButtonText.text = "[ Recording ]";

                        System.Random rand = new System.Random();
                        __instance.menuNotificationText.text = lines_to_read[rand.Next(lines_to_read.Length)];
                    }
                }
                else
                {
                    if (UnityInput.Current.GetKeyUp("R"))
                    {
                        Microphone.End(mic_name);
                        __instance.menuNotificationButtonText.text = "[ done ]";
                        __instance.menuNotificationText.text = "Recording stopped";
                        ac = SavWav.TrimSilence(ac, 0.01f);
                        SavWav.Save(assembly_path + "\\sample_player_audio\\sample_player1_audio.wav", ac);
                    }
                    else if (UnityInput.Current.GetKeyUp("N"))
                    {
                        System.Random rand = new System.Random();
                        __instance.menuNotificationText.text = lines_to_read[rand.Next(lines_to_read.Length)];
                    }
                }
            }
        }



    }
}
