using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using ErosionBrushPlugin;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.6FingerReload", "6FingerReload", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class SixFingerReload : BaseUnityPlugin
    {

        // Player-Configurable Vars.
        private static ConfigEntry<bool> config_enable;
        private static ConfigEntry<string> comma_forbids;
        public static ConfigEntry<float> magCompareThres;
        private static ConfigEntry<float> hoz_railUnlock_dist;
        private static ConfigEntry<float> ver_railUnlock_dist;
        private static ConfigEntry<float> mag_trigger_dist;
        private static ConfigEntry<float> magwellTriggerScale;
        //private static ConfigEntry<bool> test_bool_1;

        // Code vars.
        public static FVRFireArmMagazine detectedMag;
        public static float timeMagDetected;
        public static FVRFireArmMagazine acceptedRailedMag;

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

            Harmony harmony = new Harmony("NGA.6FingerReload");
            Logger.LogMessage("New NGA.6FingerReload harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            UpdateForbidden();
            Logger.LogMessage("Updated forbidden guns.");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.6FingerReload");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            config_enable = Config.Bind("Enabled",
                                         "ON/OFF",
                                         true,
                                         "Enable or disable the mod now.");
            comma_forbids = Config.Bind("Excluded Items",
                                         "List to Forbid",
                                         "",
                                         "Use toolbox object info tool to find name of magazine, Comma separated, add (Clone) postfix. Needs game restart. [Mod has builtin excluded guns not shown here.]");
            magCompareThres = Config.Bind("INTERNAL",
                                         "Compare Mag Thres",
                                         0.01f,
                                         "When to ignore Load magazine call.");
            hoz_railUnlock_dist = Config.Bind("INTERNAL",
                                         "Horizontal Dist cancel anim",
                                         0.15f,
                                         "(Meters) How far horizontally to cancel unbind mag from anim.");
            ver_railUnlock_dist = Config.Bind("INTERNAL",
                                         "Vertical Dist cancel anim",
                                         0.1f,
                                         "(Meters) How far vertically to cancel unbind mag from anim.");
            mag_trigger_dist = Config.Bind("INTERNAL",
                                         "Autoload dist",
                                         0.001f,
                                         "(Meters) How close mag has to be to its fully inserted positino to load");
            magwellTriggerScale = Config.Bind("INTERNAL",
                                         "Magwell Scale",
                                         0.8f,
                                         "Multiplier for how big trigger volume of guns magwell is");                                         
            // test_bool_1 = Config.Bind("Fun",
            //                              "Hand stays after",
            //                              false,
            //                              "The your hand isn't automatically kicked off after reloading.");
        }

        private void UpdateForbidden() {
            string[] nameArray = comma_forbids.Value.Split(',');
            foreach (string name in nameArray)
            {
                forbiddenNames.Add(name.Trim());
            }
            //Logger.LogMessage("Excluded guns for Mag Animations:");
            foreach (string name in forbiddenNames)
            {
                Logger.LogMessage(name);
            }
        }

        [HarmonyPatch(typeof(FVRFireArmReloadTriggerMag))]
		[HarmonyPatch("OnTriggerEnter")]
        public class FVRFireArmReloadTriggerMagOnTriggerEnter : MonoBehaviour
        {
            private static void Prefix(FVRFireArmReloadTriggerMag __instance, Collider collider)
            {
                if (!config_enable.Value) {
                    return;
                }
                if (__instance.Magazine != null && __instance.Magazine.FireArm == null 
                        && __instance.Magazine.QuickbeltSlot == null)
                {
                    if (collider.gameObject.tag == "FVRFireArmReloadTriggerWell" 
                            && __instance.gameObject.GetComponent<MagOnRailsTriggerVol>() != null)
                    {
                        // TODO: Ignore beltboxes etc.
                        detectedMag = __instance.Magazine;
                        timeMagDetected = Time.time;
                        Logger.LogWarning("Detected magwell " + collider.gameObject.name + " at " + timeMagDetected);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(FVRFireArmMagazine))]
		[HarmonyPatch("Load")]
        [HarmonyPatch(new[] { typeof(FVRFireArm) })]
        public class FVRFireArmMagazineLoad : MonoBehaviour
        {
            private static bool Prefix(FVRFireArmMagazine __instance, FVRFireArm fireArm)
            {
                if (!config_enable.Value) {
                    return true;
                }
                __instance.m_isVizLerping = false;
                __instance.UsesVizInterp = false;
                float timeNow = Time.time;
                if (__instance == detectedMag
                       && Mathf.Abs(timeMagDetected - timeNow) <= magCompareThres.Value) {
                    acceptedRailedMag = __instance;
                    detectedMag = null;
                    Logger.LogWarning("Ignored load and accepted mag for " + __instance.gameObject.name + " into " + fireArm.gameObject.name);
                    return false; // Skip Load execution and other prefixes after me.
                } else {
                    Logger.LogWarning("Doing load for " + __instance.gameObject.name + " into " + fireArm.gameObject.name
                                        + " comparing " + timeMagDetected + " - " +  timeNow + " = " + Mathf.Abs(timeMagDetected - timeNow)
                                        + " <= " + magCompareThres.Value);
                }
                return true; // Execute remaining prefixes and code.
            }
            private static void Postfix(FVRFireArmMagazine __instance, FVRFireArm fireArm)
            {
                if (!config_enable.Value) {
                    return;
                }
                __instance.m_isVizLerping = false;
                __instance.UsesVizInterp = false;
            }
        }

        [HarmonyPatch(typeof(FVRFireArmMagazine))]
		[HarmonyPatch("FVRFixedUpdate")]
		class FVRFireArmMagazineFVRFixedUpdate
		{
            static void Prefix(FVRFireArmMagazine __instance) 
            {
                if (!config_enable.Value) {
                    return;
                }
                __instance.m_isVizLerping = false;
                __instance.UsesVizInterp = false;
            }
            static void Postfix(FVRFireArmMagazine __instance) 
            {
                if (!config_enable.Value) {
                    return;
                }
                if (__instance.FireArm != null) {
                    FVRFireArm fvrfireArm = __instance.FireArm;
                    Transform hole = fvrfireArm.GetMagMountPos(__instance.IsBeltBox);
                    if (hole != null && fvrfireArm.Magazine == __instance) {
                        if (__instance.Viz != null) {
                            __instance.transform.rotation = hole.rotation;
                            __instance.transform.position = hole.position;
                            __instance.Viz.position = hole.position;
                            __instance.Viz.rotation = hole.rotation;
                        }
                    }
                }
                __instance.m_isVizLerping = false;
                __instance.UsesVizInterp = false;
            }
        }

        
        [HarmonyPatch(typeof(FVRFireArmMagazine))]
		[HarmonyPatch("Awake")]
        public class FVRFireArmMagazineAwake : MonoBehaviour
        {
            private static void Prefix(FVRFireArmMagazine __instance)
            {
                if (!config_enable.Value) {
                    return;
                }
                if (forbiddenNames.Contains(__instance.gameObject.name)) {
                    Logger.LogWarning("Skipped 6FingerReload on forbidden mag: " + __instance.gameObject.name);
                    return;
                }
                Transform trigMagChild = FindChildWithFVRFireArmReloadTriggerMag(__instance.gameObject);
                if (trigMagChild == null) {
                    Logger.LogError("No child in mag " + __instance.gameObject.name + " with FVRFireArmReloadTriggerMag");
                    return;
                }
                MagOnRailsTriggerVol railVol = trigMagChild.gameObject.AddComponent<MagOnRailsTriggerVol>();
                railVol.SelfMag = __instance;
                Logger.LogWarning("Added railVol to " + __instance.gameObject.name);
            }

        }
        
        public static Transform FindChildWithFVRFireArmReloadTriggerMag(GameObject parent)
        {
            // Iterate through all children of the parent GameObject
            foreach (Transform child in parent.transform)
            {
                // Check if the child has the desired component
                if (child.GetComponent<FVRFireArmReloadTriggerMag>() != null)
                {
                    // Return the Transform of the child if the component is found
                    return child;
                }
            }

            // Return null if no child with the component is found
            return null;
        }

        public class MagOnRailsTriggerVol : MonoBehaviour {
            
            // 1. Checks other is designated railed mag.
            // 2. Maintains rail positioning for viz.
            // 3. Calls Load() when viz's position matches.
            public void OnTriggerStay(Collider other) {
                if (this.SelfMag == null) {
                    return;
                }
                if (other.gameObject.tag != "FVRFireArmReloadTriggerWell"
                        || this.SelfMag != acceptedRailedMag)
				{
                    return;
                }
                isInTriggerWell = true;
                wellCompt = other.gameObject.GetComponent<FVRFireArmReloadTriggerWell>();
            }

            // 1. If other was designated rail, null var.
            public void OnTriggerExit(Collider other) {
                if (other.gameObject.tag == "FVRFireArmReloadTriggerWell"
                        && this.SelfMag == acceptedRailedMag)
				{
                    Logger.LogWarning("Mag exited TriggerWell " + SelfMag.gameObject.name);
                    isInTriggerWell = false;
                }
            }

            public void Update() {
                if (acceptedRailedMag == null || wellCompt == null) {
                    return;
                }
                FVRFireArm fvrfireArm = wellCompt.FireArm;
                Transform hole = fvrfireArm.GetMagMountPos(SelfMag.IsBeltBox);
                Vector3 localUp = hole.up;
                Vector3 toHole = SelfMag.transform.position - hole.position;
                float horizontalDist = Vector3.ProjectOnPlane(toHole, localUp).magnitude;
                float upDist = DistanceAlongUpAxis(SelfMag.Viz.transform, hole);
                // Unbinds mag from rail animation if it's not in trigger well or if it's real far.
                if (!isInTriggerWell) {
                    if (horizontalDist > hoz_railUnlock_dist.Value || upDist > ver_railUnlock_dist.Value) {
                        Logger.LogWarning("Disconnected mag too far away: upDist_" + upDist + " hozDist_" + horizontalDist);
                        wellCompt = null;
                        acceptedRailedMag = null;
                        SelfMag.Viz.position = SelfMag.transform.position;
                        SelfMag.Viz.rotation = SelfMag.transform.rotation;
                        SelfMag.SetAllCollidersToLayer(false, "Default");
						SelfMag.IsNonPhysForLoad = false;
                        Logger.LogWarning("Nulled acceptedRailedMag & reset vis pos");
                        return;
                    }
                }
                // Align mag with hole.
                SelfMag.IsNonPhysForLoad = true;
                SelfMag.SetAllCollidersToLayer(false, "NoCol");
                Vector3 alignedPosition = hole.position - localUp * upDist;
                SelfMag.Viz.position = alignedPosition;
                SelfMag.Viz.rotation = hole.rotation;
                // Met conditions for loading.
                if (upDist <= -mag_trigger_dist.Value) {
                    Logger.LogWarning("Met conditions for loading: upDist " + upDist + " of " + acceptedRailedMag.gameObject.name);
                    SelfMag.Viz.gameObject.SetActive(false);
                    SelfMag.Viz.localPosition = Vector3.zero;
                    SelfMag.Viz.localRotation = Quaternion.identity;
                    SelfMag.Load(fvrfireArm);
                    SelfMag.Viz.gameObject.SetActive(true);
                    SelfMag.SetAllCollidersToLayer(false, "Default");
                    SelfMag.IsNonPhysForLoad = false;
                    wellCompt = null;
                    acceptedRailedMag = null;
                    Logger.LogWarning("Nulled acceptedRailedMag");
                    return;
                }
            }

            static private float DistanceAlongUpAxis(Transform mag, Transform hole)
            {
                // Calculate the vector from the hole to the mag
                Vector3 toMag = hole.position - mag.position;
                Vector3 holeUp = hole.up;

                // Project the vector onto the hole's up direction
                float distanceAlongUp = Vector3.Dot(toMag, holeUp);
                return distanceAlongUp;
            }

            public bool isInTriggerWell = false;
            FVRFireArmReloadTriggerWell wellCompt = null;
            public FVRFireArmMagazine SelfMag;
        }

        [HarmonyPatch(typeof(FVRFireArm))]
		[HarmonyPatch("Awake")]
        public class FVRFireArmAwake : MonoBehaviour
        {
            private static void Prefix(FVRFireArm __instance)
            {
                if (!config_enable.Value) {
                    return;
                }
                
                // Leave if doesn't have magwell trigger or bullet exit point.
                Transform magWell = FindChildWithReloadTriggerWell(__instance.gameObject);
                if (magWell != null) {
                    magWell.localScale = magWell.localScale * magwellTriggerScale.Value;
                }
            }

            public static Transform FindChildWithReloadTriggerWell(GameObject parent)
            {
                // Iterate through all children of the parent GameObject
                foreach (Transform child in parent.transform)
                {
                    // Check if the child has the desired component
                    if (child.GetComponent<FVRFireArmReloadTriggerWell>() != null)
                    {
                        // Return the Transform of the child if the component is found
                        return child;
                    }
                }

                // Return null if no child with the component is found
                return null;
            }
        }
        
        internal new static ManualLogSource Logger { get; private set; }

    }
}
