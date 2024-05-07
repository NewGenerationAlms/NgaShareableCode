using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using Sodalite.ModPanel;
using System.Collections.Generic;
using static FistVR.ItemSpawnerV2;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.HeadArmSwing", "HeadArmSwing", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class HeadArmSwing : BaseUnityPlugin
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

		private static ConfigEntry<bool> GameEnabled;
        private static ConfigEntry<bool> TwinArmSwingEnabled;
        private static ConfigEntry<float> SprintSpeedCap;
        private static ConfigEntry<float> JogSpeedCap;
        private static ConfigEntry<float> JogMinHand;
        private static ConfigEntry<float> SprintMinHand;
        private static ConfigEntry<float> AccelMult;
        private static ConfigEntry<bool> OnlyArmSprint;
        
        // SHH.
        //public static ConfigEntry<string> RewardVaultName;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.HeadArmSwing");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.HeadArmSwing 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            // Overall.
            GameEnabled = Config.Bind<bool>("Overall", "ON/OFF", true, "Completely enable/disable mod");
            TwinArmSwingEnabled = Config.Bind<bool>("TwinStickArmSwing", "ON/OFF", true, "Enable armswinger with twinstick");
            SprintSpeedCap = Config.Bind<float>("TwinStickArmSwing",
                                            "Sprint speed.",
                                            2.75f, 
                                            new ConfigDescription("Sprint speed soft-cap on armswing. Bigger than jog.", 
                                            new AcceptableValueFloatRangeStep(0f, 20f, 0.25f), new object[0]));
            JogSpeedCap = Config.Bind<float>("TwinStickArmSwing",
                                            "Jog Speed.",
                                            1.25f, 
                                            new ConfigDescription("Jog speed soft-cap on armswing.", 
                                            new AcceptableValueFloatRangeStep(0f, 20f, 0.25f), new object[0]));
            SprintMinHand = Config.Bind<float>("TwinStickArmSwing",
                                            "Needed Arm-movement (Sprint).",
                                            4f, 
                                            new ConfigDescription("How much arm movement to start sprinting. Bigger than jog.", 
                                            new AcceptableValueFloatRangeStep(0.25f, 20f, 0.25f), new object[0]));
            JogMinHand = Config.Bind<float>("TwinStickArmSwing",
                                            "Needed Arm-movement (Jog).",
                                            1.75f, 
                                            new ConfigDescription("How much arm movement to start jogging.", 
                                            new AcceptableValueFloatRangeStep(0.25f, 20f, 0.25f), new object[0]));
            AccelMult = Config.Bind<float>("TwinStickArmSwing",
                                            "Acceleration multiplier.",
                                            10f, 
                                            new ConfigDescription("How fast you speed up and slow down when swinging faster or slower.", 
                                            new AcceptableValueFloatRangeStep(0.25f, 20f, 0.25f), new object[0]));
            OnlyArmSprint = Config.Bind<bool>("TwinStickArmSwing", "Only Sprint w Arms.", 
                                                true, "Pressing sprint does nothing unless you swing your arms (recommended).");
        }

        private static bool CheckSkip() {
            return !GameEnabled.Value; //if (CheckSkip()) return;
        }

        [HarmonyPatch(typeof(FVRMovementManager))]
		[HarmonyPatch("HandUpdateTwinstick")]
        public class FVRMovementManagerHandUpdateTwoAxis : MonoBehaviour
        {
            private static void Prefix(FVRMovementManager __instance)
            {
                if (CheckSkip()) return;
                if (!TwinArmSwingEnabled.Value) return;
                if (!OnlyArmSprint.Value) return;
                keptLoco = GM.Options.MovementOptions.TPLocoSpeedIndex;
                GM.Options.MovementOptions.TPLocoSpeedIndex = 5;
                int size = GM.Options.MovementOptions.TPLocoSpeeds.Length;
                for (int i = 0; i < size; i++) {
                    GM.Options.MovementOptions.TPLocoSpeeds[i] = 1f;
                }
            }
            private static void Postfix(FVRMovementManager __instance)
            {
                if (CheckSkip()) return;
                if (!TwinArmSwingEnabled.Value) return;
                if (!OnlyArmSprint.Value) return;
                GM.Options.MovementOptions.TPLocoSpeedIndex = keptLoco;
            }
            static int keptLoco = 0;
        }

        [HarmonyPatch(typeof(FVRMovementManager))]
		[HarmonyPatch("FU")]
        public class FVRMovementManagerFU : MonoBehaviour
        {
            private static void Prefix(FVRMovementManager __instance)
            {
                if (CheckSkip()) return;
                if (!TwinArmSwingEnabled.Value) return;
                // Adds angular speed of arms.
                float num = 0f;
                bool is_sprinting = false;
                for (int j = 0; j < __instance.Hands.Length; j++)
                {
                    float num2 = __instance.Hands[j].Input.VelAngularWorld.magnitude;
                    if (__instance.Hands[j].IsThisTheRightHand)
                    {
                        is_sprinting = __instance.Hands[j].Input.TouchpadNorthPressed;
                    }
                    num += num2;
                }
                // Calculates current arm impetus.
                __instance.m_tarArmSwingerImpetus = num;
                // if (num < __instance.m_curArmSwingerImpetus) {
                //     __instance.m_curArmSwingerImpetus = num;
                // } else {
                // }
                __instance.m_curArmSwingerImpetus = Mathf.MoveTowards(__instance.m_curArmSwingerImpetus, 
                                __instance.m_tarArmSwingerImpetus, 
                                AccelMult.Value * Time.deltaTime);
                // Updates player speed based on impetus if sprinting.
                if (is_sprinting) {
                    float impetus = __instance.m_curArmSwingerImpetus;
                    // If hand speed is greater than minimum jog trigger value.
                    float jogSpeed = JogSpeedCap.Value;
                    float jogMinTrig = JogMinHand.Value;
                    if (impetus > jogMinTrig) {
                        // Sets jog speed scaled up by proportion to min trigger val. 
                        float jogScaleUp = impetus/jogMinTrig;
                        float final_speed = jogSpeed * jogScaleUp;
                        // If hand speed is greater than minimum sprint trigger value.
                        float sprintMinTrig = SprintMinHand.Value;
                        float sprintSpeed = SprintSpeedCap.Value;
                        if (impetus > sprintMinTrig && jogMinTrig < sprintMinTrig) {
                            float sprintScaleUp = impetus/sprintMinTrig;
                            final_speed = sprintSpeed * sprintScaleUp;
                        }
                        // Multiplies the vector of player position by new speed.
                        __instance.worldTPAxis.x *= final_speed;
                        __instance.worldTPAxis.z *= final_speed;
                    }
                }
            }
            private static void Postfix(FVRMovementManager __instance)
            {
                if (CheckSkip()) return;
                if (!TwinArmSwingEnabled.Value) return;
                bool is_sprinting = false;
                for (int j = 0; j < __instance.Hands.Length; j++)
                {
                    if (__instance.Hands[j].IsThisTheRightHand)
                    {
                        is_sprinting = __instance.Hands[j].Input.TouchpadNorthPressed;
                    }
                }
                if (is_sprinting) {
                    // Logger.LogWarning("WTPA3! " + __instance.worldTPAxis);
                    // Logger.LogWarning("After3! " + __instance.m_smoothLocoVelocity);
                }
            }
        }

        internal new static ManualLogSource Logger { get; private set; }
    }
}
