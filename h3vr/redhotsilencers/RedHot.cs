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
using RootMotion;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.RedHot", "RedHot", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class RedHot : BaseUnityPlugin
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
        // private static ConfigEntry<bool> config_enable_vanilla;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.RedHot");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.RedHot 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            // config_enable_vanilla = Config.Bind("Scenes Saving Allowed",
            //                              "All Vanilla Scenes",
            //                              true,
            //                              "Enables scene saving in all Main Menu accessible scenes");
        }

        [HarmonyPatch(typeof(Suppressor))]
		[HarmonyPatch("ShotEffect")]
		class SuppressorShotEffectHook
		{
            static void Prefix(Suppressor __instance) {
                // Logger.LogMessage("HOOKED ShotEffect!");
                // Iterate through all child components
                foreach (Transform child in __instance.transform)
                {
                    // Check if the child is active and has a MeshRenderer
                    if (child.gameObject.activeSelf && child.GetComponent<MeshRenderer>() != null)
                    {
                        // Access the MeshRenderer and return its material
                        Renderer meshRenderer = child.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            MaterialPropertyBlock current_pBlock = new MaterialPropertyBlock();
                            meshRenderer.GetPropertyBlock(current_pBlock);
                            float emissionWeight = current_pBlock.GetFloat("_EmissionWeight");
                            Logger.LogMessage("xxxxxxxxxxxxxxxCurrent weight set to " + emissionWeight);

                            Color thered = current_pBlock.GetVector("_EmissionTint");
                            Logger.LogMessage("xxxxxxxxxxxxxxxCurrent color set to " + thered);

                            // Reduce the green and blue components by 0.01
                            emissionWeight += 0.1f;

                            // Clamp the values to ensure they stay within the valid range [0, 1]
                            emissionWeight = Mathf.Clamp(emissionWeight, 0f, 1f);

                            // Set the modified color back to the material
                            current_pBlock.SetFloat("_EmissionWeight", emissionWeight);
                            Logger.LogMessage("xxxxxxxxxxxxxxxEmission now weight set to " + emissionWeight);
                            meshRenderer.SetPropertyBlock(current_pBlock);
                        }
                    }
                }
            }
            static Color colour = Color.red;
        }

        [HarmonyPatch(typeof(Suppressor))]
		[HarmonyPatch("Awake")]
		class SuppressorAwakeHook
		{
            static void Postfix(Suppressor __instance) {
                foreach (Transform child in __instance.transform)
                {
                    // Check if the child is active and has a MeshRenderer
                    if (child.gameObject.activeSelf && child.GetComponent<MeshRenderer>() != null)
                    {
                        // Access the MeshRenderer and return its material
                        Renderer meshRenderer = child.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            MaterialPropertyBlock current_pBlock = new MaterialPropertyBlock();
                            meshRenderer.GetPropertyBlock(current_pBlock);
                            
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Suppressor))]
		[HarmonyPatch("FVRUpdate")]
		class SuppressorFVRUpdateHook
		{
            static void Prefix(Suppressor __instance) {
                // Iterate through all child components
                foreach (Transform child in __instance.transform)
                {
                    // Check if the child is active and has a MeshRenderer
                    if (child.gameObject.activeSelf && child.GetComponent<MeshRenderer>() != null)
                    {
                        // Access the MeshRenderer and return its material
                        Renderer meshRenderer = child.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            MaterialPropertyBlock current_pBlock = new MaterialPropertyBlock();
                            meshRenderer.GetPropertyBlock(current_pBlock);
                            float emissionWeight = current_pBlock.GetFloat("_EmissionWeight");

                            Color redColor = new Color(1f, 0f, 0f, 1f);
                            current_pBlock.SetColor("_EmissionTint", redColor);

                            //Material material = meshRenderer.material;
                            //Color emissionWeight = material.GetColor("_Color");
                            if (emissionWeight > 0.2f && __instance.curMount && __instance.curMount.Parent
                                    && __instance.curMount.Parent is FVRFireArm) {
                                FVRFireArm parent = ((FVRFireArm)__instance.curMount.Parent);
                                for (int j = 0; j < parent.GasOutEffects.Length; j++)
                                {
                                    // Logger.LogMessage("Follows " + parent.GasOutEffects[j].FollowsMuzzle + " j " + j + " is gasper x " + parent.GasOutEffects[j].GasPerEvent.x
                                    //     + " y " + parent.GasOutEffects[j].GasPerEvent.y);
                                    if (parent.GasOutEffects[j].FollowsMuzzle)
                                    {
                                        parent.GasOutEffects[j].GasPerEvent.y = parent.GasOutEffects[j].GasPerEvent.x;
                                        parent.GasOutEffects[j].PSystem.transform.position = parent.GetMuzzle().position;
                                        parent.GasOutEffects[j].PSystem.Emit(1);
                                    }
                                }
                            }

                            // Reduce the green and blue components by 0.01
                            emissionWeight -= 0.01f * Time.deltaTime;

                            // Clamp the values to ensure they stay within the valid range [0, 1]
                            emissionWeight = Mathf.Clamp(emissionWeight, 0f, 1f);

                            current_pBlock.SetFloat("_EmissionWeight", emissionWeight);
                            meshRenderer.SetPropertyBlock(current_pBlock);
                        }
                    }
                }
            }
        }

        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)

        internal new static ManualLogSource Logger { get; private set; }

    }
}
