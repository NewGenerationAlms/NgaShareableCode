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

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.SafehouseProgression", "SafehouseProgression", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class SafehouseProgression : BaseUnityPlugin
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

        public static string raid_extracted_loadout_filename = "Loadout_Extracted_From_Raid";
        public static string safehouse_persistent_filename = "Safehouse_Persitent_Filename";
        public static string safehouse_save_filename = "BU";
        public static string safehouse_deployment_loadout_filename = "Safehouse_Deployment_Filename";
        //public static string safehouse_scene_name = "GP_Hangar";
        public static bool extracting_to_safehouse_need_loadout = false;
        public static bool extracting_to_safehouse_need_map = false;

        // Player-Configurable Vars.
        private static ConfigEntry<string> config_safehouse_scene_name;
        
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

            Harmony harmony = new Harmony("NGA.SafehouseProgression");
            
            SetUpConfigFields();

			harmony.PatchAll();
            
            // Your plugin's ID, Name, and Version are available here.
            Logger.LogMessage($"Hello, world! Sent from NGA.SafehouseProgression 0.0.1");
            
        }

        // Assigns player-set variables.
        private void SetUpConfigFields() {
            config_safehouse_scene_name = Config.Bind("House",
                                         "Safehouse Scene ID",
                                         "GP_Hangar",
                                         "Requires scene to be scene-save enabled. Will CRASH if you get ID wrong. Find IDs in H3VR Wiki.");
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
                FVRWristMenuSection_Safehouse shMenuSection = AddWristMenuSectionAndButton(__instance, null, "Safehouse™", 
                    existingButtons, new MyWristButton("Extract-to-Safehouse", 3, ExtractToSafehouse));
                AddWristMenuSectionAndButton(__instance, shMenuSection, "Safehouse", 
                    existingButtons, new MyWristButton("Leave-the-Safehouse", 2, LeaveTheSafehouse));
                AddWristMenuSectionAndButton(__instance, shMenuSection, "Safehouse", 
                    existingButtons, new MyWristButton("Deployment-Loadout", 1, DeploySafehouseLoadout));
                
                // Grab buttons in priority order, then grab their Text components.
                int butt_ix = 0;
                foreach (GameObject gameObject in existingButtons.Keys.OrderBy((GameObject x) => existingButtons[x].priority))
                {
                    Text leText = gameObject.transform.GetComponentInChildren<Text>();
                    if (butt_ix == 0) {
                        shMenuSection.TXT_Deploy = leText;
                    }
                    if (butt_ix == 1) {
                        shMenuSection.TXT_Leave = leText;
                    }
                    if (butt_ix == 2) {
                        shMenuSection.TXT_Extract = leText;
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
                if (SceneManager.GetActiveScene().name == config_safehouse_scene_name.Value && extracting_to_safehouse_need_map) {
                    GM.CurrentSceneSettings.DefaultVaultSceneConfig.vf = null;
                }
            }
			static void Postfix(FVRSceneSettings __instance)
            {
                if (__instance == null) {
                    Logger.LogMessage("FVRSceneSettings is null!?");
                }
                Logger.LogMessage("Trying to save in scene: " + SceneManager.GetActiveScene().name);
                if (SceneManager.GetActiveScene().name == config_safehouse_scene_name.Value && extracting_to_safehouse_need_map) {
                    GM.CurrentSceneSettings.DefaultVaultSceneConfig.vf = __instance.DefaultVaultSceneConfig.vf;
                    extracting_to_safehouse_need_map = false;
                    string error;
                    VaultFile again_vault_file = new VaultFile();
                    string this_safehouse_file_name = config_safehouse_scene_name.Value + "_" + safehouse_persistent_filename;
                    bool load_file_status = VaultSystem.LoadVaultFile(this_safehouse_file_name,
                                                                        VaultFileDisplayMode.SceneConfigs,
                                                                        out again_vault_file);
                    if (!load_file_status) {
                        Logger.LogMessage("WARNING: Didn't find Safehouse file to load :(");
                    }
                    Logger.LogMessage(again_vault_file);
                    bool spawn_objs_status = VaultSystem.SpawnObjects(VaultFileDisplayMode.SceneConfigs, 
                                                                        again_vault_file, out error,
                                                                        null, Vector3.up);
                    if (!spawn_objs_status) {
                        Logger.LogMessage("ERROR: Loading extracted safehouse scene file failed :(");
                        Logger.LogMessage(error);
                    }
                }
            }
		}

        [HarmonyPatch(typeof(FVRPlayerBody))]
		[HarmonyPatch("Init")]
		class FvrPlayerBodyInitHook
		{
			static void Postfix(FVRPlayerBody __instance, FVRSceneSettings SceneSettings)
            {
                Logger.LogMessage("FVRPlayerBody Init HOOKED!");
                if (__instance == null) {
                    Logger.LogMessage("FVRPlayerBody is null!?");
                }
                if (extracting_to_safehouse_need_loadout) {
                    extracting_to_safehouse_need_loadout = false;
                    Transform player_point = new GameObject().transform;
                    player_point.position = __instance.transform.position;
                    string error;
                    VaultFile again_vault_file = new VaultFile();
                    bool load_file_status = VaultSystem.LoadVaultFile(raid_extracted_loadout_filename,
                                                                        VaultFileDisplayMode.Loadouts,
                                                                        out again_vault_file);
                    bool spawn_objs_status = VaultSystem.SpawnObjects(VaultFileDisplayMode.Loadouts,
                                                                        again_vault_file, out error,
                                                                        player_point, Vector3.zero);
                    if (spawn_objs_status) {
                        // Cleans extracted loadout file.
                        VaultFile clean_vault_file = new VaultFile();
                        VaultSystem.SaveVaultFile(raid_extracted_loadout_filename,  
                        VaultFileDisplayMode.Loadouts, clean_vault_file);
                    }
                    else {
                        Logger.LogMessage("ERROR: Loading extracted loadout file failed :(");
                        Logger.LogMessage(error);
                    }
                }
                // T-ODO: Maybe add auto-deploy-loadout? I don't think so.
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

        private static void ExtractToSafehouse(object sender)
		{
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedExtractToSafehouse) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedExtractToSafehouse = true;
                myWM.TXT_Extract.text = "Confirm ??";
                return;
            }
            myWM.ResetConfirm();

            // Scan Loadout on person to extract.
            VaultFile loadout_vault_file = new VaultFile();
            bool scanned_quickbelt = VaultSystem.FindAndScanObjectsInQuickbelt(loadout_vault_file);
            if (scanned_quickbelt) {
                if (!VaultSystem.SaveVaultFile(raid_extracted_loadout_filename,  
                        VaultFileDisplayMode.Loadouts, loadout_vault_file)) {
                    Logger.LogMessage("ERROR: Extraction Vault file not saved :(");
                }
            }
			
            // For for going to Safehouse map.
            extracting_to_safehouse_need_loadout = true;
            extracting_to_safehouse_need_map = true;
            for (int i = 0; i < GM.CurrentSceneSettings.QuitReceivers.Count; i++)
			{
				GM.CurrentSceneSettings.QuitReceivers[i].BroadcastMessage("QUIT", SendMessageOptions.DontRequireReceiver);
			}
			if (GM.LoadingCallback.IsCompleted)
			{
				SteamVR_LoadLevel.Begin(config_safehouse_scene_name.Value, false, 0.5f, 0f, 0f, 0f, 1f);
			}
		}

        private static void LeaveTheSafehouse(object sender)
		{
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedLeaveTheSafehouse) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedLeaveTheSafehouse = true;
                myWM.TXT_Leave.text = "Confirm ??";
                return;
            }
            myWM.ResetConfirm();

            // Scan Loadout on person for Deployment.
            VaultFile loadout_vault_file = new VaultFile();
            bool scanned_quickbelt = VaultSystem.FindAndScanObjectsInQuickbelt(loadout_vault_file);
            if (scanned_quickbelt) {
                if (!VaultSystem.SaveVaultFile(safehouse_deployment_loadout_filename,  
                        VaultFileDisplayMode.Loadouts, loadout_vault_file)) {
                    Logger.LogMessage("ERROR: Deploy Vault file not saved :(");
                }
            } else {
                Logger.LogMessage("ERROR: Couldn't scan quickbelt to leave safehouse :(");
            }

            // Scan the Safehouse Level for persistance.
            VaultFile safehouse_vault_file = new VaultFile();
            // T-ODO: Scan respawn point?
            bool scanned_scene = VaultSystem.FindAndScanObjectsInScene(safehouse_vault_file);
            if (scanned_scene) {
                string this_safehouse_file_name = config_safehouse_scene_name.Value + "_" + safehouse_persistent_filename;
                if (!VaultSystem.SaveVaultFile(this_safehouse_file_name,  
                        VaultFileDisplayMode.SceneConfigs, safehouse_vault_file)) {
                    Logger.LogMessage("ERROR: Safehouse Scene Vault file not saved :(");
                }
            }
            // Generate a random number between 1 and 2
            int randomChance = UnityEngine.Random.Range(1, 3);
            Logger.LogMessage("num " + randomChance);
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
                    string this_safehouse_file_name = config_safehouse_scene_name.Value + "_" + safehouse_save_filename
                                                        + "_" + formattedDateTime;
                    if (!VaultSystem.SaveVaultFile(this_safehouse_file_name,  
                            VaultFileDisplayMode.SceneConfigs, secondVaultFile)) {
                        Logger.LogMessage("ERROR: Safehouse BACKUP Scene Vault file not saved :(");
                    }
                }
            }
            SteamVR_LoadLevel.Begin("MainMenu3", false, 0.5f, 0f, 0f, 0f, 1f);
        }

        private static void DeploySafehouseLoadout(object sender)
		{
            FVRWristMenuSection_Safehouse myWM = UnityEngine.Object.FindObjectOfType<FVRWristMenuSection_Safehouse>();
            if (!myWM.confirmedDeploySafehouseLoadout) { // if confirmed value isn't tre
                myWM.ResetConfirm();
                myWM.confirmedDeploySafehouseLoadout = true;
                myWM.TXT_Deploy.text = "Confirm ??";
                return;
            }
            myWM.ResetConfirm();

            FVRPlayerBody pb = UnityEngine.Object.FindObjectOfType<FVRPlayerBody>();
            Transform player_point = new GameObject().transform;
            player_point.position = pb.transform.position;
            string error;
            VaultFile again_vault_file = new VaultFile();
            bool load_file_status = VaultSystem.LoadVaultFile(safehouse_deployment_loadout_filename,
                                                                VaultFileDisplayMode.Loadouts,
                                                                out again_vault_file);
            bool spawn_objs_status = VaultSystem.SpawnObjects(VaultFileDisplayMode.Loadouts, again_vault_file, out error,
                    player_point, Vector3.zero);

            // Clear loadout after deploy.
            VaultFile clean_vault_file = new VaultFile();
            VaultSystem.SaveVaultFile(safehouse_deployment_loadout_filename,  
            VaultFileDisplayMode.Loadouts, clean_vault_file);
        }

        [HarmonyPatch(typeof(FVRViveHand))]
        [HarmonyPatch("Update")]
        class FVRViveHandUpdateHook
        {
            static void Postfix(FVRViveHand __instance) {
                if (__instance.Input.IsGrabDown) {
                    Logger.LogMessage("IsGrabDown");
                    if (__instance.CurrentInteractable == null) {
                        Logger.LogMessage("No current interactable");
                        if (__instance.ClosestPossibleInteractable == null) {
                            Logger.LogMessage("No closest interactable");
                            // (find other hand that isn't me)
                            GameObject _player = UnityEngine.Object.FindObjectOfType<FVRPlayerBody>().gameObject;
                            if (_player == null) {
                                Logger.LogMessage("Player null!!!!!!!!!!!!!!!!!!!!");
                                return;
                            }
                            FVRViveHand leftHand = _player.transform.Find("Controller (left)").GetComponent<FVRViveHand>();
                            FVRViveHand rightHand = _player.transform.Find("Controller (right)").GetComponent<FVRViveHand>();
                            FVRViveHand otherHand = null;
                            if (leftHand == __instance) {
                                Logger.LogMessage("Lefthand is __instance");
                                otherHand = rightHand;
                            }
                            if (rightHand == __instance) {
                                Logger.LogMessage("Righthand is __instance");
                                otherHand = leftHand;
                            }
                            if (otherHand == null) {
                                Logger.LogMessage("Neither hand matched!!!!!!!!!!!!!!!!!!");
                                return;
                            }
                            Logger.LogMessage("Chk1");
                            if (otherHand.CurrentInteractable != null)
                                Logger.LogMessage("Otherhand has CurrInteractible.");
                            else
                                return;

                            if (otherHand.CurrentInteractable is FVRFireArm)
                                Logger.LogMessage("Otherhand CurrInteractible is FVRPhysicalObject.");
                            else
                                return;
                            if (((FVRFireArm)otherHand.CurrentInteractable).Foregrip.GetComponent<FVRAlternateGrip>() != null)
                                Logger.LogMessage("CurrInteractible has AltGrip");
                            else
                                return;
                            // Begin interaction.
                            // Calc if distance is ok to start it.
                            Transform interactableTransform = ((FVRFireArm)otherHand.CurrentInteractable).Foregrip.GetComponent<Transform>();
                            // Calculate the distance between __instance and the interactable object's Foregrip
                            float distance = Vector3.Distance(__instance.transform.position, interactableTransform.position);
                            // Check if the distance is less than 15cm (0.15 meters)
                            if (distance < 0.10f)
                            {
                                __instance.CurrentInteractable = ((FVRFireArm)otherHand.CurrentInteractable).Foregrip.GetComponent<FVRAlternateGrip>();
                                __instance.m_state = FVRViveHand.HandState.GripInteracting;
                                __instance.CurrentInteractable.BeginInteraction(__instance);
                                __instance.Buzz(__instance.Buzzer.Buzz_BeginInteraction);
                            } else {
                                Logger.LogMessage("Too far!!");
                            }
                            
                        }
                        
                    }
                }
            }
        }
        
        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)
        internal new static ManualLogSource Logger { get; private set; }

    }

    public class FVRWristMenuSection_Safehouse : FVRWristMenuSection
	{
        
        public void ResetConfirm()
		{
			this.confirmedDeploySafehouseLoadout = false;
			this.confirmedLeaveTheSafehouse = false;
			this.confirmedExtractToSafehouse = false;
            this.TXT_Deploy.text = "Deployment-Loadout";
			this.TXT_Leave.text = "Leave-the-Safehouse";
			this.TXT_Extract.text = "Extract-to-Safehouse";
		}

        public override void OnHide()
		{
			this.ResetConfirm();
		}
        public Text TXT_Deploy;
        public Text TXT_Leave;
        public Text TXT_Extract;
        public bool confirmedDeploySafehouseLoadout = false;
        public bool confirmedLeaveTheSafehouse = false;
        public bool confirmedExtractToSafehouse = false;
    }

}
