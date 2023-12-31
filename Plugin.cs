using BepInEx;
using LC_API;
using System.Linq;
using UnityEngine;

// StartOfRound requires adding the game's Assembly-CSharp to dependencies

namespace Wendigos
{
    public class WendigoAI : MaskedPlayerEnemy
    {
        public override void Update()
        {
            base.Update();
            {

            }
        }
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            StartOfRound startOfRound = StartOfRound.Instance;

            var currentLevel = RoundManager.Instance.currentLevel;

            RoundManager.Instance.currentLevel.Enemies.Add(new SpawnableEnemyWithRarity());
            
        }

        private void Update()
        {

        }
    }
}
