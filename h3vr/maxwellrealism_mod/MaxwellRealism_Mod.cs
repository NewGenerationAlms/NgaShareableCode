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

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.MaxwellRealismMod", "MaxwellRealismMod", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInDependency("NGA.JsonFileIO", "0.0.0")]
    [BepInProcess("h3vr.exe")]
    public partial class MaxwellRealismMod : BaseUnityPlugin
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
        
        // Player-set variables.
		private static ConfigEntry<bool> _Feeding;
        private static ConfigEntry<int> _Feeding_Cycle_h;
        private static ConfigEntry<bool> _Cancer;
        private static ConfigEntry<int> _Insurance_Copay;

        // Save-file variables.
        private static JsonSaveSystem.ExtensibleDictionary myDic;
        private static string mod_folder_name = "NGA-MaxwellRealism_Mod";
        private static string save_file_name = "nga_maxwell_realism_mod.json";
        private static DateTime date_installed;
        private static int payment_balance;
        private static DateTime last_time_ate;
        private static float overall_size_scale;
        private static float wide_mult;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.MaxwellRealismMod");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.MaxwellRealismMod 0.0.1");
            InitializeSaveFile();
            ReadVarsFromSaveFile();
            Logger.LogMessage($"Save file ready and read");

            if (_Cancer.Value) {
                // Define the target date
                DateTime targetDate = date_installed.AddMonths(9);

                // Get the current date
                DateTime currentDate = DateTime.Now;

                // Calculate the difference in days
                TimeSpan difference = targetDate - currentDate;
                int daysUntilTarget = (int)difference.TotalDays;

                Logger.LogMessage("Days until Maxwell's bills are due (9 months after install): " + daysUntilTarget);
            }

        }

        /// <summary>
        /// Below are the save-file functions which handle reading and writing to one save-file inside
        /// our mod's folder, specific to Bepinex profile. These should be roughly standard across most mods.
        /// </summary>
        
        // ENSURES: A save file exists and myDic is up to date with its contents.
        private void InitializeSaveFile() {
            string myDicString = JsonSaveSystem.FileIO.ReadFile(mod_folder_name, save_file_name);
            if (myDicString == null) {
                myDicString = CreateDefaultSaveFile();
                JsonSaveSystem.FileIO.WriteToFile(mod_folder_name, save_file_name, myDicString);
            } else {
                myDic = JsonSaveSystem.ExtensibleDictionary.FromJson(myDicString);
            }
        }

        // ENSURES: myDic is populated.
        private static string CreateDefaultSaveFile() {
            myDic = new JsonSaveSystem.ExtensibleDictionary();
            myDic.AddEntry("overall_size_scale", ((float)0.3f).ToString());
            myDic.AddEntry("wide_mult", ((float)1f).ToString());
            myDic.AddEntry("last_time_ate", DateTime.MinValue.ToString("o"));
            myDic.AddEntry("date_installed", DateTime.Now.ToString("o"));
            myDic.AddEntry("payment_balance", ((int)0).ToString());
            return myDic.ToJson();
        }

        // REQUIRES: myDic already initialized/refreshed.
        // ENSURES: All key-value fields are read from the file and local variables
        // updated. 
        private static void ReadVarsFromSaveFile() {
            overall_size_scale = float.Parse(myDic.GetValue("overall_size_scale"));
            wide_mult = float.Parse(myDic.GetValue("wide_mult"));
            last_time_ate  = DateTime.Parse(myDic.GetValue("last_time_ate"));
            date_installed = DateTime.Parse(myDic.GetValue("date_installed"));
            payment_balance = int.Parse(myDic.GetValue("payment_balance"));
        }
        // REQUIRES: myDic already initialized.
        // ENSURES: Local variables values are written to myDic.
        private static void WriteVarsToMyDic()
        {
            // Make sure myDic is initialized
            if (myDic == null)
            {
                Logger.LogError("myDic is not initialized.");
                return;
            }

            // Write values to myDic with corresponding keys
            myDic.InsertOrUpdateEntry("overall_size_scale", overall_size_scale.ToString());
            myDic.InsertOrUpdateEntry("wide_mult", wide_mult.ToString());
            myDic.InsertOrUpdateEntry("last_time_ate", last_time_ate.ToString("o"));
            myDic.InsertOrUpdateEntry("date_installed", date_installed.ToString("o"));
            myDic.InsertOrUpdateEntry("payment_balance", payment_balance.ToString());
        }

        // Should be called after a save-file variable is updated locally.
        // ENSURES: Contents of myDic are written to save-file and synced with local variables.
        private static void RefreshSaveFile() {
            WriteVarsToMyDic();
            JsonSaveSystem.FileIO.WriteToFile(mod_folder_name, save_file_name, myDic.ToJson());
            myDic = JsonSaveSystem.ExtensibleDictionary.FromJson(
                                    JsonSaveSystem.FileIO.ReadFile(mod_folder_name, save_file_name));
            ReadVarsFromSaveFile();
        }

        // Assigns player-set variables to Sodalite.
        private void SetUpConfigFields()
        {
            // Tomigotchi.
            _Feeding = Config.Bind<bool>("Tomigotchi", "Feeding", true, 
            "Jess is just a kitten, you gotta feed her so she grows into a strong Maxwell :)");
            _Feeding_Cycle_h = Config.Bind<int>("Tomigotchi", "Feed every X hours", 48, 
            "The number of hours before Jess needs to be fed again. Overfeeding is not recommended.");

            // Hardcore.
            _Cancer = Config.Bind<bool>("Hardcore", "Cancer", false, 
            "[NOT YET IMPLEMENTED] Maxwell has cancer (like irl) and due to insufficient health insurance needs help with her bills.");
            _Insurance_Copay = Config.Bind<int>("Hardcore", "Insurance CoPay", 500, 
            "The amount in TnH tokens needed to pay for Maxwells cancer treatment. Collected on TnH completion.");
        }

        /// <summary>
        /// Below we find the methods from H3VR which we're patching our own code into (Prefix and Postfix).
        /// </summary> 
        /// 

        private static bool CheckTomigotchiSkip() {
            return !_Feeding.Value;
        }

        private static void UpdateJustAte(FVRPhysicalObject __instance) {
            TimeSpan timeSinceLastAte = DateTime.Now - last_time_ate;

            // Check if it's within the feeding cycle duration
            if (timeSinceLastAte.TotalHours <= _Feeding_Cycle_h.Value && wide_mult < 5f)
            {
                // Update wide_mult by adding 1
                Logger.LogMessage("Maxwell got a bit wider");
                wide_mult += 1f;
            }
            // Check if it's between the feeding cycle duration and twice the feeding cycle duration
            else if (timeSinceLastAte.TotalHours <= 2 * _Feeding_Cycle_h.Value)
            {
                // Update overall_size_scale by 0.1 if it's not over 1
                if (overall_size_scale < 1f)
                {
                    Logger.LogMessage("Maxwell grew by 0.1 :)");
                    overall_size_scale += 0.1f;
                    wide_mult = 1;
                }
            } else {
                Logger.LogWarning("You haven't feed Max in a while! Make sure to feed him after " 
                                    + _Feeding_Cycle_h.Value + " hours but before " + 2*_Feeding_Cycle_h.Value);
            }
            last_time_ate = DateTime.Now;
            ScaleObj(__instance);
        }

        private static void ScaleObj(FVRPhysicalObject __instance) {
            // Calculate the new scale values based on overall_size_scale and wide_mult
            float newX = overall_size_scale * wide_mult;
            float newY = overall_size_scale;
            float newZ = overall_size_scale;

            // Set the new scale
            __instance.transform.localScale = new Vector3(newX, newY, newZ);
        }

        [HarmonyPatch(typeof(FVRPhysicalObject))]
		[HarmonyPatch("FVRFixedUpdate")]
        public class FVRPhysicalObjectFVRFixedUpdate : MonoBehaviour
        {
            private static void Postfix(FVRPhysicalObject __instance)
            {
                if (CheckTomigotchiSkip()) return;
                if (__instance.gameObject.name != "Dingus(Clone)") {
                    return;
                }
                ScaleObj(__instance);
                Transform pattyNose = __instance.transform.Find("_Patty-Nose");
                if (pattyNose == null)
                {
                    Logger.LogWarning("No _Patty-Nose child object found.");
                    return;
                }
                // Check for collisions with objects around the _Patty-Nose position
                Vector3 center = pattyNose.position;
                Vector3 size = new Vector3(0.01f, 0.01f, 0.01f); // Adjust size as needed
                Collider[] colliders = Physics.OverlapBox(center, size);
                foreach (Collider collider in colliders)
                {
                    if (collider.gameObject.name == "BeefCan(Clone)")
                    {
                        Logger.LogMessage("You just ate: " + collider.gameObject.name);
                        UpdateJustAte(__instance);
                        RefreshSaveFile();
                        Logger.LogMessage("Vars saved.");
                        GameObject.Destroy(collider.gameObject);
                    }
                }
            }
        }
 
        private static bool CheckCancerSkip() {
            return !_Cancer.Value;
        }

        [HarmonyPatch(typeof(TNH_Manager))]
		[HarmonyPatch("SetPhase_Completed")]
        public class TNH_ManagerSetPhase_Completed : MonoBehaviour
        {
            private static void Postfix(TNH_Manager __instance)
            {
                if (CheckCancerSkip()) return;
                payment_balance += __instance.m_numTokens;
                RefreshSaveFile();
                Logger.LogMessage("Payment balanced added: " + __instance.m_numTokens 
                                    + " new total: " + payment_balance);
            }
        }

        internal new static ManualLogSource Logger { get; private set; }
    }
}
