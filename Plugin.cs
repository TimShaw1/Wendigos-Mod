﻿using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using LC_API;
using System;
using System.Collections;
using System.Collections.Generic;
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

        public static List<PlayerControllerB> deadPlayers = new List<PlayerControllerB>();
        Harmony harmonyInstance = new Harmony("my-instance");
        public static List<MaskedPlayerEnemy> maskedEnemies;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            //Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            harmonyInstance.PatchAll();

            Console.WriteLine(Config.ConfigFilePath.Replace("Wendigos.cfg", ""));

            string config_path = Config.ConfigFilePath.Replace("Wendigos.cfg", "");

            System.IO.Directory.CreateDirectory(config_path + "Wendigos\\player_sentences");

            File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_idle_sentences.txt",
                "Help me\n" +
                "Stop Sign Over Here\n" +
                "Where is everyone?"
                );

            File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_nearby_sentences.txt",
                "Help me\n" +
                "Stop Sign Over Here\n" +
                "Where is everyone?"
                );

            File.WriteAllText(config_path + "Wendigos\\player_sentences\\player0_chasing_sentences.txt",
                "Help me\n" +
                "Stop Sign Over Here\n" +
                "Where is everyone?"
                );

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
                        Debug.Log("www.error " + request.error);
                        Debug.Log(" www.uri " + request.uri);
                        Debug.Log(" www.url " + request.url);
                        Debug.Log(" www.result " + request.result);
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
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\audio_output" + "\\output0_line" + rand.Next(0,3) + ".wav";
                try
                {
                    // Sandworm bug? Avoid sandworm if necessary
                    AudioClip clip = LoadWavFile(path);
                    if (clip && !__instance.creatureVoice.isPlaying)
                        __instance.creatureVoice.PlayOneShot(clip);
                }
                catch(Exception e) {
                    Console.WriteLine("Playing audio failed: " + e.Message + ": " + e.Source);
                }
            }
        }


    }
}
