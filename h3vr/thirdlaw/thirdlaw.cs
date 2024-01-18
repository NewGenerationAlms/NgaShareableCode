using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.ThirdLaw", "ThirdLaw", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class ThirdLaw : BaseUnityPlugin
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
        private static ConfigEntry<bool> config_enable_mag_stuff;
        private static ConfigEntry<bool> config_enable_pistol_slide_stuff;
        private static ConfigEntry<bool> config_enable_big_slide_stuff;
        private static ConfigEntry<float> config_force_mag_insert;
        private static ConfigEntry<float> config_torque_mag_insert;
        private static ConfigEntry<float> config_force_mag_eject;
        private static ConfigEntry<float> config_torque_mag_eject;
        private static ConfigEntry<float> config_pistol_torque_slide_fore;
        private static ConfigEntry<float> config_pistol_torque_slide_rear;
        private static ConfigEntry<float> config_big_torque_slide_fore;
        private static ConfigEntry<float> config_big_torque_slide_rear;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.ThirdLaw");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.ThirdLaw 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            config_enable_mag_stuff = Config.Bind("Overall",
                                         "Enable Magazine Reactions",
                                         true,
                                         "Whether mag insertion/release causes gun movements.");
            config_enable_pistol_slide_stuff = Config.Bind("Overall",
                                         "Enable Pistol Slide Reactions",
                                         true,
                                         "Whether pistol slide manipulation causes gun movements.");
            config_enable_big_slide_stuff = Config.Bind("Overall",
                                         "Enable Rifles+ Reactions",
                                         true,
                                         "Whether bolt handle manipulation causes gun movements.");
            config_force_mag_insert = Config.Bind("Magazine",
                                         "Force Mag Insert Up",
                                         10f,
                                         "How strong an upward force is put on mag INSERT.");
            config_torque_mag_insert = Config.Bind("Magazine",
                                         "Force Mag Insert Torque",
                                         1f,
                                         "How strong an forward-axis rotation force is put on mag INSERT.");
            config_force_mag_eject = Config.Bind("Magazine",
                                         "Force Mag Eject Up",
                                         10f,
                                         "How strong an upward force is put on mag EJECT.");
            config_torque_mag_eject = Config.Bind("Magazine",
                                         "Torque Mag Eject Sideways",
                                         2.5f,
                                         "How strong an forward-axis rotation force is put on mag EJECT.");
            config_pistol_torque_slide_fore = Config.Bind("Handles",
                                         "Pistol Torque Slide Reach Fore",
                                         1f,
                                         "How strong an trigger-axis rotation force when slide reaches fore.");
            config_pistol_torque_slide_rear = Config.Bind("Handles",
                                         "Pistol Torque Slide Reach Back",
                                         1f,
                                         "How strong an trigger-axis rotation force when slide reaches rear.");
            config_big_torque_slide_fore = Config.Bind("Handles",
                                         "Rifles+ Torque Bolt Reach Fore",
                                         20f,
                                         "How strong an trigger-axis rotation force when bolt handle reaches fore.");
            config_big_torque_slide_rear = Config.Bind("Handles",
                                         "Rifles+ Torque Bolt Reach Back",
                                         10f,
                                         "How strong an trigger-axis rotation force when bolt handle reaches rear.");
        }


        [HarmonyPatch(typeof(FVRFireArm))]
		[HarmonyPatch("LoadMag")]
		class FVRFireArmLoadMagHook
		{
            static void Postfix(FVRFireArm __instance, FVRFireArmMagazine mag) {
                if (!config_enable_mag_stuff.Value) {
                    return;
                }
                Rigidbody parent_rb = __instance.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_force_mag_insert.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.up) * forceMagnitude;
                    parent_rb.AddForce(localUpForce, ForceMode.Force);

                    // ROTATION in Z.
                    float torqueMagnitude = config_torque_mag_insert.Value; // Example torque magnitude
                    Vector3 localTorque = __instance.transform.TransformDirection(Vector3.forward) * torqueMagnitude;
                    parent_rb.AddTorque(localTorque, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody insert.");
                }
            }
        }

        [HarmonyPatch(typeof(FVRFireArm))]
		[HarmonyPatch("EjectMag")]
		class FVRFireArmEjectMagHook
		{
            static void Postfix(FVRFireArm __instance, bool PhysicalRelease = false) {
                if (!config_enable_mag_stuff.Value) {
                    return;
                }
                Rigidbody parent_rb = __instance.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_force_mag_eject.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.up) * forceMagnitude;
                    parent_rb.AddForce(localUpForce, ForceMode.Force);

                    // ROTATION in Z.
                    float torqueMagnitude = config_torque_mag_eject.Value; // Example torque magnitude
                    Vector3 localTorque = __instance.transform.TransformDirection(Vector3.forward) * torqueMagnitude;
                    parent_rb.AddTorque(localTorque, ForceMode.Force);
                } else {
                    // // Logger.LogMessage("No parent rigidbody eject.");
                }
            }
        }

        // PISTOL SLIDES.
        [HarmonyPatch(typeof(HandgunSlide))]
		[HarmonyPatch("SlideEvent_ArriveAtFore")]
		class HandgunSlideSlideEvent_ArriveAtForeHook
		{
            static void Postfix(HandgunSlide __instance) {
                if (!config_enable_pistol_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                Rigidbody parent_rb = __instance.Handgun.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_pistol_torque_slide_fore.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide fore.");
                }
            }
        }
        [HarmonyPatch(typeof(HandgunSlide))]
		[HarmonyPatch("SlideEvent_SmackRear")]
		class HandgunSlideSlideEvent_SmackRearHook
		{
            static void Postfix(HandgunSlide __instance) {
                if (!config_enable_pistol_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                Rigidbody parent_rb = __instance.Handgun.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_pistol_torque_slide_rear.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * -forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide rear.");
                }
            }
        }

        // CLOSED BOLT HANDLE.
        [HarmonyPatch(typeof(ClosedBoltHandle))]
		[HarmonyPatch("Event_ArriveAtFore")]
		class ClosedBoltEvent_ArriveAtForeHook
		{
            static void Postfix(ClosedBoltHandle __instance) {
                if (!config_enable_big_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                Rigidbody parent_rb = __instance.Weapon.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_big_torque_slide_fore.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide fore.");
                }
            }
        }
        [HarmonyPatch(typeof(ClosedBoltHandle))]
		[HarmonyPatch("Event_HitLockPosition")]
		class ClosedBoltEvent_HitLockPositionHook
		{
            static void Postfix(ClosedBoltHandle __instance) {
                if (!config_enable_big_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                Rigidbody parent_rb = __instance.Weapon.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_big_torque_slide_fore.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide fore.");
                }
            }
        }
        [HarmonyPatch(typeof(ClosedBoltHandle))]
		[HarmonyPatch("Event_SmackRear")]
		class ClosedBoltHandleEvent_SmackRearHook
		{
            static void Postfix(ClosedBoltHandle __instance) {
                if (!config_enable_big_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                Rigidbody parent_rb = __instance.Weapon.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_big_torque_slide_rear.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * -forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide rear.");
                }
            }
        }

        // CLOSED BOLT.
        [HarmonyPatch(typeof(ClosedBolt))]
		[HarmonyPatch("BoltEvent_ArriveAtFore")]
		class ClosedBoltBoltEvent_ArriveAtFore
		{
            static void Postfix(ClosedBolt __instance) {
                if (!config_enable_big_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                if (__instance.Weapon.m_timeSinceFiredShot < 0.1f) {
                    return;
                }
                Rigidbody parent_rb = __instance.Weapon.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_big_torque_slide_fore.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide fore.");
                }
            }
        }
        [HarmonyPatch(typeof(ClosedBolt))]
		[HarmonyPatch("BoltEvent_BoltCaught")]
		class ClosedBoltBoltEvent_BoltCaught
		{
            static void Postfix(ClosedBolt __instance) {
                if (!config_enable_big_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                if (__instance.Weapon.m_timeSinceFiredShot < 0.1f) {
                    return;
                }
                Rigidbody parent_rb = __instance.Weapon.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_big_torque_slide_fore.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide fore.");
                }
            }
        }
        [HarmonyPatch(typeof(ClosedBolt))]
		[HarmonyPatch("BoltEvent_SmackRear")]
		class ClosedBoltBoltEvent_SmackRear
		{
            static void Postfix(ClosedBolt __instance) {
                if (!config_enable_big_slide_stuff.Value || __instance.IsHeld) {
                    return;
                }
                if (__instance.Weapon.m_timeSinceFiredShot < 0.1f) {
                    return;
                }
                Rigidbody parent_rb = __instance.Weapon.transform.GetComponent<Rigidbody>();
                if (parent_rb) {
                    // UP FORCE.
                    float forceMagnitude = config_big_torque_slide_rear.Value; // Example force magnitude
                    Vector3 localUpForce = __instance.transform.TransformDirection(Vector3.right) * -forceMagnitude;
                    parent_rb.AddTorque(localUpForce, ForceMode.Force);
                } else {
                    // Logger.LogMessage("No parent rigidbody handgun slide rear.");
                }
            }
        }

        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)

        internal new static ManualLogSource Logger { get; private set; }

    }
}
