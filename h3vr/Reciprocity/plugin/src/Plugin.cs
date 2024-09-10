// Code ideated and written by wpoTank. Ported & edited by NGA.

using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using Sodalite.ModPanel;
using System.Collections.Generic;
using static FistVR.ItemSpawnerV2;
using System.Linq;
using RootMotion.FinalIK;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("AGN.Sfdjqspdjuz", "Sfdjqspdjuz", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.0.0")]
    [BepInProcess("h3vr.exe")]
    public partial class Sfdjqspdjuz : BaseUnityPlugin
    {
        private float boltpos;
        private float boltposprev;
        private Vector3 velocity;
        //private float boltvelocity;
        //private Vector3 boltacpos;
        private Rigidbody obrigidbody;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("AGN.Sfdjqspdjuz");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from AGN.Sfdjqspdjuz 0.0.1");
        }


        private static bool IsModDisabled() {
            return !GameEnabled.Value;
        }

        /// <summary>
        /// FVRPhysicalObject patches.
        /// </summary>
        [HarmonyPatch(typeof(FVRPhysicalObject))]
		[HarmonyPatch("Awake")]
        public class FVRPhysicalObject_Awake : MonoBehaviour
        {
            private static void Postfix(FVRPhysicalObject __instance)
            {
                if (IsModDisabled()) return;
                if (!GripPhysicsEnabled.Value) return;
                __instance.GameObject.AddComponent<PhysicsObjectFeedback>().ForceMuilt = config_GripForce_Force.Value;
                __instance.GameObject.GetComponent<PhysicsObjectFeedback>().DampeningMuilt = config_GripForce_Dampening.Value;
                if(__instance.Size == FVRPhysicalObject.FVRPhysicalObjectSize.Small)
                {
                    __instance.GetComponent<Rigidbody>().mass = config_Small_Weight.Value;
                }
                if (__instance.Size == FVRPhysicalObject.FVRPhysicalObjectSize.Medium)
                {
                    __instance.GetComponent<Rigidbody>().mass = config_Medium_Weight.Value;
                }
                if (__instance.Size == FVRPhysicalObject.FVRPhysicalObjectSize.Large)
                {
                    __instance.GetComponent<Rigidbody>().mass = config_Large_Weight.Value;
                }
                if (__instance.Size == FVRPhysicalObject.FVRPhysicalObjectSize.Massive)
                {
                    __instance.GetComponent<Rigidbody>().mass = config_Massive_Weight.Value;
                }
                if (__instance.Size == FVRPhysicalObject.FVRPhysicalObjectSize.CantCarryBig)
                {
                    __instance.GetComponent<Rigidbody>().mass = config_CantCarryBig_Weight.Value;
                }
                return;
            }
        }

        [HarmonyPatch(typeof(FVRPhysicalObject))]
		[HarmonyPatch("FU")]
        public class FVRPhysicalObject_FU : MonoBehaviour
        {
            private static void Prefix(FVRPhysicalObject __instance)
            {
                if (IsModDisabled()) return;
                if (!GripPhysicsEnabled.Value) return;
                if (__instance.IsHeld && __instance.gameObject.GetComponent<PhysicsObjectFeedback>() != null)
                {
                    __instance.GameObject.GetComponent<PhysicsObjectFeedback>().ForceMuilt = config_GripForce_Force.Value;
                    __instance.GameObject.GetComponent<PhysicsObjectFeedback>().DampeningMuilt = config_GripForce_Dampening.Value;
                    __instance.gameObject.GetComponent<PhysicsObjectFeedback>().velocity = __instance.RootRigidbody.velocity;
                    __instance.gameObject.GetComponent<PhysicsObjectFeedback>().torque = __instance.RootRigidbody.angularVelocity;
                }
                var hgun = __instance.gameObject.GetComponent<Velocityhandlehandgun>();
                __instance.gameObject.GetComponent<PhysicsObjectFeedback>().latchGrabbed = hgun != null && hgun.handposgameobject != null;
                return;
            }
            private static void Postfix(FVRPhysicalObject __instance)
            {
                if (IsModDisabled()) return;
                if (!GripPhysicsEnabled.Value) return;
                if (__instance.IsHeld && __instance.gameObject.GetComponent<PhysicsObjectFeedback>() != null)
                {
                    var hgun = __instance.gameObject.GetComponent<Velocityhandlehandgun>();
                    if (hgun != null && hgun.handposgameobject != null) {
                        __instance.RootRigidbody.velocity = __instance.gameObject.GetComponent<PhysicsObjectFeedback>().velocity;
                    }
                    __instance.RootRigidbody.angularVelocity = __instance.gameObject.GetComponent<PhysicsObjectFeedback>().torque;
                }
                return;
            }
        }
        
        
        /// <summary>
        /// HANDGUN patches.
        /// </summary>
        [HarmonyPatch(typeof(HandgunSlide))]
		[HarmonyPatch("BeginInteraction")]
        public class HandgunSlide_BeginInteraction : MonoBehaviour
        {
            private static void Postfix(HandgunSlide __instance, FVRViveHand hand)
            {
                if (IsModDisabled()) return;
                if (!HandgunEnabled.Value) return;
                if (__instance.Handgun != null) {
                    __instance.Handgun.GetComponent<Velocityhandlehandgun>().grab();
                }
                return;
            }
        }
        
        [HarmonyPatch(typeof(Handgun))]
		[HarmonyPatch("Awake")]
        public class Handgun_Awake : MonoBehaviour
        {
            private static void Postfix(Handgun __instance)
            {
                if (IsModDisabled()) return;
                if (!HandgunEnabled.Value) return;
                var script = __instance.GameObject.AddComponent<Velocityhandlehandgun>();
                script.forcemuilt = config_Bolt_HandPosition_Force_Muiltiplyer.Value;
                script.dampingFactor = config_Bolt_HandPosition_Dampening.Value;
                return;
            }
        }

        [HarmonyPatch(typeof(Handgun))]
		[HarmonyPatch("FVRUpdate")]
        public class Handgun_FVRUpdate : MonoBehaviour
        {
            // TODO: Beware. These may be used in other methods at the same time? Nah Prob not.
            private static float boltvelocity= 0f;
            private static Vector3 boltacpos;
            private static void Prefix(Handgun __instance)
            {
                if (IsModDisabled()) return;
                if (!HandgunEnabled.Value) return;
                if (!__instance.IsHeld) return;
                if (!RecoilEnabled.Value && __instance.m_timeSinceFiredShot <= 0.1f) return;
                Velocityhandlehandgun velcomp = __instance.GetComponent<Velocityhandlehandgun>();
                if (velcomp != null) {
                    boltvelocity = velcomp.bolt_velocity;
                }
                return;
            }
            private static void Postfix(Handgun __instance)
            {
                if (IsModDisabled()) return;
                if (!HandgunEnabled.Value) return;
                if (!__instance.IsHeld) return;
                if (!RecoilEnabled.Value && __instance.m_timeSinceFiredShot <= 0.1f) return;

                boltacpos = __instance.Slide.transform.position;
                __instance.GetComponent<Rigidbody>().AddForceAtPosition(
                            __instance.transform.forward * boltvelocity * 3000 * config_handgun_force_multiplyer.Value,
                            boltacpos, ForceMode.Acceleration);

                // TODO: Possible location of end-rack bug
                if (__instance.Slide.m_slideZ_current == __instance.Slide.m_slideZ_rear)
                {
                    __instance.GetComponent<Rigidbody>().AddForceAtPosition(
                            __instance.transform.forward * boltvelocity * 150 * config_handgun_force_multiplyer_end.Value,
                            boltacpos, ForceMode.VelocityChange);
                }
                if (__instance.Slide.m_slideZ_current == __instance.Slide.m_slideZ_forward)
                {
                    __instance.GetComponent<Rigidbody>().AddForceAtPosition(
                            __instance.transform.forward * boltvelocity * 150 * config_handgun_force_multiplyer_end.Value,
                            boltacpos, ForceMode.VelocityChange);
                }
            }
        }

        internal new static ManualLogSource Logger { get; private set; }

        // Constants.

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            // Overall.
            GameEnabled = Config.Bind<bool>("Overall", "ON/OFF.", true, "Completely disable the mod.");
            RecoilEnabled = Config.Bind<bool>("Overall", "Recoil ON/OFF.", false, "Overall recoil forces from slide movement.");
            HandgunEnabled = Config.Bind<bool>("Overall", "Handgun ON/OFF.", true, "Forces applied to handguns.");
            RifleEnabled = Config.Bind<bool>("Overall", "Rifle ON/OFF.", false, "Forces applied to rifles.");
            OpenBoltEnabled = Config.Bind<bool>("Overall", "Open-bolt ON/OFF.", false, "Forces applied to Open-bolts");
            TubeFedEnabled = Config.Bind<bool>("Overall", "Tube-fed ON/OFF.", false, "Forces applied to Tube-feds");
            BoltActionEnabled = Config.Bind<bool>("Overall", "Bolt-action ON/OFF.", false, "Forces applied to Bolt actions");
            GripPhysicsEnabled = Config.Bind<bool>("Overall", "Grip Physics ON/OFF.", true, "Overall recoil forces from slide movement.");
            // Examples
            // FloatOne = Config.Bind<float>("Overall", "FloatOne", 0f, new ConfigDescription("Saturation level (-100 to 100)", new AcceptableValueFloatRangeStep(-100f, 100f, 2f)));
            // IntOne = Config.Bind("Overall", "IntOne", 1, new ConfigDescription("(1,3) step 1.", new AcceptableValueIntRangeStep(1, 3, 1)));
            // StringOne = Config.Bind("Overall",
            //                 "StringOne",
            //                 "DefaultValue",
            //                 "Description");
            // Handgun.
            config_handgun_force_multiplyer = Config.Bind<float>("Handgun",
                                            "force muiltiplyer for handguns",
                                            0.3f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_handgun_force_multiplyer_end = Config.Bind<float>("Handgun",
                                            "force muiltiplyer for handguns at the end of travel",
                                            0.2f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            // GripForce.
            config_GripForce_Force = Config.Bind<float>("GripForce",
                                            "Force muiltiplyer for grip force",
                                            1f,
                                            new ConfigDescription("how much force is added to gripped object", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_GripForce_Dampening = Config.Bind<float>("GripForce",
                                            "Dampening muiltiplyer for grip force",
                                            1f,
                                            new ConfigDescription("how much Dampening is added to gripped object", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            // Rifle.
            config_rifle_force_multiplyer = Config.Bind<float>("Rifle",
                                            "force muiltiplyer for rifles",
                                            1f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_rifle_force_multiplyer_end = Config.Bind<float>("Rifle",
                                            "force muiltiplyer for rifles at the end of travel",
                                            1f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            // Open-bolt.
            config_open_bolt_force_multiplyer = Config.Bind<float>("Open-bolt",
                                            "force muiltiplyer for open bolt guns",
                                            1f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_open_bolt_force_multiplyer_end = Config.Bind<float>("Open-bolt",
                                            "force muiltiplyer for open bolt guns at the end of travel",
                                            1f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            // Tube-fed.
            config_tube_fed_force_multiplyer = Config.Bind<float>("Tube-fed",
                                            "force muiltiplyer for tube fed guns",
                                            1f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_Tube_fed_force_multiplyer_end = Config.Bind<float>("Tube-fed",
                                            "force muiltiplyer for tube fed guns at the end of travel",
                                            1f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_tube_fed_pump_force_multiplyer = Config.Bind<float>("Tube-fed",
                                            "force muiltiplyer for tube fed guns",
                                            1f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_Tube_fed_pump_force_multiplyer_end = Config.Bind<float>("Tube-fed",
                                            "force muiltiplyer for tube fed guns at the end of travel when it has a pump",
                                            1f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity when it has a pump", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            // Object-Weight.
            config_Small_Weight = Config.Bind<float>("Object Weight",
                                            "Weight of small objects",
                                            1f,
                                            new ConfigDescription("The Weight of objects with a qb size of small", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.5f)));
            config_Medium_Weight = Config.Bind<float>("Object Weight",
                                            "Weight of Medium objects",
                                            1f,
                                            new ConfigDescription("The Weight of objects with a qb size of Medium", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.5f)));
            config_Large_Weight = Config.Bind<float>("Object Weight",
                                            "Weight of Large objects",
                                            1f,
                                            new ConfigDescription("The Weight of objects with a qb size of Large", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.5f)));
            config_Massive_Weight = Config.Bind<float>("Object Weight",
                                            "Weight of Massive objects",
                                            1f,
                                            new ConfigDescription("The Weight of objects with a qb size of Massive", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.5f)));
            config_CantCarryBig_Weight = Config.Bind<float>("Object Weight",
                                            "Weight of CantCarryBig objects",
                                            1f,
                                            new ConfigDescription("The Weight of objects with a qb size of CantCarryBig", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.5f)));
            // Bolt-action.
            config_Bolt_HandPosition_Dampening = Config.Bind<float>("Bolt-action",
                                            "Dampening muiltiplyer for slides",
                                            0.8f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for hand velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_bolt_action_force_multiplyer = Config.Bind<float>("Bolt-action",
                                            "force muiltiplyer for bolt actions",
                                            1f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_bolt_action_force_multiplyer_rot = Config.Bind<float>("Bolt-action",
                                            "force muiltiplyer for bolt actions rotation",
                                            1f,
                                            new ConfigDescription("how much force is added as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_Bolt_action_force_multiplyer_end_rot = Config.Bind<float>("Bolt-action",
                                            "force muiltiplyer for bolt actions at the end of travel",
                                            1f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_Bolt_action_force_multiplyer_end = Config.Bind<float>("Bolt-action",
                                            "force muiltiplyer for bolt actions at the end of travel for rotation",
                                            1f,
                                            new ConfigDescription("how much force is added when the bolt reachet the limit of movement as a muiltiplyer for bolt velocity", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));
            config_Bolt_HandPosition_Force_Muiltiplyer = Config.Bind<float>("Bolt-action",
                                            "force muiltiplyer hand position on bolts",
                                            1f,
                                            new ConfigDescription("how much force is added based on the hand position relative to bolt", 
                                            new AcceptableValueFloatRangeStep(0f, 100f, 0.1f)));

        }

        /// CONFIG ENTRIES
        // Overall Examples.
		private static ConfigEntry<bool> GameEnabled;
        private static ConfigEntry<bool> GripPhysicsEnabled;
        private static ConfigEntry<bool> RecoilEnabled;
        private static ConfigEntry<bool> HandgunEnabled;
        private static ConfigEntry<bool> RifleEnabled;
        private static ConfigEntry<bool> OpenBoltEnabled;
        private static ConfigEntry<bool> TubeFedEnabled;
        private static ConfigEntry<bool> BoltActionEnabled;
        // Examples.
        private static ConfigEntry<float> FloatOne;
        public static ConfigEntry<int> IntOne;
        public static ConfigEntry<string> StringOne;
        // Grip-Force.
        private static ConfigEntry<float> config_GripForce_Dampening;
        private static ConfigEntry<float> config_GripForce_Force;
        // Handgun.
        private static ConfigEntry<float> config_handgun_force_multiplyer;
        private static ConfigEntry<float> config_handgun_force_multiplyer_end;
        // Rifle.
        private static ConfigEntry<float> config_rifle_force_multiplyer;
        private static ConfigEntry<float> config_rifle_force_multiplyer_end;
        // Open-bolt.
        private static ConfigEntry<float> config_open_bolt_force_multiplyer;
        private static ConfigEntry<float> config_open_bolt_force_multiplyer_end;
        // Tube-fed.
        private static ConfigEntry<float> config_tube_fed_force_multiplyer;
        private static ConfigEntry<float> config_Tube_fed_force_multiplyer_end;
        private static ConfigEntry<float> config_tube_fed_pump_force_multiplyer;
        private static ConfigEntry<float> config_Tube_fed_pump_force_multiplyer_end;
        // Bolt-action.
        private static ConfigEntry<float> config_bolt_action_force_multiplyer;
        private static ConfigEntry<float> config_Bolt_action_force_multiplyer_end;
        private static ConfigEntry<float> config_bolt_action_force_multiplyer_rot;
        private static ConfigEntry<float> config_Bolt_action_force_multiplyer_end_rot;
        private static ConfigEntry<float> config_Bolt_HandPosition_Force_Muiltiplyer;
        private static ConfigEntry<float> config_Bolt_HandPosition_Dampening;
        // Weight.
        private static ConfigEntry<float> config_Small_Weight;
        private static ConfigEntry<float> config_Medium_Weight;
        private static ConfigEntry<float> config_Large_Weight;
        private static ConfigEntry<float> config_Massive_Weight;
        private static ConfigEntry<float> config_CantCarryBig_Weight;

    }
}
