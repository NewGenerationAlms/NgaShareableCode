using BepInEx;
using BepInEx.Logging;
using Sodalite.Api;
using Sodalite.UiWidgets;
using FistVR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Sodalite.Utilities;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using Sodalite;
using static FistVR.ItemSpawnerV2;
using BepInEx.Configuration;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;
using FMOD;
using RootMotion;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.SaveItAll", "SaveItAll", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class SaveItAll : BaseUnityPlugin
    {
        /* == Quick Start == 
         * Your plugin class is a Unity MonoBehaviour that gets added to a global game object when the game starts.
         * You should use Awake to initialize yourself, read configs, register stuff, etc.
         * If you need to use Update or other Unity event methods those will work too.
         *
         * Some references on how to do various things:
         * Adding config settings to your plugin: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/4_configuration.html
         * Hooking / Patching game methods: https://harmony.pardeike.net/articles/patching.html
         * Also check out the Unity documentation: https://docs.unity3d.com/560/Documentation/ScriptReference/index.html
         * And the C# documentation: https://learn.microsoft.com/en-us/dotnet/csharp/
         */

        // Player-Configurable Vars.
        private static ConfigEntry<bool> config_enable_vanilla;
        private static ConfigEntry<bool> config_enable_tnh;
        private static ConfigEntry<bool> config_enable_modded;
        private static ConfigEntry<string> config_exclude_ids;
        private static ConfigEntry<string> config_include_ids;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.SaveItAll");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.SaveItAll 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            config_enable_vanilla = Config.Bind("Scenes Saving Allowed",
                                         "All Vanilla Scenes",
                                         true,
                                         "Enables scene saving in all Main Menu accessible scenes");
            config_enable_modded = Config.Bind("Scenes Saving Allowed",
                                         "Any Modded Map",
                                         true,
                                         "Enables scene saving in any Atlas map installed from Thundersore. !!Wurstmod maps DONT work!!");
            config_enable_tnh = Config.Bind("Scenes Saving Allowed",
                                         "Vanilla TnH Maps",
                                         false,
                                         "Enables scene saving in all vanilla TnH Menu accessible maps");
            config_include_ids = Config.Bind("Custom Include/Exclude",
                                         "Scenes IDs to INCLUDE",
                                         "sampleID5,sampleID7",
                                         "Comma, separated list of scene IDs to INCLUDE from scene saving. Enforced after all conditions above.");
            config_exclude_ids = Config.Bind("Custom Include/Exclude",
                                         "Scenes IDs to EXCLUDE",
                                         "sampleID1,sampleID2",
                                         "Comma, separated list of scene IDs to EXCLUDE from scene saving. Enforced after all conditions above.");
        }

        [HarmonyPatch(typeof(FVRSceneSettings))]
		[HarmonyPatch("Awake")]
		class FVRSceneSettingsAwakeHook
		{
            static void Prefix(FVRSceneSettings __instance) {
                bool is_scene_saving_allowed = false;
                List<string> vanillaScenes = new List<string> {"Grillhouse_2Story", "IndoorRange",
                                                                "GP_Hangar", "SniperRange", "ArizonaTargets", "WarehouseRange_Rebuilt",
                                                                "Friendly45_New", "ArizonaTargets_Night", "BreachAndClear_TestScene1",
                                                                "ProvingGround", "ObstacleCourseScene1", "NewSnowGlobe", "Wurstwurld1",
                                                                "MF2_MainScene", "Boomskee", "Testing3_LaserSword","MeatGrinder",
                                                                "OmnisequencerTesting3","WinterWasteland","Cappocolosseum",
                                                                "SamplerPlatter"};
                List<string> tnhScenes = new List<string> {"Institution","TakeAndHoldClassic","TakeAndHold_WinterWasteland"};
                List<string> customIncludedList = config_include_ids.Value.Split(',').ToList();
                List<string> customExcludedList = config_exclude_ids.Value.Split(',').ToList();
                List<string> allowedList = new List<string>();
                if (config_enable_vanilla.Value) {
                    allowedList.AddRange(vanillaScenes);
                }
                if (config_enable_tnh.Value) {
                    allowedList.AddRange(tnhScenes);
                }

                if (allowedList.Contains(SceneManager.GetActiveScene().name)) {
                    is_scene_saving_allowed = true;
                }
                if (config_enable_modded.Value && (SceneManager.GetActiveScene().buildIndex == -1)) {
                    Logger.LogMessage("Detected modded map, setting to true.");
                    is_scene_saving_allowed = true;
                }
                if (customIncludedList.Contains(SceneManager.GetActiveScene().name)) {
                    is_scene_saving_allowed = true;
                }
                if (customExcludedList.Contains(SceneManager.GetActiveScene().name)) {
                    is_scene_saving_allowed = false;
                }

                Logger.LogMessage("Scene manager num scenes " + SceneManager.sceneCount);

                if (!__instance.IsSceneSavingEnabled) {
                    Logger.LogMessage("Modifying scene save, it was originally set to False!");
                    __instance.IsSceneSavingEnabled = is_scene_saving_allowed;
                }
                Logger.LogMessage("Scene saving is set to " + __instance.IsSceneSavingEnabled 
                                    + " in " + SceneManager.GetActiveScene().name + " w build index: " 
                                    + SceneManager.GetActiveScene().buildIndex);
                PrintLists(vanillaScenes, tnhScenes, customIncludedList, customExcludedList, allowedList);
            }

            static void PrintLists(List<string> vanillaScenes, List<string> tnhScenes, List<string> customIncludedList, List<string> customExcludedList, List<string> allowedList) {
                Logger.LogMessage("===== Printing Lists =====");

                Logger.LogMessage("Vanilla Scenes:");
                foreach (string scene in vanillaScenes) {
                    Logger.LogMessage(scene);
                }

                Logger.LogMessage("TNH Scenes:");
                foreach (string scene in tnhScenes) {
                    Logger.LogMessage(scene);
                }

                Logger.LogMessage("Custom Included List:");
                foreach (string scene in customIncludedList) {
                    Logger.LogMessage(scene);
                }

                Logger.LogMessage("Custom Excluded List:");
                foreach (string scene in customExcludedList) {
                    Logger.LogMessage(scene);
                }

                Logger.LogMessage("Allowed List:");
                foreach (string scene in allowedList) {
                    Logger.LogMessage(scene);
                }

                Logger.LogMessage("==========================");
            }

            static void Postfix(FVRSceneSettings __instance) {
                Logger.LogMessage("Confirming scene save set to: " + __instance.IsSceneSavingEnabled);
            }
		}

        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)

        internal new static ManualLogSource Logger { get; private set; }

    }
}
