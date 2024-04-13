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

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.SosigIntegrityConfigs", "SosigIntegrityConfigs", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class SosigIntegrityConfigs : BaseUnityPlugin
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
        private static ConfigEntry<float> config_link_integrity_damaged_threshold;
        private static ConfigEntry<bool> config_link_allow_destroy;
        private static ConfigEntry<float> config_link_head_starting_integrity;
        private static ConfigEntry<float> config_link_torso_starting_integrity;
        private static ConfigEntry<float> config_link_upper_starting_integrity;
        private static ConfigEntry<float> config_link_lower_starting_integrity;
        private static ConfigEntry<bool> config_allow_sosig_explosion_fx;
        private static ConfigEntry<bool> config_allow_sosig_gibs_spawn;
        private static ConfigEntry<bool> config_allow_gibs_flying;
        private static ConfigEntry<float> config_tickdown_override_clear;
        private static ConfigEntry<bool> config_enable_ketchup;
        private static ConfigEntry<string> config_mustard_colour;
        private static ConfigEntry<bool> config_clearsosig_explode;
        private static ConfigEntry<bool> config_clearsosig_force_tickdown;


        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.SosigIntegrityConfigs");


            SetUpConfigFields();

            harmony.PatchAll();

            // Your plugin's ID, Name, and Version are available here.

            Logger.LogMessage($"Hello, world! Sent from NGA.SosigIntegrityConfigs 0.0.1");


        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            config_clearsosig_force_tickdown = Config.Bind("Sosig Body.Clean Up",
                                         "Use Tickdown on ClearSosig",
                                         false,
                                         "Forces all gamemodes like TnH and SupplyRun to use Tickdown Override below.");
            config_tickdown_override_clear = Config.Bind("Sosig Body.Clean Up",
                                         "Tickdown Time before Sosig cleared Override",
                                         15f,
                                         "Requires Tickdown on ClearSosig: True. Time before game cleans up dead sosigs. Attempts to override All gamemodes.");
            config_clearsosig_explode = Config.Bind("Sosig Body.Clean Up",
                                         "Explode on ClearSosig",
                                         true,
                                         "Default: true. Disallows link explosions on Sosig cleanup in TnH, SupplyRun, etc. Independent of integrity settings below.");
            config_link_head_starting_integrity = Config.Bind("Sosig Body.Integrity",
                                         "Sosig Head Link Integrity",
                                         100f,
                                         "Sosig part link integrity, decreased on damage, explodes on 0 killing sosig");
            config_link_torso_starting_integrity = Config.Bind("Sosig Body.Integrity",
                                         "Sosig Torso Link Integrity",
                                         100f,
                                         "Sosig part link integrity, decreased on damage, explodes on 0 killing sosig");
            config_link_upper_starting_integrity = Config.Bind("Sosig Body.Integrity",
                                         "Sosig Upper Link Integrity",
                                         100f,
                                         "Sosig part link integrity, decreased on damage, explodes on 0 killing sosig.");
            config_link_lower_starting_integrity = Config.Bind("Sosig Body.Integrity",
                                         "Sosig Lower Link Integrity",
                                         100f,
                                         "Sosig part link integrity, decreased on damage, explodes on 0 killing sosig.");
            config_link_integrity_damaged_threshold = Config.Bind("Sosig Body.Integrity",
                                         "Integrity threshold Damaged Percent %",
                                         70f,
                                         "If Link integrity falls below threshold %, displays damaged sosig link mesh.");
            config_link_allow_destroy = Config.Bind("Sosig Body.Explode",
                                         "Allow link explode",
                                         true,
                                         "Default: 'true'. Whether the sosig link will explode/despawn when its integrity reaches 0.");
            config_allow_sosig_explosion_fx = Config.Bind("Sosig Body.Explode",
                                         "Sosig Explosion Sound",
                                         true,
                                         "Default: 'true'. Starting value of sosig link integrity, gets decreased upon damage.");
            config_allow_sosig_gibs_spawn = Config.Bind("Sosig Body.Explode",
                                         "Sosig Spawns Gibs on Explosion",
                                         true,
                                         "Default: 'true'. Whether little bits of sosigs (gibs) are spawned once a link explodes.");
            config_allow_gibs_flying = Config.Bind("Sosig Body.Explode",
                                         "Sosig Gibs go Flying on Explosion",
                                         true,
                                         "Default: 'true'. If Gibs spawning are enabled, this determines whether they go flying or simply fall on the ground.");
            config_enable_ketchup = Config.Bind("Sosig Body.Colour",
                                         "Ketchup",
                                         false,
                                         "Some people like ketchup. WARNING: Requires restarting game, try changing 'Mustard Colour' instead.");
            config_mustard_colour = Config.Bind("Sosig Body.Colour",
                                         "Mustard Colour",
                                         "#E3C834FF",
                                         "Default: #E3C834FF (mustard). Requires hex code for color, red is #ff0000.");
            

        }

        [HarmonyPatch(typeof(Sosig))]
        [HarmonyPatch("Start")]
        class SosigStartHook
        {
            static void Postfix(Sosig __instance)
            {
                // Logger.LogMessage("HOOOOKED Sosig Start");
                for (int i = 0; i < __instance.Links.Count; i++)
                {
                    if (__instance.Links[i] != null)
                    {
                        switch (__instance.Links[i].BodyPart)
                        {
                            case SosigLink.SosigBodyPart.Head:
                                __instance.Links[i].SetIntegrity(config_link_head_starting_integrity.Value);
                                __instance.Links[i].m_fullintegrity = config_link_head_starting_integrity.Value;
                                break;

                            case SosigLink.SosigBodyPart.Torso:
                                __instance.Links[i].SetIntegrity(config_link_torso_starting_integrity.Value);
                                __instance.Links[i].m_fullintegrity = config_link_torso_starting_integrity.Value;
                                break;

                            case SosigLink.SosigBodyPart.UpperLink:
                                __instance.Links[i].SetIntegrity(config_link_upper_starting_integrity.Value);
                                __instance.Links[i].m_fullintegrity = config_link_upper_starting_integrity.Value;
                                break;

                            case SosigLink.SosigBodyPart.LowerLink:
                                __instance.Links[i].SetIntegrity(config_link_lower_starting_integrity.Value);
                                __instance.Links[i].m_fullintegrity = config_link_lower_starting_integrity.Value;
                                break;

                            default:
                                __instance.Links[i].SetIntegrity(config_link_head_starting_integrity.Value);
                                __instance.Links[i].m_fullintegrity = config_link_head_starting_integrity.Value;
                                break;
                        }
                    }
                    else
                    {
                        Logger.LogMessage("Sosig links null?");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Sosig))]
        [HarmonyPatch("DestroyLink")]
        class SosigDestroyLinkHook
        {
            static bool Prefix(Sosig __instance, SosigLink link, Damage.DamageClass damClass)
            {
                if (link.J != null)
                {
                    UnityEngine.Object.Destroy(link.J);
                }
                for (int i = __instance.Links.Count - 1; i >= 0; i--)
                {
                    if (__instance.Links[i] != null && __instance.Links[i].J != null && __instance.Links[i].J.connectedBody == link.R)
                    {
                        __instance.Links[i].transform.SetParent(null);
                        UnityEngine.Object.Destroy(__instance.Links[i].J);
                    }
                }
                __instance.m_linksDestroyed[(int)link.BodyPart] = true;
                __instance.Mustard *= 0.75f;
                switch (link.BodyPart)
                {
                    case SosigLink.SosigBodyPart.Head:
                        __instance.KillSpeech();
                        if (__instance.m_doesExplodeKill_Head)
                        {
                            __instance.SosigDies(damClass, Sosig.SosigDeathType.JointExplosion);
                        }
                        else
                        {
                            __instance.Shudder(3f);
                        }
                        break;
                    case SosigLink.SosigBodyPart.Torso:
                        __instance.SosigDies(damClass, Sosig.SosigDeathType.JointExplosion);
                        break;
                    case SosigLink.SosigBodyPart.UpperLink:
                        if (__instance.m_doesExplodeKill_Upper)
                        {
                            __instance.SosigDies(damClass, Sosig.SosigDeathType.JointExplosion);
                        }
                        else
                        {
                            __instance.BreakBack(false);
                            __instance.Shudder(2f);
                        }
                        break;
                    case SosigLink.SosigBodyPart.LowerLink:
                        if (__instance.m_doesExplodeKill_Lower)
                        {
                            __instance.SosigDies(damClass, Sosig.SosigDeathType.JointExplosion);
                        }
                        else
                        {
                            __instance.Hobble(false);
                            __instance.Shudder(2f);
                        }
                        break;
                }
                // MY CHANGES HERE
                if (config_allow_sosig_explosion_fx.Value)
                {
                    // Logger.LogMessage("Skipping sound!");
                    UnityEngine.Object.Instantiate<GameObject>(__instance.DamageFX_Explosion, link.transform.position, UnityEngine.Random.rotation);
                }
                // MY CHANGES HERE
                if (link.HasSpawnOnDestroy && config_allow_sosig_gibs_spawn.Value)
                {
                    UnityEngine.Object.Instantiate<GameObject>(link.SpawnOnDestroy.GetGameObject(), link.transform.position, Quaternion.identity);
                }
                // MY CHANGES HERE
                if (config_allow_sosig_gibs_spawn.Value && __instance.UsesGibs && GM.Options.SimulationOptions.SosigChunksMode == SimulationOptions.SosigChunks.Enabled)
                {
                    Vector3 position = link.transform.position;
                    Quaternion rotation = link.transform.rotation;
                    for (int j = 0; j < __instance.GibLocalPoses.Length; j++)
                    {
                        Vector3 vector = link.transform.position + link.transform.right * (__instance.GibLocalPoses[j].x) + link.transform.up * (__instance.GibLocalPoses[j].y) + link.transform.forward * (__instance.GibLocalPoses[j].z);
                        if (!config_allow_gibs_flying.Value)
                        {
                            //vector = link.transform.position + link.transform.right * (__instance.GibLocalPoses[j].x+0.2f) + link.transform.up * (__instance.GibLocalPoses[j].y+0.2f) + link.transform.forward * (__instance.GibLocalPoses[j].z+0.2f);
                            //Logger.LogMessage("Separated!");
                        }

                        GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.GibPrefabs[j], vector, rotation);
                        Rigidbody component = gameObject.GetComponent<Rigidbody>();
                        // MY CHANGES HERE
                        if (config_allow_gibs_flying.Value)
                        {
                            // Logger.LogMessage("Adding froce!");
                            component.AddForceAtPosition(-link.R.velocity * UnityEngine.Random.Range(0.3f, 1f), link.transform.position, ForceMode.VelocityChange);
                        }
                        else
                        {
                            // Logger.LogMessage("No froce!");
                            nga_static_instance = __instance;
                            component.drag = 20;
                            nga_static_instance.StartCoroutine(EnableColliderAfterDelay(component, 0.05f));
                        }
                    }
                }
                UnityEngine.Object.Destroy(link.gameObject);
                __instance.CleanUpDecals();
                __instance.UpdateRenderers();
                return false; // skip original method
            }

            static public Sosig nga_static_instance;

            static IEnumerator EnableColliderAfterDelay(Rigidbody rg, float delay)
            {
                // Logger.LogMessage("WAIIIT");
                yield return new WaitForSeconds(delay);
                rg.drag = 1;
            }
        }

        [HarmonyPatch(typeof(Sosig))]
        [HarmonyPatch("TickDownToClear")]
        class SosigTickDownToClearHook
        {
            static void Postfix(Sosig __instance, float f)
            {
                // Logger.LogMessage("HOOOOKED Sosig TickDownToClear");
                if (config_clearsosig_force_tickdown.Value) {
                    Logger.LogMessage("Using new tickdown val " + config_tickdown_override_clear.Value);
                    __instance.m_tickDownToClear = config_tickdown_override_clear.Value;
                } else {
                    Logger.LogMessage("No tickdown forced");
                }
            }
        }

        [HarmonyPatch(typeof(Sosig))]
        [HarmonyPatch("ClearSosig")]
        class SosigClearSosigHook
        {
            static bool Prefix(Sosig __instance)
            {
                //Logger.LogMessage("HOOOOKED Sosig ClearSosigHook");
                if (config_clearsosig_force_tickdown.Value) {
                    if (!__instance.m_isTickingDownToClear) {
                        // If sosig told to clear, but it didn't come from TickingDown,
                        // then force it to tick.
                        //Logger.LogMessage("Clearsosig called without tick, forcing tick");
                        __instance.KillSosig();
                        __instance.TickDownToClear(config_tickdown_override_clear.Value);
                        return false; // skip original
                    }
                }
                if (config_clearsosig_explode.Value) {
                    //Logger.LogMessage("Execute og clearsosig");
                    return true; // execute original
                }

                // My patch copied from original.
                //Logger.LogMessage("Doing clearsosig prefix no-explode NOW");
                if (__instance.DeParentOnSpawn != null)
                {
                    UnityEngine.Object.Destroy(__instance.DeParentOnSpawn.gameObject);
                }
                for (int i = 0; i < __instance.Links.Count; i++)
                {
                    if (__instance.Links[i] != null)
                    {
                        // Replaces the Explode thingy.
                        UnityEngine.Object.Destroy(__instance.Links[i].gameObject);
                    }
                }
                for (int j = 0; j < __instance.Hands.Count; j++)
                {
                    __instance.Hands[j].DropHeldObject();
                }
                __instance.Inventory.DropAllObjects();
                __instance.ClearCoverPoint();
                UnityEngine.Object.Destroy(__instance.gameObject);
                return false; // skip original method
            }
        }

        [HarmonyPatch(typeof(SosigLink))]
        [HarmonyPatch("GetDamageStateIndex")]
        class SosigLinkGetDamageStateIndexHook
        {
            static bool Prefix(SosigLink __instance, ref int __result)
            {
                if (__instance.m_integrity > __instance.m_fullintegrity * (config_link_integrity_damaged_threshold.Value/100f))
                {
                    __result = 0;
                    return false; // skip og
                }
                if (__instance.m_integrity > __instance.m_fullintegrity * (config_link_integrity_damaged_threshold.Value/200f))
                {
                    __result = 1;
                    return false; // skip og
                }
                __result = 2;
                return false; // skip og
            }
        }
        
        [HarmonyPatch(typeof(SosigLink))]
        [HarmonyPatch("LinkExplodes")]
        class SosigLinkLinkExplodesHook
        {
            static bool Prefix(SosigLink __instance, Damage.DamageClass damClass)
            {
                // Logger.LogMessage("HOOOOKED SosigLink LinkExplodes");
                if (config_link_allow_destroy.Value)
                {
                    return true;
                }
                return false; // skip original
            }
        }

        [HarmonyPatch(typeof(Sosig))]
        [HarmonyPatch("BleedingUpdate")]
        class SosigBleedingUpdateHook
        {
            static void Prefix(Sosig __instance)
            {
                // Logger.LogMessage("HOOOOKED Sosig BleedingUpdate");
                string hex_color = config_mustard_colour.Value;
                List<GameObject> mustards = new List<GameObject>{__instance.DamageFX_LargeMustardBurst, __instance.DamageFX_SmallMustardBurst,
                                                __instance.DamageFX_LargeMustardBurst, __instance.DamageFX_MustardSpoutSmall,
                                                __instance.DamageFX_MustardSpoutLarge};
                if (config_enable_ketchup.Value)
                {
                    foreach (GameObject leobj in mustards)
                    {
                        if (leobj)
                        {
                            // Logger.LogMessage("Null obj");
                        }
                        ParticleSystem particleSystem = leobj.GetComponent<ParticleSystem>();
                        if (particleSystem == null)
                        {
                            // Logger.LogMessage("No particle sys for " + leobj.name);
                        }
                        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();

                        if (particleRenderer != null)
                        {
                            // Assign the material to the renderer
                            Color colour = GetColorFromString(hex_color);
                            // Logger.LogMessage("A " + leobj.name);
                            particleRenderer.material.SetColor("_Tint", colour);
                            //Logger.LogMessage("B " + particleRenderer.material.GetCo("_Tint", colour););
                            particleRenderer.material.SetColor("_Color", colour);
                            //Logger.LogMessage("C " + leobj.name);
                        }
                        else
                        {
                            // Logger.LogMessage("No particle renderer for " + leobj.name);
                        }
                    }
                }
            }
        }

        static Color GetColorFromString(string colorString)
        {
            Color color;

            // Try parsing the color string

            if (ColorUtility.TryParseHtmlString(colorString, out color))
            {
                // Parsing successful, return the Color object
                return color;
            }
            else
            {
                // Parsing failed, return a default color or handle the error accordingly
                Logger.LogMessage("Failed to parse color from string: " + colorString);
                return Color.white; // Default color (you can change this)
            }
        }

        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)

        internal new static ManualLogSource Logger { get; private set; }

    }
}
