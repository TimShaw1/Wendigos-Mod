using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using LC_API;
using System.Collections.Generic;
using System.Linq;
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

        public static List<FakePlayer> fakePlayers = new List<FakePlayer>();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            StartOfRound startOfRound = StartOfRound.Instance;

            var currentLevel = RoundManager.Instance.currentLevel;

            //RoundManager.Instance.currentLevel.Enemies.Add(new SpawnableEnemyWithRarity());

            Logger.LogInfo($"");

        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Update))]
        class MaskedPlayerEnemyPatch
        {
            static void Postfix()
            {
                StartOfRound startOfRound = StartOfRound.Instance;
                var currentLevel = RoundManager.Instance.currentLevel;

                var players = startOfRound.allPlayerScripts;
                foreach (var player in players)
                {
                    if (player.isPlayerDead)
                    {
                        fakePlayers.Add(new FakePlayer(player));
                    }
                }


            }
        }
    }
}
