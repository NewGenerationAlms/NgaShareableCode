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
    [BepInPlugin("NGA.GunCaseFix", "GunCaseFix", "0.1.0")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class GunCaseFix : BaseUnityPlugin
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
        private static ConfigEntry<bool> config_enable_vanilla;
        private static ConfigEntry<bool> config_enable_tnh;
        private static ConfigEntry<bool> config_enable_modded;
        private static ConfigEntry<string> config_exclude_ids;
        private static ConfigEntry<string> config_include_ids;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.GunCaseFix");
            Logger.LogMessage("New harmony");
            SetUpConfigFields();
            Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.GunCaseFix 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
            // config_enable_vanilla = Config.Bind("Scenes Saving Allowed",
            //                              "All Vanilla Scenes",
            //                              true,
            //                              "Enables scene saving in all Main Menu accessible scenes");
            // config_enable_modded = Config.Bind("Scenes Saving Allowed",
            //                              "Any Modded Map",
            //                              true,
            //                              "Enables scene saving in any Atlas map installed from Thundersore. !!Wurstmod maps DONT work!!");
            // config_enable_tnh = Config.Bind("Scenes Saving Allowed",
            //                              "Vanilla TnH Maps",
            //                              false,
            //                              "Enables scene saving in all vanilla TnH Menu accessible maps");
            // config_include_ids = Config.Bind("Custom Include/Exclude",
            //                              "Scenes IDs to INCLUDE",
            //                              "sampleID5,sampleID7",
            //                              "Comma, separated list of scene IDs to INCLUDE from scene saving. Enforced after all conditions above.");
            // config_exclude_ids = Config.Bind("Custom Include/Exclude",
            //                              "Scenes IDs to EXCLUDE",
            //                              "sampleID1,sampleID2",
            //                              "Comma, separated list of scene IDs to EXCLUDE from scene saving. Enforced after all conditions above.");
        }

        [HarmonyPatch(typeof(SaveableGunCase))]
		[HarmonyPatch("OpenCase")]
		class SaveableGunCaseOpenCaseHook
		{
			static void Postfix(SaveableGunCase __instance)
            {
                //Logger.LogMessage("SaveableGunCase OpenCase Postfix HOOKED!");
                if (__instance == null) {
                    Logger.LogMessage("SaveableGunCase is null!?");
                }
                //Logger.LogMessage("Contained Positions count" + __instance.m_containedPositions.Count);
                //Logger.LogMessage("Contained Rotations count" + __instance.m_containedRotations.Count);
                //Logger.LogMessage("Contained Objects count" + __instance.m_containedObjects.Count);
                __instance.m_containedPositions.Clear();
                __instance.m_containedRotations.Clear();
                //Logger.LogMessage("Contained again Positions count" + __instance.m_containedPositions.Count);
                //Logger.LogMessage("Contained again Rotations count" + __instance.m_containedRotations.Count);
            }
		}

        [HarmonyPatch(typeof(SaveableGunCase))]
		[HarmonyPatch("CloseCase")]
		class SaveableGunCloseCaseHook
		{
			static void Postfix(SaveableGunCase __instance)
            {
                //Logger.LogMessage("SaveableGunCase CloseCase Postfix HOOKED!");
                if (__instance == null) {
                    Logger.LogMessage("SaveableGunCase is null!?");
                }
                //Logger.LogMessage("Contained Positions count" + __instance.m_containedPositions.Count);
                //Logger.LogMessage("Contained Rotations count" + __instance.m_containedRotations.Count);
                //Logger.LogMessage("Contained Objects count" + __instance.m_containedObjects.Count);
            }
		}

        [HarmonyPatch(typeof(VaultSystem))]
		[HarmonyPatch("WriteToVaultFileObject")]
		class VaultSystemWriteToVaultFileObjectHook
		{
			static bool Prefix(List<FVRPhysicalObject> objs, VaultFile f, bool savePositionsRelativeToSpawner,
                                bool saveworldPositions, bool encodeAsLoadout, Transform ScanRoot)
            {
                Logger.LogMessage("VaultSystem WriteToVaultFileObject Postfix HOOKED!");
                f.Objects.Clear();
                if (encodeAsLoadout)
                {
                    f.QuickbeltLayoutName = ManagerSingleton<GM>.Instance.QuickbeltConfigurations[GM.Options.QuickbeltOptions.QuickbeltPreset].name;
                }
                Dictionary<FVRPhysicalObject, int> dictionary = new Dictionary<FVRPhysicalObject, int>();
                Dictionary<FVRPhysicalObject, int> dictionary2 = new Dictionary<FVRPhysicalObject, int>();
                Dictionary<FVRPhysicalObject, int> dictionary3 = new Dictionary<FVRPhysicalObject, int>();
                Dictionary<FVRPhysicalObject, int> dictionary4 = new Dictionary<FVRPhysicalObject, int>();
                List<FVRPhysicalObject> MYobjs = new List<FVRPhysicalObject>(); // MYobjs will replace objs after this loop.
                for (int i = 0; i < objs.Count; i++)
                {
                    //Logger.LogMessage("OG order: " + objs[i]);
                    List<FVRPhysicalObject> containedObjectsRecursively = objs[i].GetContainedObjectsRecursively();
                    MYobjs.Add(objs[i]); // A
                    for (int j = 0; j < containedObjectsRecursively.Count; j++)
                    {
                        // if (!objs.Contains(containedObjectsRecursively[j]))
                        // {
                        //     objs.Add(containedObjectsRecursively[j]);
                        // }
                        // If object already added, remove it from its previous position, then add it after its parent.
                        if (MYobjs.Contains(containedObjectsRecursively[j])) {
                            MYobjs.Remove(containedObjectsRecursively[j]);
                        }
                        MYobjs.Add(containedObjectsRecursively[j]);
                    }
                }
                objs = MYobjs; // objs is now MYobjs, which has all contained objects occurring after their parent. 
                for (int k = 0; k < objs.Count; k++)
                {
                    dictionary.Add(objs[k], k);
                    VaultObject vaultObject = VaultSystem.SavePhysicalObjectToVaultObject(objs[k], savePositionsRelativeToSpawner, saveworldPositions, dictionary2, dictionary3, dictionary4, k, ScanRoot);
                    vaultObject.Index = k;
                    f.Objects.Add(vaultObject);
                }
                for (int l = 0; l < objs.Count; l++)
                {
                    List<FVRPhysicalObject> containedObjects = objs[l].GetContainedObjects();
                    if (containedObjects.Count > 0)
                    {
                        Logger.LogMessage(string.Concat(new object[] { "Object of index:", l, " has ", containedObjects.Count, " children" }));
                    }
                    for (int m = 0; m < containedObjects.Count; m++)
                    {
                        int num = dictionary[containedObjects[m]];
                        Logger.LogMessage(string.Concat(new object[] { "While writing file, Object of index:", num, " is contained in object of index", l }));
                        // Only has contained object point to its parent, no order information is added.
                        f.Objects[num].IsContainedIn = l;
                    }
                }
                for (int n = 0; n < objs.Count; n++)
                {
                    bool flag = false;
                    if (dictionary2.ContainsKey(objs[n]))
                    {
                        f.Objects[n].InSlotOfRootObjectIndex = dictionary2[objs[n]];
                        flag = true;
                    }
                    if (dictionary3.ContainsKey(objs[n]))
                    {
                        f.Objects[n].InSlotOfElementIndex = dictionary3[objs[n]];
                        flag = true;
                    }
                    if (dictionary4.ContainsKey(objs[n]))
                    {
                        f.Objects[n].QuickbeltSlotIndex = dictionary4[objs[n]];
                        flag = true;
                    }
                    if (flag)
                    {
                        Logger.LogMessage(string.Concat(new object[]
                        {
                            objs[n].name,
                            " was encoded on root object index:",
                            f.Objects[n].InSlotOfRootObjectIndex,
                            " and element index:",
                            f.Objects[n].InSlotOfElementIndex,
                            " in slot index:",
                            f.Objects[n].QuickbeltSlotIndex
                        }));
                    }
                }
                for (int num2 = 0; num2 < objs.Count; num2++)
                {
                    if (objs[num2].QuickbeltSlot != null && GM.CurrentPlayerBody.IsSlotInternal(objs[num2].QuickbeltSlot))
                    {
                        int num3 = GM.CurrentPlayerBody.QBSlots_Internal.IndexOf(objs[num2].QuickbeltSlot);
                        Logger.LogMessage(string.Concat(new object[]
                        {
                            "Encoding Object:",
                            f.Objects[num2].Elements[0].ObjectID,
                            " into player internal quickbelt slot: ",
                            num3
                        }));
                        f.Objects[num2].QuickbeltSlotIndex = num3;
                    }
                }
                f.ModsUsed.Clear();
                for (int num4 = 0; num4 < f.Objects.Count; num4++)
                {
                    for (int num5 = 0; num5 < f.Objects[num4].Elements.Count; num5++)
                    {
                        string objectID = f.Objects[num4].Elements[num5].ObjectID;
                        FVRObject fvrobject = IM.OD[objectID];
                        string spawnedFromId = fvrobject.SpawnedFromId;
                        if (IM.HasSpawnedID(spawnedFromId))
                        {
                            ItemSpawnerID spawnerID = IM.GetSpawnerID(spawnedFromId);
                            string fromMod = spawnerID.FromMod;
                            if (fromMod != string.Empty && !f.ModsUsed.Contains(fromMod))
                            {
                                f.ModsUsed.Add(fromMod);
                            }
                        }
                    }
                }
                Logger.LogMessage("Printing VaultObjects in order after SAVE:");
                foreach (VaultObject vaultObject in f.Objects)
                {
                    Logger.LogMessage(vaultObject);
                }
                return false; // Skip vanilla method execution.
            }
		}

        [HarmonyPatch(typeof(VaultSystem))]
		[HarmonyPatch("SpawnVaultFileRoutine")]
		class VaultSystemSpawnVaultFileRoutineHook
		{
			static bool Prefixfix(IEnumerator __result, VaultFile f, Transform SpawnRelativeTo, bool spawnRelativeTo, 
                                                                bool decodeAsLoadout, bool clearScene, 
                                                                Vector3 spawnOffset, VaultSystem.ReturnObjectListDelegate del, 
                                                                bool displayFileLoad)
            {
                Logger.LogMessage("VaultSystem SpawnVaultFileRoutine Postfix HOOKED!");
                __result = SpawnVaultFileRoutine(f, SpawnRelativeTo, spawnRelativeTo, 
                                                    decodeAsLoadout, clearScene, 
                                                    spawnOffset, del, displayFileLoad);
                return false;
            }
            private static IEnumerator SpawnVaultFileRoutine(VaultFile f, Transform SpawnRelativeTo, bool spawnRelativeTo, 
                                                                bool decodeAsLoadout, bool clearScene, 
                                                                Vector3 spawnOffset, VaultSystem.ReturnObjectListDelegate del, 
                                                                bool displayFileLoad)
            {
                if (displayFileLoad)
                {
                    GM.IsAsyncLoading = true;
                }
                Logger.LogMessage("Initiating Spawn Routine for: " + f.Objects.Count + " root objects");
                if (displayFileLoad)
                {
                    GM.CurrentPlayerBody.ShowLoadingBar();
                }
                if (decodeAsLoadout)
                {
                    GM.CurrentPlayerBody.WipeQuickbeltContents();
                }
                yield return null;
                if (clearScene)
                {
                    VaultSystem.ClearExistingSaveableObjects(true);
                }
                yield return null;
                FVRPhysicalObject[] spawnedObjs = new FVRPhysicalObject[f.Objects.Count];
                if (decodeAsLoadout)
                {
                    string quickbeltLayoutName = f.QuickbeltLayoutName;
                    if (GM.CurrentPlayerBody.DoesQuickbeltNameExist(quickbeltLayoutName))
                    {
                        int num = 0;
                        if (GM.CurrentPlayerBody.SetQuickbeltBasedOnStringName(quickbeltLayoutName, out num))
                        {
                            GM.Options.QuickbeltOptions.QuickbeltPreset = num;
                            GM.Options.SaveToFile();
                        }
                    }
                }
                Dictionary<VaultElement, FVRPhysicalObject> dicElementToObj = new Dictionary<VaultElement, FVRPhysicalObject>();
                // Added ordered dictionary.
                OrderedDictionary dicObjectsContainedInIndex = new OrderedDictionary();
                for (int o = 0; o < f.Objects.Count; o++)
                {
                    VaultObject obj = f.Objects[o];
                    if (IM.OD.ContainsKey(obj.Elements[0].ObjectID))
                    {
                        if (IM.HasSpawnedID(IM.OD[obj.Elements[0].ObjectID].SpawnedFromId))
                        {
                            for (int e = 0; e < f.Objects[o].Elements.Count; e++)
                            {
                                if (IM.HasSpawnedID(IM.OD[obj.Elements[e].ObjectID].SpawnedFromId))
                                {
                                    yield return IM.OD[obj.Elements[e].ObjectID].GetGameObjectAsync();
                                }
                            }
                        }
                    }
                }
                for (int o2 = 0; o2 < f.Objects.Count; o2++)
                {
                    VaultObject obj2 = f.Objects[o2];
                    if (IM.OD.ContainsKey(obj2.Elements[0].ObjectID))
                    {
                        if (IM.HasSpawnedID(IM.OD[obj2.Elements[0].ObjectID].SpawnedFromId))
                        {
                            GameObject rootObj = null;
                            List<GameObject> toDealWith = new List<GameObject>();
                            FVRFireArm myGun = null;
                            List<int> validIndexes = new List<int>();
                            Dictionary<GameObject, VaultElement> dicGO = new Dictionary<GameObject, VaultElement>();
                            Dictionary<int, GameObject> dicByIndex = new Dictionary<int, GameObject>();
                            Dictionary<GameObject, int> dicByObj = new Dictionary<GameObject, int>();
                            List<AnvilCallback<GameObject>> callbackList = new List<AnvilCallback<GameObject>>();
                            for (int i = 0; i < obj2.Elements.Count; i++)
                            {
                                callbackList.Add(IM.OD[obj2.Elements[i].ObjectID].GetGameObjectAsync());
                            }
                            yield return callbackList;
                            for (int j = 0; j < obj2.Elements.Count; j++)
                            {
                                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(callbackList[j].Result);
                                Logger.LogMessage("Spawned " + gameObject.name);
                                if (j == 0)
                                {
                                    rootObj = gameObject;
                                }
                                FVRPhysicalObject component = gameObject.GetComponent<FVRPhysicalObject>();
                                dicGO.Add(gameObject, obj2.Elements[j]);
                                dicByIndex.Add(obj2.Elements[j].Index, gameObject);
                                dicByObj.Add(gameObject, obj2.Elements[j].Index);
                                dicElementToObj.Add(obj2.Elements[j], component);
                                component.SetGenericInts(obj2.Elements[j].GenericInts);
                                component.SetGenericStrings(obj2.Elements[j].GenericStrings);
                                component.SetGenericVector3s(obj2.Elements[j].GenericVector3s);
                                component.SetGenericRotations(obj2.Elements[j].GenericRotations);
                                component.ConfigureFromFlagDic(obj2.Elements[j].Flags.dictionary);
                                if (j == 0)
                                {
                                    validIndexes.Add(j);
                                }
                                if (obj2.Elements[j].Type == "firearm")
                                {
                                    myGun = gameObject.GetComponent<FVRFireArm>();
                                    Vector3 vector = Vector3.zero;
                                    if (SpawnRelativeTo != null)
                                    {
                                        vector = SpawnRelativeTo.position;
                                    }
                                    gameObject.transform.position = vector;
                                    gameObject.transform.rotation = Quaternion.identity;
                                    if (myGun.Magazine != null && myGun.Magazine.IsIntegrated)
                                    {
                                        myGun.Magazine.ReloadMagWithList(obj2.Elements[j].LoadedRoundsInMag);
                                    }
                                    myGun.SetLoadedChambers(obj2.Elements[j].LoadedRoundsInChambers);
                                }
                                else if (obj2.Elements[j].Type == "magazine")
                                {
                                    toDealWith.Add(gameObject);
                                    FVRFireArmMagazine component2 = gameObject.GetComponent<FVRFireArmMagazine>();
                                    component2.ReloadMagWithList(obj2.Elements[j].LoadedRoundsInMag);
                                }
                                else if (obj2.Elements[j].Type == "attachment")
                                {
                                    toDealWith.Add(gameObject);
                                }
                                else if (gameObject.GetComponent<Speedloader>() != null && obj2.Elements[j].LoadedRoundsInMag.Count > 0)
                                {
                                    Speedloader component3 = gameObject.GetComponent<Speedloader>();
                                    component3.ReloadSpeedLoaderWithList(obj2.Elements[j].LoadedRoundsInMag);
                                }
                                else if (gameObject.GetComponent<FVRFireArmClip>() != null && obj2.Elements[j].LoadedRoundsInMag.Count > 0)
                                {
                                    FVRFireArmClip component4 = gameObject.GetComponent<FVRFireArmClip>();
                                    component4.ReloadClipWithList(obj2.Elements[j].LoadedRoundsInMag);
                                }
                            }
                            int BreakIterator = 400;
                            while (toDealWith.Count > 0 && BreakIterator > 0)
                            {
                                BreakIterator--;
                                for (int k = toDealWith.Count - 1; k >= 0; k--)
                                {
                                    VaultElement vaultElement = dicGO[toDealWith[k]];
                                    if (validIndexes.Contains(vaultElement.ObjectAttachedTo))
                                    {
                                        GameObject gameObject2 = toDealWith[k];
                                        if (gameObject2.GetComponent<FVRFireArmAttachment>() != null)
                                        {
                                            FVRFireArmAttachment component5 = gameObject2.GetComponent<FVRFireArmAttachment>();
                                            FVRFireArmAttachmentMount mount = VaultSystem.GetMount(dicByIndex[vaultElement.ObjectAttachedTo], vaultElement.MountAttachedTo);
                                            gameObject2.transform.rotation = Quaternion.LookRotation(vaultElement.OrientationForward, vaultElement.OrientationUp);
                                            gameObject2.transform.position = VaultSystem.GetPositionRelativeToGun(vaultElement, rootObj.transform);
                                            if (component5.CanScaleToMount && mount.CanThisRescale())
                                            {
                                                component5.ScaleToMount(mount);
                                            }
                                            component5.AttachToMount(mount, false);
                                            if (component5 is Suppressor)
                                            {
                                                (component5 as Suppressor).AutoMountWell();
                                            }
                                            validIndexes.Add(vaultElement.Index);
                                            toDealWith.RemoveAt(k);
                                        }
                                        else if (gameObject2.GetComponent<FVRFireArmMagazine>() != null)
                                        {
                                            FVRFireArmMagazine component6 = gameObject2.GetComponent<FVRFireArmMagazine>();
                                            GameObject gameObject3 = dicByIndex[vaultElement.ObjectAttachedTo];
                                            FVRFireArm component7 = gameObject3.GetComponent<FVRFireArm>();
                                            AttachableFirearmPhysicalObject component8 = gameObject3.GetComponent<AttachableFirearmPhysicalObject>();
                                            VaultElement vaultElement2 = dicGO[gameObject2];
                                            if (vaultElement2.MountAttachedTo < 0)
                                            {
                                                if (component7 != null)
                                                {
                                                    component6.transform.position = component7.GetMagMountPos(component6.IsBeltBox).position;
                                                    component6.transform.rotation = component7.GetMagMountPos(component6.IsBeltBox).rotation;
                                                    component6.Load(component7);
                                                }
                                                if (component8 != null)
                                                {
                                                    component6.transform.position = component8.FA.MagazineMountPos.position;
                                                    component6.transform.rotation = component8.FA.MagazineMountPos.rotation;
                                                    component6.Load(component8.FA);
                                                }
                                            }
                                            else if (component7 != null)
                                            {
                                                component6.transform.position = component7.SecondaryMagazineSlots[vaultElement2.MountAttachedTo].MagazineMountPos.position;
                                                component6.transform.rotation = component7.SecondaryMagazineSlots[vaultElement2.MountAttachedTo].MagazineMountPos.rotation;
                                                component6.LoadIntoSecondary(component7, vaultElement2.MountAttachedTo);
                                            }
                                            toDealWith.RemoveAt(k);
                                        }
                                    }
                                }
                            }
                            if (spawnRelativeTo)
                            {
                                rootObj.transform.position = SpawnRelativeTo.TransformPoint(obj2.Elements[0].PosOffset) + spawnOffset;
                                rootObj.transform.rotation = Quaternion.LookRotation(SpawnRelativeTo.TransformDirection(obj2.Elements[0].OrientationForward), SpawnRelativeTo.TransformDirection(obj2.Elements[0].OrientationUp));
                            }
                            else if (decodeAsLoadout)
                            {
                                rootObj.transform.position = SpawnRelativeTo.position + Vector3.up * 0.3f * (float)o2;
                                rootObj.transform.rotation = SpawnRelativeTo.rotation;
                            }
                            else if (!decodeAsLoadout)
                            {
                                rootObj.transform.position = obj2.Elements[0].PosOffset;
                                rootObj.transform.rotation = Quaternion.LookRotation(obj2.Elements[0].OrientationForward, obj2.Elements[0].OrientationUp);
                            }
                            if (obj2.IsContainedIn > -1)
                            {
                                Logger.LogMessage(rootObj.gameObject.name + " is contained in index " + obj2.IsContainedIn);
                                rootObj.SetActive(false);
                                dicObjectsContainedInIndex.Add(rootObj, obj2.IsContainedIn);
                            }
                            spawnedObjs[obj2.Index] = rootObj.GetComponent<FVRPhysicalObject>();
                        }
                    }
                }
                yield return null;
                for (int l = 0; l < spawnedObjs.Length; l++)
                {
                    if (f.Objects[l].QuickbeltSlotIndex != -1)
                    {
                        if (f.Objects[l].InSlotOfRootObjectIndex == -1 && spawnedObjs[l].QuickbeltSlot == null && decodeAsLoadout)
                        {
                            Logger.LogMessage(spawnedObjs[l].gameObject.name + " is in a player internal quickbelt slot: " + f.Objects[l].QuickbeltSlotIndex);
                            spawnedObjs[l].SetQuickBeltSlot(GM.CurrentPlayerBody.QBSlots_Internal[f.Objects[l].QuickbeltSlotIndex]);
                        }
                        else if (spawnedObjs[l].QuickbeltSlot == null)
                        {
                            VaultElement vaultElement3 = f.Objects[f.Objects[l].InSlotOfRootObjectIndex].Elements[f.Objects[l].InSlotOfElementIndex];
                            FVRPhysicalObject fvrphysicalObject = dicElementToObj[vaultElement3];
                            FVRQuickBeltSlot fvrquickBeltSlot = fvrphysicalObject.Slots[f.Objects[l].QuickbeltSlotIndex];
                            spawnedObjs[l].SetQuickBeltSlot(fvrquickBeltSlot);
                        }
                    }
                }
                foreach (KeyValuePair<GameObject, int> keyValuePair in dicObjectsContainedInIndex)
                {
                    FVRPhysicalObject fvrphysicalObject2 = spawnedObjs[keyValuePair.Value];
                    fvrphysicalObject2.ContainOtherObject(keyValuePair.Key.GetComponent<FVRPhysicalObject>());
                    Logger.LogMessage("Container " + fvrphysicalObject2 
                                        + "we added " + keyValuePair.Key.GetComponent<FVRPhysicalObject>());
                }
                if (del != null)
                {
                    List<FVRPhysicalObject> list = new List<FVRPhysicalObject>();
                    for (int m = 0; m < spawnedObjs.Length; m++)
                    {
                        list.Add(spawnedObjs[m]);
                    }
                    Logger.LogMessage("Delegate found in spawn routine with " + spawnedObjs.Length + " possible objects spawned");
                    del(list);
                }
                GM.IsAsyncLoading = false;
                GM.CurrentPlayerBody.HideLoadingBar();
                yield break;
            }
		}


        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)

        internal new static ManualLogSource Logger { get; private set; }

    }
}
