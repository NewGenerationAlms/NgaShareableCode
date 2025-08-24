using BepInEx;
using BepInEx.Logging;
using FistVR;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using HarmonyLib;
using BepInEx.Configuration;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.RedHot", "RedHot", "2.0.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class RedHot : BaseUnityPlugin
    {
        // Player-Configurable Vars.
        private static ConfigEntry<bool> debug_mode;
        private static ConfigEntry<bool> spit_smoke;
        private static ConfigEntry<float> temp_increase_per_shot;
        private static ConfigEntry<float> temp_decrease_rate;
        private static ConfigEntry<bool> allow_diffusion_pattern;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.RedHot");
            SetUpConfigFields();
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.RedHot 2.0.0");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            spit_smoke = Config.Bind("Options",
                                         "Spit Smoke.",
                                         true,
                                         "Enables smoke emission from hot suppressors.");
            allow_diffusion_pattern = Config.Bind("Options",
                                         "Allow Diffusion Pattern.",
                                         false,
                                         "Paints the suppressor so it's not uniformly red, instead using a pattern which may break.");
            temp_increase_per_shot = Config.Bind("Options",
                                         "Temp Increase Per Shot.",
                                         0.005f,
                                         "Amount the suppressor's temperature increases per shot fired.");
            temp_decrease_rate = Config.Bind("Options",
                                         "Temp Decrease Rate",
                                         0.005f,
                                         "Rate at which the suppressor cools down over time.");
            debug_mode = Config.Bind("Debug",
                                         "Debug Mode",
                                         false,
                                         "Enables debug mode, which outputs extra information to the console.");
        }

        public class SuppressorTempMan : MonoBehaviour
        {
            public Suppressor mySupp;
            public List<Renderer> meshRenderers = new List<Renderer>();
            public List<ObjectTemperature> geoTemps = new List<ObjectTemperature>();
            public float timeSinceLogged = 0f;
            public float currentTemp = 0f;
            public static Texture2D GradientMask = null;
            public static Color emissionColor = new Color(3f, 1.055172f, 0f, 1f);

            // Finds all Renderers that will need to be updated on temperature changes. Sets emissino to color red.
            private void Awake()
            {
                EmissionMaskProvider();
                mySupp = this.GetComponent<Suppressor>();
                foreach (Transform child in this.transform)
                {
                    if (child.gameObject.activeSelf && child.GetComponent<MeshRenderer>() != null)
                    {
                        Renderer meshRenderer = child.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderers.Add(meshRenderer);
                            AddObjectTemperatureToChild(child);
                            CopyMaterialEnableEmission(meshRenderer);
                            AddEmmissionToRendererV1(meshRenderer, 0f);
                        }
                    }
                }
            }
            private void AddObjectTemperatureToChild(Transform child)
            {
                // Add & initialize ObjectTemperature component.
                ObjectTemperature geoTemp = child.gameObject.AddComponent<ObjectTemperature>();
                geoTemp.thermalIntensityProfile = ObjectTemperature.ThermalProfile.HotBrass;
                if (allow_diffusion_pattern.Value && GradientMask != null) geoTemp._thermalTexture = GradientMask;
                geoTemp.GetRenderers();
                geoTemp.thermalIntensityScale = 0f;
                geoTemp.thermalTextureStrength = 0f;
                geoTemps.Add(geoTemp);
            }
            public static void CopyMaterialEnableEmission(Renderer renderer)
            {
                Material mat = new Material(renderer.sharedMaterial);
                renderer.material = mat;

                // Turn on emission and set a color
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emissionColor);
                if (allow_diffusion_pattern.Value && GradientMask != null) mat.SetTexture("_EmissionMap", GradientMask);
            }
            public static void AddEmmissionToRendererV1(Renderer renderer,
                float emissionWeight)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", emissionColor);
                mpb.SetFloat("_EmissionWeight", emissionWeight);
                renderer.SetPropertyBlock(mpb);
            }

            public void FixedUpdate()
            {
                timeSinceLogged += Time.deltaTime;
                // 1. Renders smoke.
                RenderSmoke();

                // 2. Cools down the suppressor over time.
                DecrementCurrTempWithCurve();

                // 3. Update object temperature for thermal vision.
                foreach (ObjectTemperature geoTemp in geoTemps)
                {
                    geoTemp.thermalIntensityScale = currentTemp * currentTemp * 8f;
                    geoTemp.thermalTextureStrength = currentTemp * currentTemp * 8f;
                }

                // 4. Updates the material emission weight.
                foreach (Renderer renderer in meshRenderers)
                {
                    AddEmmissionToRendererV1(renderer, currentTemp * currentTemp);
                }
                if (currentTemp >= 0.01f)
                {
                    if (timeSinceLogged >= 1f)
                    {
                        timeSinceLogged = 0f;
                        LogMessage("Current Temp: " + currentTemp);
                    }
                }
            }

            private void DecrementCurrTempWithCurve()
            {
                currentTemp -= Time.deltaTime * temp_decrease_rate.Value;
                currentTemp = Mathf.Clamp(currentTemp, 0f, 1f);
            }

            private void RenderSmoke()
            {
                if (!spit_smoke.Value) return;
                if (currentTemp > 0.2f && mySupp && mySupp.curMount
                        && mySupp.curMount.Parent
                        && mySupp.curMount.Parent is FVRFireArm)
                {
                    FVRFireArm parent = (FVRFireArm)mySupp.curMount.Parent;
                    for (int j = 0; j < parent.GasOutEffects.Length; j++)
                    {
                        if (parent.GasOutEffects[j].FollowsMuzzle)
                        {
                            parent.GasOutEffects[j].GasPerEvent.y = parent.GasOutEffects[j].GasPerEvent.x;
                            parent.GasOutEffects[j].PSystem.transform.position = parent.GetMuzzle().position;
                            parent.GasOutEffects[j].PSystem.Emit(1);
                        }
                    }
                }
            }

            public void ShotFired()
            {
                currentTemp += temp_increase_per_shot.Value;
                LogMessage("Shot fired!!! Current Temp: " + currentTemp);
            }

            // Generates a gradient texture for thermal vision.
            private static void EmissionMaskProvider()
            {
                if (GradientMask != null) return;
                string modDir = Path.Combine(Paths.PluginPath, "NGA-RedHotSilencers");
                string filePath = Path.Combine(modDir, "DiffuseScatteringOnRing.png");
                
                if (!File.Exists(filePath))
                {
                    Logger.LogError($"[MyMod] EmissionMaskProvider: PNG not found at {filePath}");
                    return;
                }

                try
                {
                    // Load the PNG into a Texture2D
                    byte[] pngData = File.ReadAllBytes(filePath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(pngData))
                    {
                        Logger.LogError("[MyMod] EmissionMaskProvider: Failed to load PNG data.");
                        return;
                    }

                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;

                    GradientMask = tex;
                    LogMessage($"[MyMod] EmissionMaskProvider: Loaded custom mask from {filePath}");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[MyMod] EmissionMaskProvider: Exception while loading texture - {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(Suppressor))]
		[HarmonyPatch("ShotEffect")]
		class SuppressorShotEffectHook
		{
            static void Prefix(Suppressor __instance)
            {
                // Finds SuppressorTempMan component, and calls ShotFired to notify of a shot.
                SuppressorTempMan tempMan = __instance.GetComponent<SuppressorTempMan>();
                if (tempMan != null)
                {
                    tempMan.ShotFired();
                }
                else
                {
                    LogError("SuppressorTempMan component not found on suppressor!");
                }
            }
        }

        [HarmonyPatch(typeof(MuzzleDevice))]
		[HarmonyPatch("Awake")]
		class SuppressorAwakeHook
		{
            static void Postfix(MuzzleDevice __instance) {
                // Adds SuppressorTempMan component to the suppressor.
                if (__instance is Suppressor &&
                 __instance.GetComponent<SuppressorTempMan>() == null)
                {
                    __instance.gameObject.AddComponent<SuppressorTempMan>();
                    LogMessage("SuppressorTempMan component added to suppressor.");
                }
            }
        }

        internal static void LogMessage(string message)
        {
            if (!debug_mode.Value) return;
            Logger?.LogInfo(message);
        }

        internal static void LogError(string message)
        {
            if (!debug_mode.Value) return;
            Logger?.LogError(message);
        }

        internal new static ManualLogSource Logger { get; private set; }

    }
}
