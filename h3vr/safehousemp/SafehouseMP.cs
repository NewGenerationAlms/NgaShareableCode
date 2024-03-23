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
using Atlas;
using Sodalite.ModPanel;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.SafehouseMP", "SafehouseMP", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInDependency("nrgill28.Atlas", "1.0.0")]
    [BepInProcess("h3vr.exe")]
    public partial class SafehouseMP : BaseUnityPlugin
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

        public static string safehouse_persistent_filename = "SPH3MP_Safehouse_Scene";
        public static string raid_extracted_loadout_filename = "SPH3MP_Saved_Loadout";
        public static string safehouse_deployment_loadout_filename = "SPH3MP_OLD_Deployment_Filename";
        public static string safehouse_backup_filename = "BackUp";
        //public static string safehouse_scene_name = "GP_Hangar";
        public static bool extracting_to_safehouse_need_loadout = false;
        public static bool extracting_to_safehouse_need_map = false;

        // Player-Configurable Vars.
        // // House related.
        private static ConfigEntry<int> config_safehouse_index;
        private static ConfigEntry<string> config_safehouse_scene_name;
        private static ConfigEntry<string> config_safehouse_scene_name_TWO;
        private static ConfigEntry<string> config_safehouse_scene_name_THREE;

        // // Saving related.
        private static ConfigEntry<bool> config_delete_loadout_on_spawn;
        private static ConfigEntry<bool> config_delete_loadout_on_save;
        private static ConfigEntry<int> config_chance_to_backup;
        
        public struct MyWristButton {
            public Action<object> onclick;
            public string name;
            public int priority;
            public MyWristButton(string name_, int priority_, Action<object> clickAction = null) {
                onclick = clickAction;
                name = name_;
                priority = priority_;
            }
        } 
        
        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.SafehouseMP");
            
            SetUpConfigFields();

			harmony.PatchAll();
            
            // Your plugin's ID, Name, and Version are available here.
            Logger.LogMessage("Hello, world! Sent from NGA.SafehouseMP 0.0.1");
            
        }

        // Assigns player-set variables.
        private void SetUpConfigFields() {
            config_safehouse_index = Config.Bind("House",
                                         "SceneID INDEX",
                                         1,
                                         new ConfigDescription("Swap between 3 safehouse scene IDs you specified below. All your safehouses will be saved.", new AcceptableValueIntRangeStep(1, 3, 1)));
            config_safehouse_scene_name = Config.Bind("House",
                                         "Safehouse Scene ID: 1",
                                         "GP_Hangar",
                                         "Examples: IndoorRange, ArizonaTargets, Grillhouse_2Story. Find more in Thunderstore. Will CRASH if you get ID wrong.");
            config_safehouse_scene_name_TWO = Config.Bind("House",
                                         "Safehouse Scene ID: 2",
                                         "IndoorRange",
                                         "Index: 2");
            config_safehouse_scene_name_THREE = Config.Bind("House",
                                         "Safehouse Scene ID: 3",
                                         "Grillhouse_2Story",
                                         "Index: 3");
            config_delete_loadout_on_spawn = Config.Bind("Saving",
                                         "Delete Saved Loadout after Spawned",
                                         true,
                                         "Whether the loadout you saved will be deleted after you press the Spawn-Loadout button.");
            config_delete_loadout_on_save = Config.Bind("Saving",
                                         "Clear Quickbelt after Save-Loadout",
                                         false,
                                         "Will clear everything you're wearing from your quickbelt after clicking Save-Loadout.");
            config_chance_to_backup = Config.Bind("Saving",
                                         "Chance to perform a backup (denominator)",
                                         2,
                                         "Creates a backup with a 1 in (denominator) chance% every time scene is saved. Default denominator of 2 means 1/2 or 50% chance.");
        }

        private static string GetSafehouseName() {
            List<string> safehouses = new List<string>{config_safehouse_scene_name.Value, config_safehouse_scene_name_TWO.Value,
                                                        config_safehouse_scene_name_THREE.Value};
            return safehouses[config_safehouse_index.Value - 1];
        }

        [HarmonyPatch(typeof(FVRWristMenu2))]
		[HarmonyPatch("Awake")]
		class WristMenuAwakeHook
		{
			static void Postfix(FVRWristMenu2 __instance)
            {
                if (__instance == null) {
                    Logger.LogMessage("FVRWristMenu2 is null!?");
                }
                Dictionary<GameObject, MyWristButton> existingButtons = new Dictionary<GameObject, MyWristButton>();
                FVRWristMenuSection_Safehouse shMenuSection = AddWristMenuSectionAndButton(__instance, null, "Safehouse MP", 
                    existingButtons, new MyWristButton("Save-Loadout", 4, SaveLoadout));
                AddWristMenuSectionAndButton(__instance, shMenuSection, "Safehouse MP", 
                    existingButtons, new MyWristButton("Spawn-Loadout\n(Host ONLY)", 3, DeploySafehouseLoadout));
                AddWristMenuSectionAndButton(__instance, shMenuSection, "Safehouse MP", 
                    existingButtons, new MyWristButton("Enter-Safehouse", 2, ExtractToSafehouse));
                AddWristMenuSectionAndButton(__instance, shMenuSection, "Safehouse MP", 
                    existingButtons, new MyWristButton("Save-Safehouse", 1, SaveSafehouseScene));
                
                // Grab buttons in priority order, then grab their Text components.
                int butt_ix = 0;
                foreach (GameObject gameObject in existingButtons.Keys.OrderBy((GameObject x) => existingButtons[x].priority))
                {
                    Text leText = gameObject.transform.GetComponentInChildren<Text>();
                    if (butt_ix == 0) {
                        shMenuSection.TXT_SaveSafehouse = leText;
                    }
                    if (butt_ix == 1) {
                        shMenuSection.TXT_EnterSafehouse = leText;
                    }
                    if (butt_ix == 2) {
                        shMenuSection.TXT_SpawnLoadout = leText;
                    }
                    if (butt_ix == 3) {
                        shMenuSection.TXT_SaveLoadout = leText;
                    }
                    butt_ix++;
                }
            }
		}

        [HarmonyPatch(typeof(FVRSceneSettings))]
		[HarmonyPatch("LoadDefaultSceneRoutine")]
		class FVRSceneSettingsLoadDefaultSceneRoutineHook
		{
            static void Prefix(FVRSceneSettings __instance) {
                // Sets certain value null so that original method skips over loading the default scene file.
                if (SceneManager.GetActiveScene().name == GetSafehouseName() && extracting_to_safehouse_need_map
                        && GM.CurrentSceneSettings && GM.CurrentSceneSettings.DefaultVaultSceneConfig != null) {
                    GM.CurrentSceneSettings.DefaultVaultSceneConfig = null;
                }
            }
			static void Postfix(FVRSceneSettings __instance)
            {
                if (__instance == null) {
                    Logger.LogMessage("FVRSceneSettings is null!?");
                }
                Logger.LogMessage("Scene ID: " + SceneManager.GetActiveScene().name);
                if (SceneManager.GetActiveScene().name == GetSafehouseName() && extracting_to_safehouse_need_map) {
                    extracting_to_safehouse_need_map = false;
                    string error;
                    // Attempt loading file if it exists.
                    VaultFile safehouse_curr_vault_file = new VaultFile();
                    string this_safehouse_file_name = GetSafehouseName() + "_" + safehouse_persistent_filename;
                    bool load_file_status = VaultSystem.LoadVaultFile(this_safehouse_file_name,
                                                                        VaultFileDisplayMode.SceneConfigs,
                                                                        out safehouse_curr_vault_file);
                    if (!load_file_status) {
                        Logger.LogMessage("WARNING: Didn't find Safehouse file.");
                    }
                    // Logger.LogMessage(safehouse_curr_vault_file);

                    // Attempts to spawn objects, will spawn empty place if vault file not loaded.
                    bool spawn_objs_status = VaultSystem.SpawnObjects(VaultFileDisplayMode.SceneConfigs, 
                                                                        safehouse_curr_vault_file, out error,
                                                                        null, Vector3.up);
                    if (!spawn_objs_status) {
                        Logger.LogMessage("ERROR: Loading safehouse scene file failed.");
                        Logger.LogMessage(error);
                        return;
                    }
                }
            }
		}

        private static void ExtractToSafehouse(object sender)
		{
            Button button = sender as Button;
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedEnterSafehouse) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedEnterSafehouse = true;
                myWM.TXT_EnterSafehouse.text = "Enter-Safehouse ???";
                return;
            }
            myWM.ResetConfirm();         
			
            // For for going to Safehouse map.
            extracting_to_safehouse_need_map = true;
            for (int i = 0; i < GM.CurrentSceneSettings.QuitReceivers.Count; i++)
			{
				GM.CurrentSceneSettings.QuitReceivers[i].BroadcastMessage("QUIT", SendMessageOptions.DontRequireReceiver);
			}
			if (GM.LoadingCallback.IsCompleted)
			{
                // Checks for if it's a custom scene and swaps load function.
				CustomSceneInfo customScene = AtlasPlugin.GetCustomScene(GetSafehouseName());
                if (customScene != null) {
                    AtlasPlugin.LoadCustomScene(GetSafehouseName());
                } else {
                    SteamVR_LoadLevel.Begin(GetSafehouseName(), false, 0.5f, 0f, 0f, 0f, 1f);
                }
			}
		}

        private static void SaveLoadout(object sender)
		{
            Button button = sender as Button;
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedSaveLoadout) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedSaveLoadout = true;
                myWM.TXT_SaveLoadout.text = "Save-Loadout ???";
                return;
            }
            myWM.ResetConfirm(); 
            // Scan Loadout on person to extract.
            VaultFile loadout_vault_file = new VaultFile();
            bool scanned_quickbelt = VaultSystem.FindAndScanObjectsInQuickbelt(loadout_vault_file);
            if (scanned_quickbelt) {
                if (!VaultSystem.SaveVaultFile(raid_extracted_loadout_filename,  
                        VaultFileDisplayMode.Loadouts, loadout_vault_file)) {
                    Logger.LogMessage("ERROR: Saving loadout failed.");
                    if (button && button.transform) {
                        SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                    }
                    return;
                }
            } else {
                Logger.LogMessage("ERROR: Quickbelt scan failed.");
                if (button && button.transform) {
                    SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                }
                return;
            }

            if (config_delete_loadout_on_save.Value) {
                VaultFile empty_loadout = new VaultFile();
                string myerror;
                FVRPlayerBody pb = UnityEngine.Object.FindObjectOfType<FVRPlayerBody>();
                Transform player_point = new GameObject().transform;
                player_point.position = pb.transform.position;
                bool spawn_objs_status = VaultSystem.SpawnObjects(VaultFileDisplayMode.Loadouts, empty_loadout, out myerror,
                                                                player_point, Vector3.zero);
                if (!spawn_objs_status) {
                    Logger.LogMessage("ERROR: Couldn't spawn empty loadout w status: " + myerror);
                }
            }
        }

        private static void SaveSafehouseScene(object sender) {
            Button button = sender as Button;
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedSaveSafehouse) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedSaveSafehouse = true;
                myWM.TXT_SaveSafehouse.text = "Save-Safehouse ???";
                return;
            }
            myWM.ResetConfirm();

            if (SceneManager.GetActiveScene().name != GetSafehouseName()) {
                Logger.LogMessage("WARNING: Won't save since this isn't selected House: " + GetSafehouseName());
                if (button && button.transform) {
                    SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                }
                return;
            }

            // Scan the Safehouse Level for persistance.
            VaultFile safehouse_vault_file = new VaultFile();
            // T-ODO: Scan respawn point?
            bool scanned_scene = VaultSystem.FindAndScanObjectsInScene(safehouse_vault_file);
            if (scanned_scene) {
                string this_safehouse_file_name = GetSafehouseName() + "_" + safehouse_persistent_filename;
                if (!VaultSystem.SaveVaultFile(this_safehouse_file_name,  
                        VaultFileDisplayMode.SceneConfigs, safehouse_vault_file)) {
                    Logger.LogMessage("ERROR: Scene Vault save failed");
                    if (button && button.transform) {
                        SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                    }
                    return;
                }
            } else {
                Logger.LogMessage("ERROR: Scene scan failed");
                if (button && button.transform) {
                    SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                }
                return;
            }
            // Generate a random number between 1 and 2
            int randomChance = UnityEngine.Random.Range(1, config_chance_to_backup.Value+1);
            // Check if the random number is 1 (1/2 chance)
            if (randomChance == 1)
            {
                // Create another variable similar to safehouse_vault_file
                VaultFile secondVaultFile = new VaultFile();

                // Perform the same operation
                bool secondScannedScene = VaultSystem.FindAndScanObjectsInScene(secondVaultFile);

                // Do something with secondVaultFile or secondScannedScene if needed
                if (secondScannedScene) {
                    // Get the current date and time
                    DateTime currentDateTime = DateTime.Now;

                    // Format the date and time as a string suitable for a filename
                    string formattedDateTime = currentDateTime.ToString("yyyyMMdd_HHmmss");
                    string this_safehouse_file_name = GetSafehouseName() + "_" + safehouse_backup_filename
                                                        + "_" + formattedDateTime;
                    if (!VaultSystem.SaveVaultFile(this_safehouse_file_name,  
                            VaultFileDisplayMode.SceneConfigs, secondVaultFile)) {
                        Logger.LogMessage("ERROR: BACKUP Scene Vault save failed");
                        if (button && button.transform) {
                            SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                        }
                        return;
                    }
                }
            }
        }

        private static void DeploySafehouseLoadout(object sender)
		{
            Button button = sender as Button;
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedSpawnLoadout) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedSpawnLoadout = true;
                myWM.TXT_SpawnLoadout.text = "Spawn-Loadout ???";
                return;
            }
            myWM.ResetConfirm();

            FVRPlayerBody pb = UnityEngine.Object.FindObjectOfType<FVRPlayerBody>();
            Transform player_point = new GameObject().transform;
            player_point.position = pb.transform.position;
            string error;
            VaultFile again_vault_file = new VaultFile();
            bool load_file_status = VaultSystem.LoadVaultFile(raid_extracted_loadout_filename,
                                                                VaultFileDisplayMode.Loadouts,
                                                                out again_vault_file);
            if (!load_file_status) {
                Logger.LogMessage("ERROR: Loading saved loadout failed.");
                if (button && button.transform) {
                    SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                }
                return;
            }
            bool spawn_objs_status = VaultSystem.SpawnObjects(VaultFileDisplayMode.Loadouts, again_vault_file, out error,
                                                                player_point, Vector3.zero);
            if (!spawn_objs_status) {
                Logger.LogMessage("ERROR: Spawning saved loadout failed.");
                if (button && button.transform) {
                    SM.PlayGlobalUISound(SM.GlobalUISound.Error, button.transform.position);
                }
                return;
            }

            if (config_delete_loadout_on_spawn.Value) {
                // Clear loadout after deploy.
                VaultFile clean_vault_file = new VaultFile();
                VaultSystem.SaveVaultFile(raid_extracted_loadout_filename,  
                                            VaultFileDisplayMode.Loadouts, clean_vault_file);
            }
        }


        private static void AddWristMenuButton(FVRWristMenuSection shMenuSection, MyWristButton new_button, Transform buttonTransform) {
			
            buttonTransform.GetComponentInChildren<Text>().text = new_button.name;
            Button component = buttonTransform.GetComponent<Button>();
            component.onClick = new Button.ButtonClickedEvent();
            component.onClick.AddListener(delegate
            {
                SM.PlayGlobalUISound(SM.GlobalUISound.Beep, buttonTransform.position);
                new_button.onclick(component);
            });
        }

        private static void ResizeAndReorderSection(RectTransform background, int oldButtonCount, 
            Dictionary<GameObject, MyWristButton> existingButtons)
        {
            Vector2 sizeDelta = background.sizeDelta;
            int childCount = background.childCount;
            RectTransform rectTransform = (RectTransform)background.GetChild(0);
            float num = rectTransform.sizeDelta.y * rectTransform.localScale.y;
            float num2 = (sizeDelta.y - num * (float)oldButtonCount) / (float)(oldButtonCount + 2);
            float num3 = num + num2;
            background.sizeDelta = new Vector2(sizeDelta.x, (float)childCount * num3 + num2 * 2f);
            float num4 = num3 / 2f + num2;
            foreach (GameObject gameObject in existingButtons.Keys.OrderBy((GameObject x) => existingButtons[x].priority))
            {
                gameObject.transform.localPosition = new Vector3(0f, num4, 0f);
                num4 += num3;
            }
        }

        private static FVRWristMenuSection_Safehouse AddWristMenuSectionAndButton(FVRWristMenu2 instance2, FVRWristMenuSection_Safehouse menuSection, 
                        string sectionText, Dictionary<GameObject, MyWristButton> existingButtons, MyWristButton new_button) {
            if (menuSection == null) {
                FVRWristMenuSection fvrwristMenuSection = instance2.Sections
                    .First((FVRWristMenuSection x) => x.GetType() == typeof(FVRWristMenuSection_Spawn));
                Transform transform = fvrwristMenuSection.transform;
                FVRWristMenuSection fvrwristMenuSection2 = UnityEngine.Object
                    .Instantiate<FVRWristMenuSection>
                        (fvrwristMenuSection, transform.position, transform.rotation, transform.parent);
                GameObject gameObject = fvrwristMenuSection2.gameObject;
                UnityEngine.Object.Destroy(fvrwristMenuSection2);
                // Get the renderer component
                Renderer rend = gameObject.GetComponent<Renderer>();
                if (rend != null)
                {
                    // Change the material's color
                    Color newColor = new Color(0.3f, 0.67f, 0.3f, 1.0f);
                    rend.material.color = newColor;
                }
                menuSection = gameObject.AddComponent<FVRWristMenuSection_Safehouse>();
                menuSection.ButtonText = sectionText;
                menuSection.Menu = instance2;
                instance2.Sections.Add(menuSection);
                instance2.RegenerateButtons();
                RectTransform rectTransform = (RectTransform)menuSection.transform;
                RectTransform rectTransform2 = (RectTransform)rectTransform.GetChild(0);
                int childCount = rectTransform.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    RectTransform rectTransform3 = (RectTransform)rectTransform.GetChild(i);
                    if (!(rectTransform3 == rectTransform2))
                    {
                        UnityEngine.Object.DestroyImmediate(rectTransform3.gameObject);
                    }
                }
                existingButtons.Add(rectTransform2.gameObject, new_button);
                AddWristMenuButton(menuSection,  new_button, rectTransform2);
                ResizeAndReorderSection(rectTransform, childCount, existingButtons);
                return menuSection;
            }
            RectTransform rectTransform4 = (RectTransform)menuSection.transform;
            Transform child = rectTransform4.GetChild(0);
            RectTransform rectTransform5 = (RectTransform)UnityEngine.Object.Instantiate<GameObject>(child.gameObject, child.position, child.rotation, child.parent).transform;
            existingButtons.Add(rectTransform5.gameObject, new_button);
            AddWristMenuButton(menuSection, new_button, rectTransform5);
            ResizeAndReorderSection(rectTransform4, rectTransform4.childCount - 1, existingButtons);
            return menuSection;
        }

        
        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)
        internal new static ManualLogSource Logger { get; private set; }

    }

    public class FVRWristMenuSection_Safehouse : FVRWristMenuSection
	{
        public void ResetConfirm()
		{
			this.confirmedSaveLoadout = false;
			this.confirmedSpawnLoadout = false;
			this.confirmedEnterSafehouse = false;
            this.confirmedSaveSafehouse = false;
            this.TXT_SaveLoadout.text = "Save-Loadout";
			this.TXT_SpawnLoadout.text = "Spawn-Loadout";
			this.TXT_EnterSafehouse.text = "Enter-Safehouse";
            this.TXT_SaveSafehouse.text = "Save-Safehouse";
		}

        public override void OnHide()
		{
			this.ResetConfirm();
		}
        public Text TXT_SaveLoadout;
        public Text TXT_SpawnLoadout;
        public Text TXT_EnterSafehouse;
        public Text TXT_SaveSafehouse;
        public bool confirmedSaveLoadout = false;
        public bool confirmedSpawnLoadout = false;
        public bool confirmedEnterSafehouse = false;
        public bool confirmedSaveSafehouse = false;
    }

}
