using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using Sodalite;
using Sodalite.Api;
using Sodalite.ModPanel;
using System.Collections.Generic;
using static FistVR.ItemSpawnerV2;
using System.Collections;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.PauseButton", "PauseButton", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class PauseButton : BaseUnityPlugin
    {
		private static ConfigEntry<bool> GameEnabled;
        private static ConfigEntry<float> YourFavoriteNumber;
        private static bool game_paused_now = false;
        public static float technically_not_zero = 1e-10f;
        private static readonly float s_fixedDeltaTime = Time.fixedDeltaTime;
        FVRSceneSettings _currentSceneSettingsPtr = null;
  

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.PauseButton");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.PauseButton 0.0.1");
        }
        public void Update()
		{
			if (GM.CurrentSceneSettings != null && GM.CurrentSceneSettings != _currentSceneSettingsPtr)
			{
                _currentSceneSettingsPtr = GM.CurrentSceneSettings;
				GM.CurrentSceneSettings.PlayerDeathEvent -= DisablePauseOnDeath;
				GM.CurrentSceneSettings.PlayerDeathEvent += DisablePauseOnDeath;
			}
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            // Overall.
            GameEnabled = Config.Bind<bool>("Overall", "ON/OFF", true, "Disables all code execution by this mod.");
            YourFavoriteNumber = Config.Bind<float>("Overall",
                                            "Your lucky float.",
                                            2.75f, 
                                            new ConfigDescription("Does nothing.", 
                                            new AcceptableValueFloatRangeStep(0f, 20f, 0.25f), new object[0]));
        }

        private static bool CheckSkip() {
            return !GameEnabled.Value; //if (CheckSkip()) return;
        }

        public static void TogglePause(object sender, ButtonClickEventArgs args)
		{
			if (game_paused_now)
			{
				SpeedUpTime();
			} else {
                SlowDownTime();
            }
		}
        private void DisablePauseOnDeath(bool killedSelf = false)
		{
			SpeedUpTime();
		}

        public static void SpeedUpTime()
		{
			ChangeTimeScale(1f);
			game_paused_now = false;
		}
        public static void SlowDownTime()
		{
			ChangeTimeScale(0f);
			game_paused_now = true;
		}
        public static void ChangeTimeScale(float scale)
		{
			Time.timeScale = Mathf.Clamp(scale, technically_not_zero, 1f);
			Time.fixedDeltaTime = Time.timeScale * s_fixedDeltaTime;
		}
        
        [HarmonyPatch(typeof(FVRWristMenu2))]
		[HarmonyPatch("Awake")]
		private class WristMenuAwakeHook
		{
			private static void Postfix(FVRWristMenu2 __instance)
			{
                if (__instance == null) {
                    Logger.LogMessage("FVRWristMenu2 is null!?");
                }
                bool butonIn = false;
                string buttonName = "Pause/Play";
                foreach (WristMenuButton buton in Sodalite.Api.WristMenuAPI.Buttons) {
                    Logger.LogMessage("CC " + buton.Text);
                    if (buton.Text == buttonName) {
                        butonIn = true;
                        break;
                    }
                }
                if (!butonIn) {
                    Sodalite.Api.WristMenuAPI.Buttons.Add
                            (new WristMenuButton(buttonName, int.MaxValue,
                                new ButtonClickEvent(PauseButton.TogglePause)));
                }
            }
        }

        internal new static ManualLogSource Logger { get; private set; }
    }
}
