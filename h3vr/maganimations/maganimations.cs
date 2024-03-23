using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections.Generic;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.MagAnimations", "MagAnimations", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class MagAnimations : BaseUnityPlugin
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
        private static ConfigEntry<bool> config_enable;
        private static ConfigEntry<float> config_mag_lock_dist;
        //private static ConfigEntry<float> config_mag_lock_hozdist;
        private static ConfigEntry<float> config_mag_auto_load_above;
        private static ConfigEntry<string> comma_forbids;

        private static HashSet<string> forbiddenNames = new HashSet<string>
        {
            "P90_Mag(Clone)",
            "LaserPistol_Magazine(Clone)",
            "HandCrankFrank_Magazine(Clone)",
            "Mk19_BeltBox(Clone)",
            "BerthierCarbine_Magazine(Clone)",
            "Carcano1891_Magazine(Clone)",
            "M2Tombstone_BeltBox(Clone)",
            "KWG_Magazine(Clone)",
            "SustenanceCrossbow_Battery(Clone)",
            "Minigun_Box(Clone)",
            "S9R Derringer_Mag(Clone)",
            "G11_Mag(Clone)"
        };

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.MagAnimations");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            UpdateForbidden();
            Logger.LogMessage("Updated forbidden guns.");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.MagAnimations 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            config_enable = Config.Bind("Magazine",
                                         "ON/OFF",
                                         true,
                                         "Enable or disable feature.");
            config_mag_lock_dist = Config.Bind("Magazine",
                                         "Vertical Distance to start mag insert animation",
                                         0.15f,
                                         "How far before mag starts insert animation in vertical dir");
            // config_mag_lock_hozdist = Config.Bind("Magazine",
            //                              "Horizontal Distance to start mag insert animation",
            //                              0.05f,
            //                              "How far before mag starts insert animation on horizontal dir");
            config_mag_auto_load_above = Config.Bind("Magazine",
                                         "Auto Load distance above entered",
                                         0.02f,
                                         "How far above mag entry point is needed to push to auto load.");
            comma_forbids = Config.Bind("Excluded Mags",
                                         "List to Forbid",
                                         "G11_Mag(Clone),Minigun_Box(Clone)",
                                         "Use toolbox object info tool to find name of magazine, Comma separated, add (Clone) postfix. Needs game restart.");
        }

        private void UpdateForbidden() {
            string[] nameArray = comma_forbids.Value.Split(',');
            foreach (string name in nameArray)
            {
                forbiddenNames.Add(name.Trim());
            }
            Logger.LogMessage("Excluded guns for Mag Animations:");
            foreach (string name in forbiddenNames)
            {
                Logger.LogMessage(name);
            }
        }

        
        [HarmonyPatch(typeof(FVRFireArmMagazine))]
		[HarmonyPatch("FVRFixedUpdate")]
		class FVRFireArmMagazineFVRFixedUpdate
		{
            
            static void Postfix(FVRFireArmMagazine __instance) 
            {
                if (!config_enable.Value) {
                    return;
                }
                if (__instance == null || !__instance.IsHeld || __instance.m_hand == null) {
                    return;
                }
                if (__instance.IsHeld &&  __instance.m_hand != null &&
                    __instance.m_hand.OtherHand != null &&
                    __instance.m_hand.OtherHand.CurrentInteractable != null 
                    && __instance.m_hand.OtherHand.CurrentInteractable is FVRFireArm)
                {
                    FVRFireArm fvrfireArm = __instance.m_hand.OtherHand.CurrentInteractable as FVRFireArm;
                    if (forbiddenNames.Contains(__instance.gameObject.name)) {
                        return;
                    }
                    Transform hole = fvrfireArm.GetMagMountPos(__instance.IsBeltBox);
                    if (hole == null) {
                        return;
                    }
                    bool isMagPalming = __instance.m_magChild != null || __instance.m_magParent != null;
                    if (fvrfireArm.Magazine == null  && fvrfireArm.MagazineType == __instance.MagazineType 
                        && fvrfireArm.GetMagMountPos(__instance.IsBeltBox) != null
                        && !isMagPalming)
                    {
                        // Force easy load.
						__instance.IsNonPhysForLoad = true;

                        // Do calcs.
                        Vector3 localUp = hole.up;
                        
                        Vector3 toHole = __instance.RoundEjectionPos.position - hole.position;
                        float horizontalDist = Vector3.ProjectOnPlane(toHole, localUp).magnitude;
                        float upDist = DistanceAlongUpAxis(__instance.transform, hole);
                        float dist = Vector3.Distance(__instance.RoundEjectionPos.position, hole.position);
                        if (dist <= config_mag_lock_dist.Value 
                            ) //&& horizontalDist <= config_mag_lock_hozdist.Value && upDist >= 0
                        {
                            // Calculate the aligned position below the hole
                            Vector3 alignedPosition = hole.position - localUp * upDist;
                            __instance.Viz.position = alignedPosition;
                            __instance.Viz.rotation = hole.rotation;

                            if (upDist < -config_mag_auto_load_above.Value) {
                                // Actual load.
                                __instance.Load(fvrfireArm);
                            }
                        }
                        else
                        {
                            // undo when not close
                            __instance.Viz.position = __instance.transform.position;
                            __instance.Viz.rotation = __instance.transform.rotation;
                            // Force easy load.
                            __instance.SetAllCollidersToLayer(false, "Default");
						    __instance.IsNonPhysForLoad = false;
                        }
                    } 
                }
                if (__instance.FireArm) {
                    FVRFireArm fvrfireArm = __instance.FireArm;
                    Transform hole = fvrfireArm.GetMagMountPos(__instance.IsBeltBox);
                    __instance.Viz.position = hole.position;
                    __instance.Viz.rotation = hole.rotation;
                    __instance.transform.position = hole.position;
                    __instance.transform.rotation = hole.rotation;
                }
            }
            static private float DistanceAlongUpAxis(Transform mag, Transform hole)
            {
                // Calculate the vector from the hole to the mag
                Vector3 toMag = hole.position - mag.position;

                // Get the hole's local up direction
                Vector3 holeUp = hole.up;

                // Project the vector onto the hole's up direction
                float distanceAlongUp = Vector3.Dot(toMag, holeUp);

                return distanceAlongUp;
            }
        }

        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)

        internal new static ManualLogSource Logger { get; private set; }

    }
}
