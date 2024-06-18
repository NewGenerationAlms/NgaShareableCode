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
        private static ConfigEntry<float> LinVelMult;
        private static ConfigEntry<float> AngVelMult;
        private static ConfigEntry<bool> OnlyArmSprint;
        private static ConfigEntry<bool> AllowJump;
        private static ConfigEntry<float> JumpThres;
        
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
            LinVelMult = Config.Bind<float>("TwinStickArmSwing",
                                            "Linear Hand Vel Mult.",
                                            3f, 
                                            new ConfigDescription("How much linear hand velocity contributes to sprint speed.", 
                                            new AcceptableValueFloatRangeStep(0.25f, 20f, 0.25f), new object[0]));
            AngVelMult = Config.Bind<float>("TwinStickArmSwing",
                                            "Angular Hand Vel Mult.",
                                            1f, 
                                            new ConfigDescription("How much angular hand velocity contributes to sprint speed.", 
                                            new AcceptableValueFloatRangeStep(0.25f, 20f, 0.25f), new object[0]));
            OnlyArmSprint = Config.Bind<bool>("TwinStickArmSwing", "Only Sprint w Arms.", 
                                                true, "Pressing sprint does nothing unless you swing your arms (recommended).");
            AllowJump = Config.Bind<bool>("TwinStickArmJump", "Allow Armswing Jump.", 
                                                true, "Thrust hands above head.");
            JumpThres = Config.Bind<float>("TwinStickArmJump",
                                            "Needed arm movement (Jump up).",
                                            2f, 
                                            new ConfigDescription("How fast both hands need to be flung above head.", 
                                            new AcceptableValueFloatRangeStep(0.25f, 20f, 0.25f), new object[0]));
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
                keptLoco = GM.Options.MovementOptions.TPLocoSpeedIndex;
                if (CheckSkip()) return;
                if (!TwinArmSwingEnabled.Value) return;
                if (!OnlyArmSprint.Value) return;
                GM.Options.MovementOptions.TPLocoSpeedIndex = 5;
                int size = GM.Options.MovementOptions.TPLocoSpeeds.Length;
                for (int i = 0; i < size; i++) {
                    GM.Options.MovementOptions.TPLocoSpeeds[i] = 1f;
                }
            }
            private static void Postfix(FVRMovementManager __instance)
            {
                GM.Options.MovementOptions.TPLocoSpeedIndex = keptLoco;
                if (CheckSkip()) return;
                if (!TwinArmSwingEnabled.Value) return;
                //if (!OnlyArmSprint.Value) return;
                if (__instance.Mode != FVRMovementManager.MovementMode.TwinStick) {
                    return;
                }
                // Adds angular speed of arms.
                float num = 0f;
                bool is_sprinting = __instance.m_sprintingEngaged;
                for (int j = 0; j < __instance.Hands.Length; j++)
                {
                    float num2 = Mathf.Max(__instance.Hands[j].Input.VelAngularWorld.magnitude * AngVelMult.Value,
                                            __instance.Hands[j].Input.VelLinearWorld.magnitude * LinVelMult.Value);
                    
                    num += num2;
                }
                // Calculates current arm impetus, zero if sprint not pressed.
                // Logger.LogMessage("curImp: " + __instance.m_curArmSwingerImpetus);
                __instance.m_tarArmSwingerImpetus = num;
                __instance.m_curArmSwingerImpetus = is_sprinting ? Mathf.MoveTowards(__instance.m_curArmSwingerImpetus, 
                                __instance.m_tarArmSwingerImpetus, 
                                AccelMult.Value * Time.deltaTime) : 0f;
                // Logger.LogWarning("sprint: " + is_sprinting + " curImp: " + __instance.m_curArmSwingerImpetus
                //                         + " tarImp: " + num);
                // Updates player speed based on impetus if sprinting.
                if (is_sprinting && __instance.m_isGrounded) {
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
                        // Logger.LogMessage("X: " + __instance.worldTPAxis.x + " Z: " + __instance.worldTPAxis.z + " scl: " + final_speed);
                        Vector3 normalized = __instance.worldTPAxis.normalized;
                        __instance.worldTPAxis.x += normalized.x * final_speed;
                        __instance.worldTPAxis.z += normalized.z * final_speed;
                    }
                }
                float jumpThreshold = JumpThres.Value;
                if (AllowJump.Value
                        && __instance.Hands[0].transform.position.y > __instance.Head.position.y 
                        && __instance.Hands[1].transform.position.y > __instance.Head.position.y 
                        && __instance.Hands[0].Input.VelLinearWorld.y > jumpThreshold
                        && __instance.Hands[1].Input.VelLinearWorld.y > jumpThreshold)
                {
                    __instance.Jump();
                }
            }
            static int keptLoco = 0;
        }

        // [HarmonyPatch(typeof(FVRMovementManager))]
		// [HarmonyPatch("FU")]
        // public class FVRMovementManagerFU : MonoBehaviour
        // {
        //     private static void Prefix(FVRMovementManager __instance)
        //     {
                
        //     }
        // }

        internal new static ManualLogSource Logger { get; private set; }
    }
}
