using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using Sodalite.ModPanel;
using System.Collections.Generic;
using static FistVR.ItemSpawnerV2;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.MasteryCamos", "MasteryCamos", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInDependency("NGA.JsonFileIO", "0.0.0")]
    [BepInProcess("h3vr.exe")]
    public partial class MasteryCamos : BaseUnityPlugin
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
        
        // Save-file variables.
        public static JsonSaveSystem.ExtensibleDictionary myDic;
        public static string mod_folder_name = "NGA-MasteryCamos";
        public static string mod_save_folder_name = "NGA-ProfileSaveFolder";
        public static string save_file_name = "nga_mastery_camos.json";

        // Save-state variables.
        public static QuestsState questsState;
        public static string flagDicCamoIdKey = "nga_mc_camoid";
        public static CamoPacks camoPacks;
        public static QuestsPool questsPool;
        // Game variables.
        public static int maxQuestsPerGun = 4;
        

        private void Awake()
        {
            Logger = base.Logger;
            Harmony harmony = new Harmony("NGA.MasteryCamos");

            SetUpConfigFields();
            InitializeCamos();
            FileStuff.InitializeSaveFile();
            FileStuff.ReadVarsFromSaveFile();
            InitializeQuests();
            Logger.LogMessage("Initialized :)");

            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.MasteryCamos 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields() {}

        public static void ApplyCamo(GameObject obj, MasteryCamosGunState gunstate) {
            ApplyCamoAsync.StartApplyCamo(obj, gunstate);
        }

        public class ApplyCamoAsync : MonoBehaviour
        {
            public static void StartApplyCamo(GameObject someGameObject, MasteryCamosGunState someGunState)
            {
                // Create a new GameObject to host the MonoBehaviour instance
                GameObject coroutineHost = new GameObject("CamoCoroutineHost");
                // Add the ApplyCamoAsync component to the new GameObject
                ApplyCamoAsync applyCamoAsync = coroutineHost.AddComponent<ApplyCamoAsync>();
                // Start the coroutine on the MonoBehaviour instance
                applyCamoAsync.StartCoroutine(applyCamoAsync.ApplyCamoCoroutine(someGameObject, someGunState));
            }
            public IEnumerator ApplyCamoCoroutine(GameObject someGameObject, MasteryCamosGunState someGunState)
            {
                string camoid = someGunState.camo_id;
                Texture ooo = GetTexture(camoid);
                if (ooo != null)
                {
                    ChangeAlbedo(someGameObject, ooo);
                    AddOrUpdateMCStateRecursively(someGameObject, someGunState);
                }
                else
                {
                    Logger.LogError("Error trying to load albedo null texture for " + camoid);
                }
                yield return null;
                // Destroy the host GameObject after the coroutine is finished
                Destroy(gameObject);
            }
        }


        [HarmonyPatch(typeof(TNH_Manager))]
		[HarmonyPatch("SetPhase_Completed")]
        public class TNH_ManagerSetPhase_Completed : MonoBehaviour
        {
            private static void Postfix(TNH_Manager __instance)
            {
                FVRObject fvrobject6 = IM.OD["NGA_PaintTub"];
                GameObject gameObject8 = UnityEngine.Object.Instantiate<GameObject>(fvrobject6.GetGameObject(), 
                                            __instance.FinalItemSpawnerPoint.position + Vector3.up + 0.5f*Vector3.forward, 
                                            __instance.FinalItemSpawnerPoint.rotation * Quaternion.Euler(0, 180, 0));
            }
        }
        [HarmonyPatch(typeof(TNH_Manager))]
		[HarmonyPatch("SetPhase_Dead")]
        public class TNH_ManagerSetPhase_Dead : MonoBehaviour
        {
            private static void Postfix(TNH_Manager __instance)
            {
                FVRObject fvrobject6 = IM.OD["NGA_PaintTub"];
                GameObject gameObject8 = UnityEngine.Object.Instantiate<GameObject>(fvrobject6.GetGameObject(), 
                                            __instance.FinalItemSpawnerPoint.position + Vector3.up + 0.5f*Vector3.forward, 
                                            __instance.FinalItemSpawnerPoint.rotation * Quaternion.Euler(0, 180, 0));
            }
        }
        [HarmonyPatch(typeof(TNH_SupplyPoint))]
		[HarmonyPatch("SpawnConstructor")]
        public class TNH_SupplyPointSpawnConstructor : MonoBehaviour
        {
            private static void Postfix(TNH_SupplyPoint __instance) {
                Transform placePoint = __instance.SpawnPoints_Panels[0];
                FVRObject fvrobject6 = IM.OD["NGA_PaintTub"];
                GameObject gameObject8 = UnityEngine.Object.Instantiate<GameObject>(fvrobject6.GetGameObject(), 
                                            placePoint.position + Vector3.up + 0.5f*Vector3.forward, 
                                            placePoint.rotation* Quaternion.Euler(0, 180, 0));
            }
        }

        [HarmonyPatch(typeof(FVRPhysicalObject))]
		[HarmonyPatch("GetFlagDic")]
        public class FVRPhysicalObjectGetFlagDic : MonoBehaviour
        {
            private static void Postfix(FVRPhysicalObject __instance, ref Dictionary<string, string> __result)
            {
                MasteryCamosGunState gun_state = GetMCState(__instance.gameObject);
                if (gun_state != null) {
                    string key = flagDicCamoIdKey;
                    string value = gun_state.camo_id;
                    __result.Add(key, value);
                } else {
                    Logger.LogWarning("MasteryCamosGunState is null when loading from GetFlagDic");
                }
            }
        }

        [HarmonyPatch(typeof(FVRPhysicalObject))]
		[HarmonyPatch("ConfigureFromFlagDic")]
        public class FVRPhysicalObjectConfigureFromFlagDic : MonoBehaviour
        {
            private static void Postfix(FVRPhysicalObject __instance, ref Dictionary<string, string> f)
            {
                MasteryCamosGunState gun_state = new MasteryCamosGunState();
                string key = flagDicCamoIdKey;
                if (f.ContainsKey(key))
                {
                    string camo_id = f[key];
                    gun_state.camo_id = camo_id;
                    ApplyCamo(__instance.gameObject, gun_state);
                }
            }
        }

        [HarmonyPatch(typeof(TNH_SupplyPoint))]
		[HarmonyPatch("ConfigureAtBeginning")]
        public class TNH_SupplyPointConfigureAtBeginning : MonoBehaviour
        {
            private static void Postfix(TNH_SupplyPoint __instance, ref TNH_CharacterDef c) {
                FVRObject fvrobject6 = IM.OD["NGA_PaintTub"];
                GameObject gameObject8 = UnityEngine.Object.Instantiate<GameObject>(fvrobject6.GetGameObject(), 
                                            __instance.SpawnPoints_SmallItem[0].position, __instance.SpawnPoints_SmallItem[0].rotation);
				__instance.M.AddObjectToTrackedList(gameObject8);
            }
        }

        [HarmonyPatch(typeof(FVRFireArmMagazine))]
		[HarmonyPatch("DuplicateFromSpawnLock")]
        public class FVRFireArmMagazineDuplicateFromSpawnLock : MonoBehaviour
        {
            private static void Postfix(FVRFireArmMagazine __instance, ref GameObject __result, ref FVRViveHand hand) {
                MasteryCamosGunState gunstate = GetMCState(__instance.gameObject);
                if (gunstate != null) {
                    ApplyCamo(__result, gunstate);
                }
            }
        }
        [HarmonyPatch(typeof(FVRFireArmRound))]
		[HarmonyPatch("DuplicateFromSpawnLock")]
        public class FVRFireArmRoundDuplicateFromSpawnLock : MonoBehaviour
        {
            private static void Postfix(FVRFireArmRound __instance, ref GameObject __result, ref FVRViveHand hand) {
                MasteryCamosGunState gunstate = GetMCState(__instance.gameObject);
                if (gunstate != null) {
                    ApplyCamo(__result, gunstate);
                }
            }
        }
        [HarmonyPatch(typeof(FVRFireArmClip))]
		[HarmonyPatch("DuplicateFromSpawnLock")]
        public class FVRFireArmClipDuplicateFromSpawnLock : MonoBehaviour
        {
            private static void Postfix(FVRFireArmClip __instance, ref GameObject __result, ref FVRViveHand hand) {
                MasteryCamosGunState gunstate = GetMCState(__instance.gameObject);
                if (gunstate != null) {
                    ApplyCamo(__result, gunstate);
                }
            }
        }
        [HarmonyPatch(typeof(FVRGrenade))]
		[HarmonyPatch("DuplicateFromSpawnLock")]
        public class FVRGrenadeDuplicateFromSpawnLock : MonoBehaviour
        {
            private static void Postfix(FVRGrenade __instance, ref GameObject __result, ref FVRViveHand hand) {
                MasteryCamosGunState gunstate = GetMCState(__instance.gameObject);
                if (gunstate != null) {
                    ApplyCamo(__result, gunstate);
                }
            }
        }
        [HarmonyPatch(typeof(PinnedGrenade))]
		[HarmonyPatch("DuplicateFromSpawnLock")]
        public class PinnedGrenadeDuplicateFromSpawnLock : MonoBehaviour
        {
            private static void Postfix(PinnedGrenade __instance, ref GameObject __result, ref FVRViveHand hand) {
                MasteryCamosGunState gunstate = GetMCState(__instance.gameObject);
                if (gunstate != null) {
                    ApplyCamo(__result, gunstate);
                }
            }
        }
        [HarmonyPatch(typeof(Speedloader))]
		[HarmonyPatch("DuplicateFromSpawnLock")]
        public class SpeedloaderDuplicateFromSpawnLock : MonoBehaviour
        {
            private static void Postfix(Speedloader __instance, ref GameObject __result, ref FVRViveHand hand) {
                MasteryCamosGunState gunstate = GetMCState(__instance.gameObject);
                if (gunstate != null) {
                    ApplyCamo(__result, gunstate);
                }
            }
        }

        [HarmonyPatch(typeof(TNH_Manager))]
		[HarmonyPatch("IncrementScoringStat")]
        public class TNH_ManagerIncrementScoringStat : MonoBehaviour
        {
            private static void Postfix(TNH_Manager __instance, ref TNH_Manager.ScoringEvent ev, int num)
            {
                // Get Current Gun held
                // TODO: Make this return a list instead.
                List<string> guns_in_qb = GetWeaponsOnBody();
                foreach (string gun_name in guns_in_qb) {
                    if (gun_name == null || gun_name == "") {
                        Logger.LogError("Empty gun name when updating score. Please hold weapon while scoring event occurs.");
                        return;
                    }
                    // Adds mastery challenges if gun has no challenges in save file.
                    bool updated_quest_state = false;
                    if (!questsState.questDictionary.ContainsKey(gun_name))
                    {
                        BindMasteryQuestsToGun(gun_name);
                        updated_quest_state = true;
                    }
                    // Updates all quests for this gun corresponding to scoring event.
                    if (questsState.questDictionary.ContainsKey(gun_name))
                    {
                        updated_quest_state = updated_quest_state || IncrementQuests(gun_name, (int)ev);
                    } else {
                        Logger.LogError("This shouldnt have happened, there's no quests even after adding mastery?");
                    }
                    // Writes quests state to file if something changed.
                    if (updated_quest_state) { 
                        FileStuff.RefreshSaveFile();
                    }
                }
            }
        }

        public static List<string> GetWeaponsOnBody()
		{
            List<string> gunsOnPerson = new List<string>();
			for (int i = 0; i < GM.CurrentMovementManager.Hands.Length; i++)
			{
				if (GM.CurrentMovementManager.Hands[i].CurrentInteractable != null 
                    && (GM.CurrentMovementManager.Hands[i].CurrentInteractable is FVRFireArm)
                    && !gunsOnPerson.Contains(GM.CurrentMovementManager.Hands[i].CurrentInteractable.gameObject.name))
				{
					gunsOnPerson.Add(GM.CurrentMovementManager.Hands[i].CurrentInteractable.gameObject.name);
                    //Logger.LogWarning("Added handheld item: " + GM.CurrentMovementManager.Hands[i].CurrentInteractable.gameObject.name);
				}
			}
            for (int i = 0; i < GM.CurrentPlayerBody.QBSlots_Internal.Count; i++) {
                if (GM.CurrentPlayerBody.QBSlots_Internal[i].CurObject != null)
				{
					FVRPhysicalObject curObject = GM.CurrentPlayerBody.QBSlots_Internal[i].CurObject;
					if (curObject != null && curObject.IsDeepBelted() && curObject is FVRFireArm
                        && !gunsOnPerson.Contains(curObject.gameObject.name))
					{
						gunsOnPerson.Add(curObject.gameObject.name);
                        //Logger.LogWarning("Added QB item: " + curObject.gameObject.name);
					}
				}
            }
			return gunsOnPerson;
		}

        public static bool IncrementQuests(string gun_name, int eventKey) {
            // TODO: Add compatibility with new quest fields for accepted events and mastery special. Do we even need mastery special, why not just root?
            bool updated_quest_state = false;
            List<Quest> guns_quests = questsState.questDictionary[gun_name];
            for (int i = 0; i < guns_quests.Count; i++) {
                Quest quest = guns_quests[i];
                if (quest.current_progress >= quest.target_goal) {
                    continue;
                }
                if (eventKey == quest.quest_type || quest.quest_type == -1) {
                    quest.current_progress = quest.current_progress + 1;
                } else if (quest.quest_type == 6 && (eventKey == 7 || eventKey == 8)) {
                    quest.current_progress = quest.current_progress + 1;
                }
                if (quest.current_progress >= quest.target_goal) {
                    if (quest.quest_type != -1) {
                        questsState.UnlockCamo(quest.camo_reward);
                    } else {
                        // TODO: Find better way to figure out mastery.
                        questsState.UnlockMasteryCamo(quest.gun_name, quest.camo_reward);
                    }
                }
                questsState.questDictionary[quest.gun_name][i] = quest;
                Logger.LogMessage("Challenge updated: "
                                    + eventKey + " "
                                    + quest.gun_name + " " 
                                    + quest.camo_reward + " " 
                                    + questsState.questDictionary[gun_name][i].current_progress + " type:" 
                                    + questsState.questDictionary[gun_name][i].quest_type + " descr:"
                                    + questsState.questDictionary[gun_name][i].description_key);
                updated_quest_state = true;
            }
            return updated_quest_state;
        }

        public static Texture GetTexture(string camo_file_name) {
            // Note: Camo file name is the same as camo name.
            string pngFilePath = camoPacks.GetCamoFile(camo_file_name);
            if (pngFilePath == "") {
                pngFilePath = Paths.PluginPath + "/" + mod_folder_name + "/" + camo_file_name + ".png";
            }
            //Logger.LogMessage("Loading texture path: " + pngFilePath);
            Texture texture = LoadPngTexture(pngFilePath);
            if (texture != null) {
                return texture;
            } else {
                Logger.LogError("Error loading texture path: " + pngFilePath);
                return null;
            }
        }

        public static Texture LoadPngTexture(string image_path)
        {
            // Check if the image path is specified
            if (!string.IsNullOrEmpty(image_path))
            {
                // Load the PNG file as a byte array
                byte[] file_data;
                try
                {
                    file_data = File.ReadAllBytes(image_path);
                }
                catch (IOException e)
                {
                    Logger.LogError("Error reading file: " + e.Message);
                    return null; // Exit the method if reading fails
                }

                // Create a new texture
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(file_data)) {
                    return texture;
                } else {
                    Logger.LogError("Failed to LoadImage");
                    return null;
                }
            }
            else
            {
                Logger.LogError("Image path is not specified.");
                return null;
            }
        }

        public static void AddOrUpdateMCStateRecursively(GameObject obj, MasteryCamosGunState state)
        {
            // TODO: Do it to the current person, Then call recursively on each child.
            if (obj == null) {
                Logger.LogError("AddOrUpdateMCStateRecursively had a null obj?");
                return;
            }
            FVRPhysicalObject fvrPhysObj = obj.GetComponent<FVRPhysicalObject>();
            if (fvrPhysObj != null)
            {
                MasteryCamosGunState mcStateComponent = obj.GetComponent<MasteryCamosGunState>();
                if (mcStateComponent != null)
                {
                    mcStateComponent.CopyState(state);
                }
                else
                {
                    mcStateComponent = obj.gameObject.AddComponent<MasteryCamosGunState>();
                    mcStateComponent.CopyState(state);
                }
            }
            foreach (Transform child in obj.transform)
            {
                AddOrUpdateMCStateRecursively(child.gameObject, state);
            }
        }

        public static MasteryCamosGunState GetMCState(GameObject parentObject)
        {
            return parentObject.GetComponent<MasteryCamosGunState>();
        }

        // Recursive, we don't expect deeply nexted children so it should be ok.
        public static void ChangeAlbedo(GameObject obj, Texture newAlbedoTexture) {
            // TODO: Add more checks for red dot components.
            if (obj.name.StartsWith("MuzzleSmoke") || obj.name.StartsWith("MuzzleFire") 
                || obj.name.StartsWith("VolumetricLight") || obj.name.StartsWith("Lens")
                || obj.name.StartsWith("Render")
                || obj.name.StartsWith("PSystem") || obj.GetComponent<HolographicSight>() != null
                || obj.GetComponent<ParticleSystem>() != null)
            {
                // Return from the method if the string starts with any of the specified prefixes.
                return;
            }
            // Get the renderer component attached to this GameObject
            Renderer rend = obj.GetComponent<Renderer>();
            // Check if the renderer component exists
            if (rend != null)
            {
                if (rend.materials.Length > 0) {
                    Material materialCopy = new Material(rend.materials[0]);
                    materialCopy.mainTexture = newAlbedoTexture;
                    rend.material = materialCopy;
                    //Logger.LogWarning("COLORED: " + obj.name);
                }
            }

            // Iterate over all children recursively
            foreach (Transform child in obj.transform)
            {
                // Call the function recursively on each child
                ChangeAlbedo(child.gameObject, newAlbedoTexture);
            }
        }

        public class CamoPacks
        {
            public List<string> CamoNames { get; private set; } = new List<string>();
            // camoname -> full file name
            public Dictionary<string, string> CamoNameToCamoFile { get; private set; } = 
                    new Dictionary<string, string>();
            public Dictionary<string, Dictionary<string, List<string>>> PackTypeCamos { get; private set; } = 
                    new Dictionary<string, Dictionary<string, List<string>>>();

            public void AddCamo(string pack, string type, string camoName, string fullFileName = "")
            {
                CamoNames.Add(camoName);
                CamoNameToCamoFile.Add(camoName, fullFileName);
                if (!PackTypeCamos.ContainsKey(pack))
                {
                    PackTypeCamos[pack] = new Dictionary<string, List<string>>();
                }
                if (!PackTypeCamos[pack].ContainsKey(type))
                {
                    PackTypeCamos[pack][type] = new List<string>();
                }
                PackTypeCamos[pack][type].Add(camoName);
            }
            public string GetCamoFile(string camoName){
                if (CamoNameToCamoFile.ContainsKey(camoName))
                {
                    return CamoNameToCamoFile[camoName];
                }
                return "";
            }
            public List<string> GetCamos(string pack, string type)
            {
                if (PackTypeCamos.ContainsKey(pack))
                {
                    if (PackTypeCamos[pack].ContainsKey(type)) {
                        return PackTypeCamos[pack][type];
                    } else {
                        Logger.LogError("Camo type name does not exist: " + type);
                    }
                } else {
                    Logger.LogError("Camo pack name does not exist: " + pack);
                }
                return new List<string>();
            }
        }

        public static void InitializeCamos() {
            questsPool = new QuestsPool();
            string pluginsPath = Paths.PluginPath;
            string[] subdirectories = Directory.GetDirectories(pluginsPath);
            camoPacks = new CamoPacks();
            foreach (string subdirectory in subdirectories)
            {
                // Get all .png files that start with "xx_" in the current subdirectory
                string[] pngFiles = Directory.GetFiles(subdirectory, "xx_*.png");

                foreach (string filePath in pngFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    // Remove the .png extension
                    string camoName = Path.GetFileNameWithoutExtension(fileName);
                    string[] parts = camoName.Split('_');
                    if (parts.Length >= 3)
                    {
                        string pack = parts[1];  // "pack-name"
                        string type = parts[2];  // "camo-type"
                        camoPacks.AddCamo(pack, type, camoName, filePath);
                    } else {
                        Logger.LogError("Camo name is malformatted: " + camoName);
                    }
                }
            }

            // Output the results (for demonstration purposes)
            Logger.LogWarning("Camo Names:");
            foreach (var name in camoPacks.CamoNames)
            {
                Logger.LogMessage(name);
            }

            Logger.LogWarning("\nPackTypeCamos:");
            foreach (var pack in camoPacks.PackTypeCamos)
            {
                foreach (var type in pack.Value)
                {
                    foreach (var camo in type.Value)
                    {
                        Logger.LogMessage(pack.Key + " " + type.Key + " " + camo);
                    }
                }
            }
        }
        public static void InitializeQuests() {
            foreach (string key_gun in questsState.questDictionary.Keys) {
                BindMasteryQuestsToGun(key_gun);
            }
            foreach (string freebieCamo in freebieCamos) {
                questsState.UnlockCamo(freebieCamo);
            }
        }

        public class FileStuff{
            /// <summary>
            /// Below are the save-file functions which handle reading and writing to one save-file inside
            /// our mod's folder, specific to Bepinex profile. These should be roughly standard across most mods.
            /// </summary>
            
            // ENSURES: A save file exists and myDic is up to date with its contents.
            public static void InitializeSaveFile() {
                string myDicString = JsonSaveSystem.FileIO.ReadFile(mod_save_folder_name, save_file_name);
                if (myDicString == null) {
                    myDicString = CreateDefaultSaveFile();
                    JsonSaveSystem.FileIO.WriteToFile(mod_save_folder_name, save_file_name, myDicString);
                } else {
                    myDic = JsonSaveSystem.ExtensibleDictionary.FromJson(myDicString);
                }
            }

            // ENSURES: myDic is populated.
            public static string CreateDefaultSaveFile() {
                myDic = new JsonSaveSystem.ExtensibleDictionary();
                questsState = new QuestsState();
                questsState.InitNow();
                string questsStateJson = JsonConvert.SerializeObject(questsState, Formatting.Indented);
                myDic.AddEntry("questsState", questsStateJson);
                return myDic.ToJson();
            }

            // REQUIRES: myDic already initialized/refreshed.
            // ENSURES: All key-value fields are read from the file and local variables
            // updated. 
            public static void ReadVarsFromSaveFile() {
                myDic = JsonSaveSystem.ExtensibleDictionary.FromJson(
                                        JsonSaveSystem.FileIO.ReadFile(mod_save_folder_name, save_file_name));
                // Logger.LogWarning("...Reading from save file..." + myDic.GetValue("questsState"));
                questsState = JsonConvert.DeserializeObject<QuestsState>
                                            (myDic.GetValue("questsState"));
            }
            // REQUIRES: myDic already initialized.
            // ENSURES: Local variables values are written to myDic.
            public static void WriteVarsToMyDic()
            {
                // Make sure myDic is initialized
                if (myDic == null)
                {
                    Logger.LogError("myDic is not initialized.");
                    return;
                }

                // Write values to myDic with corresponding keys
                string questsStateJson = JsonConvert.SerializeObject(questsState, Formatting.Indented);
                myDic.InsertOrUpdateEntry("questsState", questsStateJson);
                // Logger.LogWarning("...Writing to save file..." + myDic.GetValue("questsState"));
            }

            // Should be called after a save-file variable is updated locally.
            // ENSURES: Contents of myDic are written to save-file and synced with local variables.
            public static void RefreshSaveFile() {
                WriteVarsToMyDic();
                JsonSaveSystem.FileIO.WriteToFile(mod_save_folder_name, save_file_name, myDic.ToJson());
                ReadVarsFromSaveFile();
            }
        }

        [Serializable]
        public struct Quest
        {
            public string gun_name;       // Name of the gun
            public int quest_type;        // Type of the quest. -1 means "all"
            public string camo_reward;    // Camo reward for completing the quest
            public int target_goal;       // Target goal to complete the quest
            public int current_progress;  // Current progress in the quest
            public string description_key;// The key that references the quests' instructions
            public Quest(string descriptionKey = "", int questType = -1, int targetGoal = 0, string gunName = "",  string camoReward = "", int currentProgress = 0)
            {
                gun_name = gunName;
                quest_type = questType;
                camo_reward = camoReward;
                target_goal = targetGoal;
                current_progress = currentProgress;
                description_key = descriptionKey;
            }
        }

        public class QuestsPool
        {
            // Quests that apply to all camos, unless they have specific quests in QuestDictionary.
            public static List<Quest> DefaultQuests { get; private set; } = new List<Quest>();
            // Overrides default quests, makes these quests only available for camo unlock.
            public static Dictionary<string, List<Quest>> QuestDictionary { get; private set; } = new Dictionary<string, List<Quest>>();
            // Mastery quests, reserved for mastery camos. New types every season.
            public static List<Quest> AllMasteryQuests { get; private set; } = new List<Quest>();

            public QuestsPool()
            {
                AllMasteryQuests.AddRange(CreateEmptyS1MasteryQuests());
                DefaultQuests.AddRange(CreateDefaultQuests());
                CreateMelonSpecialQuests();
            }
            
            public static void AddQuestToDic(string key, Quest quest) {
                if (!QuestDictionary.ContainsKey(key)) {
                    QuestDictionary.Add(key, new List<Quest>{quest});
                } else {
                    QuestDictionary[key].Add(quest);
                }
            }
            public static List<Quest> GetCamoQuests(string camoName)
            {
                string pack, type, cleanName;
                ExtractPackAndType(camoName, out pack, out type, out cleanName);

                // Check for the most specific match first, then walk back.
                if (QuestDictionary.ContainsKey(camoName))
                {
                    return QuestDictionary[camoName];
                }
                if (QuestDictionary.ContainsKey(pack + "_" + type))
                {
                    return QuestDictionary[pack + "_" + type];
                }
                if (QuestDictionary.ContainsKey(pack))
                {
                    return QuestDictionary[pack];
                }

                // Return default quests if no specific match is found
                return DefaultQuests;
            }

            public static string GetQuestDescription(string description_key) {
                if (description_key == null) {
                    return "ERROR: Could not find description. Null key.";
                }
                if (!QuestDescriptionsDic.ContainsKey(description_key)) {
                    return "ERROR: Could not find description under given key.";
                }
                string description_final = QuestDescriptionsDic[description_key] + "\n" + whileHolding;
                return description_final;
            }
            
            public static List<Quest> CreateEmptyS1MasteryQuests() {
                List<Quest> lll = new List<Quest>();
                Quest _gold_mastery_quest = new Quest()
                {
                    gun_name = "", quest_type = -1,
                    camo_reward = "xx_mastery_s1_gold-scuffed",
                    target_goal = 100, current_progress = 0,
                    description_key = "challengeXP"
                };
                AddQuestToDic("xx_mastery_s1_gold-scuffed", _gold_mastery_quest);
                Quest _alien_carn_mastery_quest = new Quest()
                {
                    gun_name = "", target_goal = 1000, current_progress = 0,
                    camo_reward = "xx_mastery_s1_alien-carniverous",
                    quest_type = -1, description_key = "challengeXP"
                };
                AddQuestToDic("xx_mastery_s1_alien-carniverous", _gold_mastery_quest);
                lll.Add(_gold_mastery_quest);
                lll.Add(_alien_carn_mastery_quest);
                return lll;
            }
            public static List<Quest> CreateMelonSpecialQuests() {
                List<Quest> lll = new List<Quest>();
                Quest melonPat = new Quest()
                {
                    gun_name = "", quest_type = 3,
                    camo_reward = "xx_MELON_special_MelonStripe",
                    target_goal = 5, current_progress = 0,
                    description_key = "holdPhaseNoDamage"
                };
                AddQuestToDic("xx_MELON_special_MelonStripe", melonPat);
                lll.Add(melonPat);
                Quest melonPatDark = new Quest()
                {
                    gun_name = "", quest_type = 3,
                    camo_reward = "xx_MELON_special_MelonStripeDARK",
                    target_goal = 5, current_progress = 0,
                    description_key = "holdPhaseNoDamage"
                };
                AddQuestToDic("xx_MELON_special_MelonStripeDARK", melonPatDark);
                lll.Add(melonPatDark);
                Quest honeyStripe = new Quest()
                {
                    gun_name = "", quest_type = 13,
                    camo_reward = "xx_MELON_special_HoneyStripe",
                    target_goal = 500, current_progress = 0,
                    description_key = "takeKillGuardUnaware"
                };
                AddQuestToDic("xx_MELON_special_HoneyStripe", honeyStripe);
                lll.Add(honeyStripe);
                Quest canPat = new Quest()
                {
                    gun_name = "", quest_type = 10,
                    camo_reward = "xx_MELON_special_CanPat",
                    target_goal = 15, current_progress = 0,
                    description_key = "holdKillStreakBonus"
                };
                AddQuestToDic("xx_MELON_special_CanPat", canPat);
                lll.Add(canPat);
                Quest canPatDark = new Quest()
                {
                    gun_name = "", quest_type = 10,
                    camo_reward = "xx_MELON_special_CanPatDARK",
                    target_goal = 15, current_progress = 0,
                    description_key = "holdKillStreakBonus"
                };
                AddQuestToDic("xx_MELON_special_CanPatDARK", canPatDark);
                lll.Add(canPatDark);
                return lll;
            }
            public static List<Quest> CreateDefaultQuests() {
                List<Quest> lll = new List<Quest>();
                lll.Add(new Quest(/*description_key*/"completeHoldPhase", /*quest_type*/0, /*target_goal*/3));
                //lll.Add(new Quest(/*description_key*/"holdSecsRemaining", /*quest_type*/1, /*target_goal*/1000)); // divided by 10=secs
                lll.Add(new Quest(/*description_key*/"holdWaveNoDamage", /*quest_type*/4, /*target_goal*/2));
                lll.Add(new Quest(/*description_key*/"holdPhaseNoDamage", /*quest_type*/3, /*target_goal*/1));
                lll.Add(new Quest(/*description_key*/"holdKill", /*quest_type*/4, /*target_goal*/60));
                lll.Add(new Quest(/*description_key*/"holdHeadshotKill", /*quest_type*/5, /*target_goal*/30));
                //lll.Add(new Quest(/*description_key*/"holdMeleeKill", /*quest_type*/6, /*target_goal*/7));
                lll.Add(new Quest(/*description_key*/"holdKillDistanceBonus", /*quest_type*/9, /*target_goal*/4));
                lll.Add(new Quest(/*description_key*/"holdKillStreakBonus", /*quest_type*/10, /*target_goal*/10));
                lll.Add(new Quest(/*description_key*/"takeCompleteNoAlert", /*quest_type*/12, /*target_goal*/3));
                lll.Add(new Quest(/*description_key*/"takeKillGuardUnaware", /*quest_type*/13, /*target_goal*/5));
                return lll;
            }
        }
        
        public static void ExtractPackAndType(string camoName, out string pack, out string type, out string cleanName)
        {
            string[] parts = camoName.Split('_');
            if (parts.Length >= 4)
            {
                pack = parts[1];
                type = parts[2];
                cleanName = parts[3];
            }
            else
            {
                pack = string.Empty;
                type = string.Empty;
                cleanName = string.Empty;
            }
        }

        public class QuestsState
        {

            // Gun names -> List of gun's active quests
            [SerializeField]
            public Dictionary<string, List<Quest>> questDictionary;

            // List of unlocked camos
            [SerializeField]
            public List<string> unlockedCamos;

            // Gun name -> List of unlocked camo names
            [SerializeField]
            public Dictionary<string, List<string>> masteryCamosUnlocked;

            // Initialize the data structures
            void Start()
            { InitNow();}
            public void InitNow()
            {
                questDictionary = new Dictionary<string, List<Quest>>();
                unlockedCamos = new List<string>();
                masteryCamosUnlocked = new Dictionary<string, List<string>>();
            }
            public void AddQuest(Quest quest)
            {
                if (!questDictionary.ContainsKey(quest.gun_name)) {
                    questDictionary.Add(quest.gun_name, new List<Quest>{quest});
                } else {
                    questDictionary[quest.gun_name].Add(quest);
                }
            }
            public void RemoveQuest(Quest quest)
            {
                if (questDictionary.ContainsKey(quest.gun_name)) {
                    questDictionary[quest.gun_name].Remove(quest);
                } else {
                    Logger.LogWarning("Not supposed to happen: Someone asked me to unbind a quest from a gun that's not being quest-tracked.");
                }
            }
            public void UnlockCamo(string camo)
            {
                if (!unlockedCamos.Contains(camo))
                {
                    unlockedCamos.Add(camo);
                }
            }

            public List<Quest> GetQuestsByRewardCamo(string rewardCamo)
            {
                List<Quest> questsWithRewardCamo = new List<Quest>();
                foreach (var gunQuests in questDictionary.Values)
                {
                    foreach (var quest in gunQuests)
                    {
                        if (quest.camo_reward == rewardCamo)
                        {
                            questsWithRewardCamo.Add(quest);
                        }
                    }
                }
                return questsWithRewardCamo;
            }
            public void UnlockMasteryCamo(string gunName, string camo)
            {
                if (!masteryCamosUnlocked.ContainsKey(gunName))
                {
                    masteryCamosUnlocked[gunName] = new List<string>();
                }
                if (!masteryCamosUnlocked[gunName].Contains(camo))
                {
                    masteryCamosUnlocked[gunName].Add(camo);
                }
            }
            public List<string> GetMasteryCamosForGun(string gunName)
            {
                if (masteryCamosUnlocked.TryGetValue(gunName, out List<string> camos))
                {
                    return camos;
                }
                else
                {
                    return new List<string>(); // return an empty list if the gun name is not found
                }
            }
            public List<Quest> GetActiveCamosForGun(string gunName)
            {
                if (questDictionary.ContainsKey(gunName))
                {
                    return questDictionary[gunName];
                }
                else
                {
                    return new List<Quest>(); // return an empty list if the gun name is not found
                }
            }
        }

        public static void BindMasteryQuestsToGun(string d_gun_name) {
            List<Quest> curr_qs = questsState.GetActiveCamosForGun(d_gun_name);
            List<Quest> mastery_quests = QuestsPool.AllMasteryQuests;
            for (int i = 0; i < mastery_quests.Count; i++) {
                Quest mast_quest = mastery_quests[i];
                bool found_this_mastery = false;
                for (int j = 0; j < curr_qs.Count; j++) {
                    if (curr_qs[j].camo_reward == mast_quest.camo_reward) {
                        found_this_mastery = true;
                        break;
                    }
                }
                if (!found_this_mastery) {
                    mast_quest.gun_name = d_gun_name;
                    questsState.AddQuest(mast_quest);
                }
            }
        }
        public static int CountChallengesForGun(string gunName)
        {
            // Check if the quest dictionary contains the specified gun name
            if (!questsState.questDictionary.ContainsKey(gunName))
            {
                return 0;
            }
            List<Quest> questsForGun = questsState.questDictionary[gunName];
            int challengeCount = 0;
            foreach (Quest quest in questsForGun)
            {
                if (!QuestsPool.AllMasteryQuests.Exists(masteryQuest => masteryQuest.camo_reward == quest.camo_reward))
                {
                    challengeCount++;
                }
            }
            return challengeCount;
        }

        // Constants.
        const string whileHolding = "Gun binded to this challenge must be held or in Quickbelt while event happens.";
        const string masteryExclusive = "Unlocks only for this gun.";
        const string completeHoldPhase = "Complete Hold Points in TnH.";
        const string holdSecsRemaining = "You get 10 points for every second saved if you finish a TnH Hold early.";
        const string holdWaveNoDamage = "Receive no damage during waves inside a Hold point in TnH.";
        const string holdPhaseNoDamage = "Receive no damage during entire Hold point in TnH.";
        const string holdKill = "Kill sosigs inside a Hold in TnH."; // TODO: Add kills during hold.
        const string holdHeadshotKill = "Kill sosigs with headshots inside a Hold in TnH.";
        const string holdMeleeKill = "Kill sosigs using Melee (not necessarily using gun) inside a Hold in TnH.";
        const string holdKillDistanceBonus = "Get 1 point for every 25 meters segment between you and your target killed in a Hold in TnH.";
        const string holdKillStreakBonus = "Get 1 point for every kill in your kill-streak. Example: Triple kill yields 3 points.";
        const string takeCompleteNoAlert = "Stealthily take a hold point. That is, press the blue orb in TnH without setting off alert.";
        const string takeKillGuardUnaware = "Kill unaware guards at a hold point during take phase in TnH.";
        const string challengeXP = "XP CHALLENGE: You get points for doing literally almost anything in TnH. Kill a sosig, 1 point, was it a headshot, 1 point, so on.\nMastery camos need to be unlocked for each gun.";
        static List<string> freebieCamos = new List<string>{"xx_MW19_digital_Artic", "xx_MW19_dragon_Asphalt", "xx_MW19_reptile_Anaconda",
                                                    "xx_MW19_skulls_CorpseDigger", "xx_MW19_splinter_Angles", "xx_MW19_spray_ChainLink",
                                                    "xx_MW19_stripes_Africa", "xx_MW19_tiger_Abominable", "xx_MW19_topo_Barren",
                                                    "xx_MW19_woodland_Canopy"};
        public static Dictionary<string, string> QuestDescriptionsDic = new Dictionary<string, string>
        {
            { "completeHoldPhase", completeHoldPhase },
            { "holdSecsRemaining", holdSecsRemaining },
            { "holdWaveNoDamage", holdWaveNoDamage },
            { "holdPhaseNoDamage", holdPhaseNoDamage },
            { "holdKill", holdKill },
            { "holdHeadshotKill", holdHeadshotKill },
            { "holdMeleeKill", holdMeleeKill },
            { "holdKillDistanceBonus", holdKillDistanceBonus },
            { "holdKillStreakBonus", holdKillStreakBonus },
            { "takeCompleteNoAlert", takeCompleteNoAlert },
            { "takeKillGuardUnaware", takeKillGuardUnaware },
            { "challengeXP", challengeXP }
        };
        
        internal new static ManualLogSource Logger { get; private set; }
    }

    public class MasteryCamosGunState : MonoBehaviour
    {
        public string camo_id;
        // Method to copy the state of another MasteryCamosGunState object
        public void CopyState(MasteryCamosGunState state)
        {
            // Copy the state properties from the provided state object
            this.camo_id = state.camo_id;
        }
    }
}
