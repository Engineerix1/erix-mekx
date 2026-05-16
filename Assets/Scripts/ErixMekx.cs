using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using ErixMekx.UI;
using HarmonyLib;
using LaunchPadBooster;
using UnityEngine;

namespace ErixMekx
{
    [BepInPlugin(ErixMekxMain.PluginGuid, ErixMekxMain.PluginName, ErixMekxMain.PluginVersion)]
    public class ErixMekxMain : MonoBehaviour
    {
        public const string PluginGuid = "erx.erixmekx";
        public const string PluginName = "ErixMekx";
        public const string PluginVersion = "1.0.1";
        public static readonly Mod MOD = new(PluginName, PluginVersion);
        public static GameObject PanelRobotPrefab;
        public static void Log(string line)
        {
            Debug.Log("[" + PluginName + "]: " + line);
        }
        private Harmony _harmony;

        public void OnLoaded(List<GameObject> prefabs, ConfigFile config)
        {
            ErixMekx.Systems.RobotConfig.Init(config);
            MOD.AddPrefabs(prefabs);

            const string targetName = "PanelRobot";

            foreach (GameObject obj in prefabs)
            {
                if (obj?.name == targetName)
                {
                    PanelRobotPrefab = obj;
                    break; // Found it, stop searching
                }
            }

            try
            {
                // Initialize the RobotConfig values before patching
                new RobotUIManager().Initialize();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                Log("ErixMekx patches loaded!");
            }
            catch (Exception ex)
            {
                Log($"{PluginGuid} Error during Awake: {ex}");
            }
        }

        private void OnDestroy()
        {
            Log($"OnDestroy (Version: {PluginVersion})");

            if (_harmony is null)
                return;

            try
            {
                // If using BepInEx ScriptEngine for hot-reload, you may want to unpatch here.
                // For release builds, be aware this can be called immediately after Awake(),
                // which may break your mod depending on your use-case.

                _harmony.UnpatchSelf();
                Destroy(PanelRobotPrefab);
            }
            catch (Exception ex)
            {
                Log(
                    $"Error while unpatching Harmony in {nameof(OnDestroy)}: {ex}"
                );
            }
            finally
            {
                _harmony = null;
            }
        }
    }
}
