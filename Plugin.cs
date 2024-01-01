using BepInEx;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using LC_API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

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

        public static List<PlayerControllerB> fakePlayers = new List<PlayerControllerB>();
        Harmony harmonyInstance = new Harmony("my-instance");

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Logger.LogWarning(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            Logger.LogWarning(fakePlayers.ToString());

            harmonyInstance.PatchAll();

            //StartOfRound startOfRound = StartOfRound.Instance;

            //var currentLevel = RoundManager.Instance.currentLevel;

            //RoundManager.Instance.currentLevel.Enemies.Add(new SpawnableEnemyWithRarity());

        }

        private void Update()
        {
            if (fakePlayers != null)
            {
                File.WriteAllText(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\output.txt", fakePlayers.ToString());
                Logger.LogWarning("Wrote fakeplayers list");
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Update))]
        class MaskedPlayerEnemyPatch
        {
            static void Prefix()
            {
                Console.WriteLine("Patch!");
                StartOfRound startOfRound = StartOfRound.Instance;
                var currentLevel = RoundManager.Instance.currentLevel;

                var players = startOfRound.allPlayerScripts;
                foreach (var player in players)
                {
                    if (player.isPlayerDead && !fakePlayers.Contains(player))
                    {
                        fakePlayers.Add(player);
                        File.WriteAllText("output.txt", fakePlayers.ToString());
                    }
                }


            }
        }


    }
}
