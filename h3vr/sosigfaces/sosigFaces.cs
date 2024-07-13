using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using Sodalite.ModPanel;
using System.Collections.Generic;
using static FistVR.ItemSpawnerV2;
using OtherLoader;
using OtherLoader.Loaders;
using OtherLoader.Patches;
using System.Linq;
using UnityEngine.UI;
using System.Collections;
using System;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.SosigFaces", "SosigFaces", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.0.0")]
    [BepInProcess("h3vr.exe")]
    public partial class SosigFaces : BaseUnityPlugin
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

        // Overall.
		private static ConfigEntry<bool> GameEnabled;
        private static ConfigEntry<bool> SosigHaveFaces;
        private static ConfigEntry<bool> PlayerHasFace;
        private static ConfigEntry<float> mouthClosedV;
        private static ConfigEntry<float> smileFrownV;
        private static ConfigEntry<float> partialOpenV;
        private static ConfigEntry<float> normalOpenv;
        private static ConfigEntry<float> talkDuration;
        private static ConfigEntry<float> silenceDuration;
        private static ConfigEntry<int> pitchSwap;
        public static List<Texture2D> IdleFaces = new List<Texture2D>();
        public static List<Texture2D> AssaultingFaces = new List<Texture2D>();
        public static List<Texture2D> DamagedFaces = new List<Texture2D>();
        public static List<Texture2D> DeadFaces = new List<Texture2D>();

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.SosigFaces");
            Logger.LogMessage("New harmony");
            LoadTextures();
            SetUpConfigFields();
            // InitiateLootPools();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.SosigFaces 0.0.1");
        }

        public static void LoadTextures()
        {
            string rootFolderPath = BepInEx.Paths.PluginPath + "\\NGA-SosigFarce\\";
            IdleFaces = LoadTexturesFromFolder(System.IO.Path.Combine(rootFolderPath, "Idle"));
            AssaultingFaces = LoadTexturesFromFolder(System.IO.Path.Combine(rootFolderPath, "Assaulting"));
            DamagedFaces = LoadTexturesFromFolder(System.IO.Path.Combine(rootFolderPath, "Damaged"));
            DeadFaces = LoadTexturesFromFolder(System.IO.Path.Combine(rootFolderPath, "Dead"));
        }
        private static List<Texture2D> LoadTexturesFromFolder(string folderPath)
        {
            List<Texture2D> textures = new List<Texture2D>();
            string[] filePaths = System.IO.Directory.GetFiles(folderPath, "*.png");
            Logger.LogWarning("Number pngs in " + folderPath + " is " + filePaths.Length);
            foreach (string filePath in filePaths)
            {
                byte[] fileData = System.IO.File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
                textures.Add(texture);
                Logger.LogWarning("Added sosig face texture!");
            }
            return textures;
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            // Overall.
            GameEnabled = Config.Bind<bool>("Overall", "ON/OFF.", true, "Turn mod On / Off");
            SosigHaveFaces = Config.Bind<bool>("Overall", "Sosig Faces ON/OFF.", true, "Sosigs have faces");
            PlayerHasFace = Config.Bind<bool>("Overall", "Player Face ON/OFF.", true, "Player has face");
            mouthClosedV = Config.Bind<float>("Volume",
                                            "Mouth closed",
                                            0.001f, 
                                            new ConfigDescription("Mouth closed for values below this.", 
                                            new AcceptableValueFloatRangeStep(0.0001f, 1f, 0.001f), new object[0]));
            smileFrownV = Config.Bind<float>("Volume",
                                            "Smile Frown",
                                            0.07f, 
                                            new ConfigDescription("Smile Frown for values below this.", 
                                            new AcceptableValueFloatRangeStep(0.0001f, 1f, 0.001f), new object[0]));
            partialOpenV =Config.Bind<float>("Volume",
                                            "Partial Open",
                                            0.02f, 
                                            new ConfigDescription("Partial Open for values below this..", 
                                            new AcceptableValueFloatRangeStep(0.0001f, 1f, 0.001f), new object[0]));
            normalOpenv = Config.Bind<float>("Volume",
                                            "Normal open",
                                            0.03f, 
                                            new ConfigDescription("Mouth open normal for values below this.", 
                                            new AcceptableValueFloatRangeStep(0.0001f, 1f, 0.001f), new object[0]));
            talkDuration = Config.Bind<float>("Animation",
                                            "Mouth Change",
                                            0.15f, 
                                            new ConfigDescription("How long before the mouth next updates", 
                                            new AcceptableValueFloatRangeStep(0.0001f, 1f, 0.001f), new object[0]));
            silenceDuration = Config.Bind<float>("Animation",
                                            "Silence Change",
                                            0.5f, 
                                            new ConfigDescription("How long before the mouth closes", 
                                            new AcceptableValueFloatRangeStep(0.0001f, 1f, 0.001f), new object[0]));
            pitchSwap = Config.Bind<int>("Animation",
                                            "Pitch Frown or Smile",
                                            1, 
                                            new ConfigDescription("Pitch needed to swap between smile and frown at same volume.", 
                                            new AcceptableValueIntRangeStep(0, 100, 1), new object[0]));
        }

        private static bool CheckSkip() {
            return !GameEnabled.Value;
        }
        private static bool CheckSosigSkip() {
            return !GameEnabled.Value || !SosigHaveFaces.Value;
        }
        private static bool CheckPlayerSkip() {
            return !GameEnabled.Value || !PlayerHasFace.Value;
        }

        [HarmonyPatch(typeof(PlayerSosigBody))]
		[HarmonyPatch("Start")]
        public class PlayerSosigBodyAwake : MonoBehaviour
        {
            private static void Postfix(PlayerSosigBody __instance)
            {
                if (CheckPlayerSkip()) return;
                Logger.LogWarning("Trying Player Sosig Start");
                Transform headTransform = __instance.Sosig_Head;
                if (headTransform == null)
                {
                    Logger.LogWarning("Player Head not found!");
                    return;
                }
                FVRObject faceThing;
                Logger.LogMessage("Doing player face");
                if (!IM.OD.TryGetValue("NGA_PlayerFaceThing", out faceThing))
                {
                    Logger.LogWarning("NGA_FaceThing not found in IM.OD!");
                    return;
                }
                GameObject spawned_face = UnityEngine.Object.Instantiate<GameObject>(faceThing.GetGameObject(), 
                                        Vector3.zero, Quaternion.identity);
                Logger.LogMessage("Made face");
                spawned_face.transform.SetParent(headTransform, false);
                Logger.LogMessage("Set face");
                // Optionally, set the local position and rotation if you need it to be in a specific orientation relative to the head
                spawned_face.transform.localPosition = Vector3.zero;
                spawned_face.transform.localRotation = Quaternion.identity;

                FaceLipSync faceThingComponent = spawned_face.gameObject.AddComponent<FaceLipSync>();
            }
        }

        [HarmonyPatch(typeof(Sosig))]
		[HarmonyPatch("ProcessDamage")]
        [HarmonyPatch(new Type[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(Vector3), typeof(SosigLink) })]
        public class SosigProcessDamage : MonoBehaviour
        {
            private static void Postfix(Sosig __instance, float damage_p, float damage_c, float damage_b, float damage_t, Vector3 point, SosigLink link)
            {
                if (CheckSosigSkip()) return;
                // Logger.LogWarning("Damaged");
                __instance.GetComponent<EnemyFaceController>().SetState("Damaged");
            }
        }

        [HarmonyPatch(typeof(Sosig))]
		[HarmonyPatch("SosigDies")]
        public class SosigSosigDies : MonoBehaviour
        {
            private static void Postfix(Sosig __instance, Damage.DamageClass damClass, Sosig.SosigDeathType deathType)
            {
                if (CheckSosigSkip()) return;
                //Logger.LogWarning("Dead");
                __instance.GetComponent<EnemyFaceController>().SetState("Dead");
            }
        }

        // Sosig.BrainUpdate()
        // Sosig.SosigOrder.Assault, Skirmish, TakeCover, StaticShootAt, StaticMeleeAttack
        [HarmonyPatch(typeof(Sosig))]
		[HarmonyPatch("BrainUpdate")]
        public class SosigBrainUpdate : MonoBehaviour
        {
            private static void Postfix(Sosig __instance)
            {
                if (CheckSosigSkip()) return;
                bool assault = false;
                switch (__instance.CurrentOrder)
                {
                    case Sosig.SosigOrder.Disabled:
                    case Sosig.SosigOrder.PathTo:
                    case Sosig.SosigOrder.Idle:
                    case Sosig.SosigOrder.SearchForEquipment:
                    case Sosig.SosigOrder.Flee:
                    case Sosig.SosigOrder.Wander:
                    case Sosig.SosigOrder.Investigate:
                    case Sosig.SosigOrder.GuardPoint:
                        assault = false;
                        break;
                    case Sosig.SosigOrder.Skirmish:
                    case Sosig.SosigOrder.TakeCover:
                    case Sosig.SosigOrder.Assault:
                    case Sosig.SosigOrder.StaticShootAt:
                    case Sosig.SosigOrder.StaticMeleeAttack:
                    default:
                        assault = true;
                        break;
                }
                string state = assault ? "Assaulting" : "Idle";
                //Logger.LogWarning(state);
                __instance.GetComponent<EnemyFaceController>().SetState(state);
            }
        }

        [HarmonyPatch(typeof(Sosig))]
		[HarmonyPatch("Start")]
        public class SosigAwake : MonoBehaviour
        {
            private static void Postfix(Sosig __instance)
            {
                if (CheckSosigSkip()) return;
                //Logger.LogWarning("Trying Sosig Start");
                bool is_player = false;
                foreach (SosigLink link in __instance.Links) {
                    Transform torsoTransform = link.transform;
                    if (link.transform.name != "Sosig_Torso")
                    {
                        //Logger.LogWarning("Link not Sosig_Torso");
                        continue;
                    }
                    if (link.transform == GM.CurrentPlayerBody.Torso) {
                        //Logger.LogWarning("This is the player's torso");
                        is_player = true;
                    }
                    Transform headTransform = torsoTransform.Find("Head");
                    if (headTransform == null)
                    {
                        Logger.LogWarning("Head not found!");
                        return;
                    }
                    FVRObject faceThing;
                    if (is_player) {
                        Logger.LogError("Doing player face when i shouldnt");
                    } else {
                        if (!IM.OD.TryGetValue("NGA_FaceThing", out faceThing))
                        {
                            Logger.LogError("NGA_FaceThing not found in IM.OD!");
                            return;
                        }
                        GameObject spawned_face = UnityEngine.Object.Instantiate<GameObject>(faceThing.GetGameObject(), 
                                                Vector3.zero, Quaternion.identity);
                        spawned_face.transform.SetParent(headTransform, false);

                        // Optionally, set the local position and rotation if you need it to be in a specific orientation relative to the head
                        spawned_face.transform.localPosition = Vector3.zero;
                        spawned_face.transform.localRotation = Quaternion.identity;
                        EnemyFaceController faceThingComponent = __instance.gameObject.AddComponent<EnemyFaceController>();
                        faceThingComponent.Init(spawned_face);
                    }
                }
            }
        }

        class FaceThingComponent : MonoBehaviour {
            public GameObject attached_face = null;
        }


        internal new static ManualLogSource Logger { get; private set; }

        // Constants.
        public class EnemyFaceController : MonoBehaviour
        {
            private Material faceMaterial;
            private Texture2D idleFace;
            private Texture2D assaultFace;
            private Coroutine damageCoroutine;
            public GameObject attached_face = null; 
            bool dead = false;
            bool damaged = false;

            public void Init(GameObject face)
            {
                attached_face = face;
                // Initialize faceMaterial
                Transform faceTransform = attached_face.transform.Find("default");
                if (faceTransform != null)
                {
                    Renderer renderer = faceTransform.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        faceMaterial = new Material(renderer.material); // Clone the material instance
                        renderer.material = faceMaterial; // Assign the cloned material to the renderer
                    }
                    else
                    {
                        Logger.LogError("Renderer component not found on the 'default' object.");
                    }
                }
                else
                {
                    Logger.LogError("'Face' object not found among children.");
                }

                // Set default idle face
                SetRandomIdleFace();
            }

            public void SetRandomIdleFace()
            {
                if (faceMaterial == null ) { return; }
                idleFace = GetRandomTexture(SosigFaces.IdleFaces);
                faceMaterial.mainTexture = idleFace;
                assaultFace = GetRandomTexture(SosigFaces.AssaultingFaces);
            }

            public void SetState(string state)
            {
                if (faceMaterial == null ) { return; }
                if (dead) {
                    return;
                }
                switch (state)
                {
                    case "Dead":
                        if (damageCoroutine != null)
                        {
                            StopCoroutine(damageCoroutine);
                        }
                        faceMaterial.mainTexture = GetRandomTexture(SosigFaces.DeadFaces);
                        dead = true;
                        break;
                    case "Idle":
                        if (damaged || faceMaterial.mainTexture == idleFace) {
                            break;
                        }  
                        faceMaterial.mainTexture = idleFace;
                        break;
                    case "Assaulting":
                        if (damaged || faceMaterial.mainTexture == assaultFace) {
                            break;
                        } 
                        faceMaterial.mainTexture = assaultFace;
                        break;
                    case "Damaged":
                        if (damaged) {
                            break;
                        }
                        if (damageCoroutine != null)
                        {
                            StopCoroutine(damageCoroutine);
                        }
                        faceMaterial.mainTexture = GetRandomTexture(SosigFaces.DamagedFaces);
                        //Logger.LogError("Damaged material changed");
                        damageCoroutine = StartCoroutine(DamageTimeout());
                        break;
                }
            }

            private IEnumerator DamageTimeout()
            {
                damaged = true;
                //Logger.LogError("Damaged? " + damaged);
                yield return new WaitForSeconds(2f);
                faceMaterial.mainTexture = idleFace;
                damaged = false;
                //Logger.LogError("Damaged? " + damaged);
            }

            private Texture2D GetRandomTexture(List<Texture2D> textures)
            {
                if (textures.Count == 0)
                {
                    Logger.LogError("No textures available for the requested state.");
                    return null;
                }
                return textures[UnityEngine.Random.Range(0, textures.Count)];
            }
        }
        public class FaceLipSync : MonoBehaviour
        {
            public AudioSource audioSource;
            public Material mouthMaterial;
            public string folderPath;
            public string closedMouthFileName = "mouthClosed.png";
            public string partiallyOpenMouthFileName = "mouthPartiallyOpen.png";
            public string openMouthFileName = "mouthOpen.png";
            public string smileMouthFileName = "mouthSmile.png";
            public string wideOpenMouthFileName = "mouthWideOpen.png";
            public string frownMouthFileName = "mouthFrown.png";

            private Texture2D closedMouthTexture;
            private Texture2D partiallyOpenMouthTexture;
            private Texture2D openMouthTexture;
            private Texture2D smileMouthTexture;
            private Texture2D wideOpenMouthTexture;
            private Texture2D frownMouthTexture;
            private string microphoneName;
            private float lastActiveTime;
            private float lastChosenMouth = Time.time;

            void Awake()
            {
                // Assign or create the AudioSource component
                if (audioSource == null) {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                // audioSource.mute = true;  // Mute the audio source to prevent playback
                // Initialize mouthMaterial
                if (mouthMaterial == null)
                {
                    Transform mouthTransform = transform.Find("mouth");
                    if (mouthTransform != null)
                    {
                        Renderer renderer = mouthTransform.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            mouthMaterial = renderer.material;
                        }
                        else
                        {
                            Logger.LogError("Renderer component not found on the 'mouth' object.");
                        }
                    }
                    else
                    {
                        Logger.LogError("'mouth' object not found among children.");
                    }
                }

                LoadTextures();
                InitializeMicrophone();
            }

            void LoadTextures()
            {
                Logger.LogMessage("LoadTextures");
                folderPath = BepInEx.Paths.PluginPath + "\\NGA-SosigFarce\\";
                Logger.LogMessage("Folder path:" + folderPath +".");
                Logger.LogMessage("Sample file path:" + folderPath + closedMouthFileName +".");
                closedMouthTexture = LoadTexture(folderPath + closedMouthFileName);
                partiallyOpenMouthTexture = LoadTexture(folderPath + partiallyOpenMouthFileName);
                openMouthTexture = LoadTexture(folderPath + openMouthFileName);
                smileMouthTexture = LoadTexture(folderPath + smileMouthFileName);
                wideOpenMouthTexture = LoadTexture(folderPath + wideOpenMouthFileName);
                frownMouthTexture = LoadTexture(folderPath + frownMouthFileName);
            }

            Texture2D LoadTexture(string path)
            {
                byte[] fileData = System.IO.File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
                return texture;
            }

            void InitializeMicrophone()
            {
                // Log available microphones
                foreach (string device in Microphone.devices)
                {
                    Logger.LogMessage("Microphone detected: " + device);
                }

                // Pick the default microphone (usually the headset's mic in VR)
                if (Microphone.devices.Length > 0)
                {
                    microphoneName = Microphone.devices[0];
                    audioSource.clip = Microphone.Start(microphoneName, true, 1, 44100);
                    audioSource.loop = true;

                    while (!(Microphone.GetPosition(microphoneName) > 0)) { }
                    audioSource.Play();
                }
                else
                {
                    Logger.LogError("No microphones detected at all!");
                }
            }

            void Update()
            {
                if (mouthMaterial == null) {
                    return;
                }
                if (Microphone.IsRecording(microphoneName))
                {
                    // Reduce the sample size for quicker response
                    float[] samples = new float[64];
                    audioSource.GetOutputData(samples, 0);

                    float currentVolume = 0f;
                    float currentPitch = 0f;
                    int zeroCrossingCount = 0;

                    for (int i = 1; i < samples.Length; i++)
                    {
                        currentVolume += Mathf.Abs(samples[i]);
                        if (samples[i - 1] < 0 && samples[i] > 0)
                        {
                            zeroCrossingCount++;
                        }
                    }
                    currentVolume /= samples.Length;
                    currentPitch = zeroCrossingCount;

                    // Determine the appropriate mouth shape based on volume and pitch
                    // TODO: Volume scaler
                    Texture2D chosenTex = closedMouthTexture;
                    if (currentVolume < mouthClosedV.Value)
                    {
                        chosenTex = closedMouthTexture;
                    }
                    else if (currentVolume < smileFrownV.Value)
                    {
                        if (currentPitch < pitchSwap.Value)
                        {
                            chosenTex = frownMouthTexture;
                        }
                        else
                        {
                            chosenTex = smileMouthTexture;
                        }
                    }
                    else if (currentVolume < partialOpenV.Value)
                    {
                        chosenTex = partiallyOpenMouthTexture;
                    }
                    else if (currentVolume < normalOpenv.Value)
                    {
                        chosenTex = openMouthTexture;
                    }
                    else
                    {
                        chosenTex = wideOpenMouthTexture;
                    }
                    lastActiveTime = Time.time;
                    // Close the mouth if no sound is detected for a short duration
                    if (Time.time - lastChosenMouth > talkDuration.Value)
                    {
                        lastChosenMouth = Time.time;
                        mouthMaterial.mainTexture = chosenTex;
                    }
                }

                // Close the mouth if no sound is detected for a short duration
                if (Time.time - lastActiveTime > silenceDuration.Value)
                {
                    mouthMaterial.mainTexture = closedMouthTexture;
                }
            }
        }
    }
    
}
