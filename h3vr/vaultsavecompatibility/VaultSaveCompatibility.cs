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
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Specialized;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.VaultSaveCompatibility", "VaultSaveCompatibility", "0.0.1")]
    [BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public partial class VaultSaveCompatibility : BaseUnityPlugin
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

        public static string raid_extracted_loadout_filename = "Loadout_Extracted_From_Raid";
        public static string safehouse_persistent_filename = "Safehouse_Persitent_Filename";
        public static string safehouse_deployment_loadout_filename = "Safehouse_Deployment_Filename";
        public static string safehouse_scene_name = "GP_Hangar";
        public static bool extracting_to_safehouse_need_loadout = false;
        public static bool extracting_to_safehouse_need_map = false;
        
        public struct MyWristButton {
            public Action<object> onclick;
            public string name;
            public int priority;
            public MyWristButton(string name_, int priority_, Action<object> clickAction = null) {
                onclick = clickAction;
                name = name_;
                priority = priority_;
            }
        } 
        
        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.VaultSaveCompatibility");

			harmony.PatchAll();
            
            // Your plugin's ID, Name, and Version are available here.
            Logger.LogMessage($"Hello, world! Sent from NGA.VaultSaveCompatibility 0.0.1");
            
        }

        [HarmonyPatch(typeof(VaultSystem))]
		[HarmonyPatch("SpawnVaultFile")]
		class VaultSystemSpawnVaultFileHook
		{
			static bool Prefix(ref bool __result, VaultFile f, Transform SpawnRelativeTo, bool spawnRelativeTo, 
                                bool decodeAsLoadout, bool clearScene, out string ErrorMessage, Vector3 spawnOffset, 
                                VaultSystem.ReturnObjectListDelegate del = null, bool displayFileLoad = false)
            {
                ErrorMessage = string.Empty;
                Debug.Log(string.Concat(new object[] { "SpawnRelativeTo", spawnRelativeTo, " ", spawnOffset }));
                if (f.Objects.Count < 1)
                {
                    ErrorMessage = "No Objects Found in file";
                    Logger.LogMessage("No Objects Found in file");
                    __result = false;
                }
                if (decodeAsLoadout && !GM.CurrentPlayerBody.DoesQuickbeltNameExist(f.QuickbeltLayoutName))
                {
                    ErrorMessage = "Quickbelt Layout Not Found, Cannot Spawn";
                    Logger.LogMessage("Quickbelt Layout Not Found, Cannot Spawn");
                    __result = false;
                }
                List<int> objectIndexesToRemove = new List<int>();
                for (int i = 0; i < f.Objects.Count; i++)
                {
                    List<int> elementIndexesToRemove = new List<int>();
                    for (int j = 0; j < f.Objects[i].Elements.Count; j++)
                    {
                        string objectID = f.Objects[i].Elements[j].ObjectID;
                        if (!IM.OD.ContainsKey(objectID))
                        {
                            // ErrorMessage = "Cannot find FVRObject with ID: " + objectID + " and thus cannot spawn file";
                            Logger.LogMessage("Cannot find FVRObject with ID: " + objectID 
                                                + " and thus cannot spawn file");
                            elementIndexesToRemove.Add(j);
                            continue;
                            //__result = false;
                        }
                        if (!IM.HasSpawnedID(IM.OD[objectID].SpawnedFromId))
                        {
                            // ErrorMessage = "Cannot find ItemSpawnerID with ID: " + IM.OD[objectID].SpawnedFromId 
                            //                 + " and thus cannot spawn file";
                            Logger.LogMessage("Cannot find ItemSpawnerID with ID: " + IM.OD[objectID].SpawnedFromId 
                                            + " and thus cannot spawn file");
                            elementIndexesToRemove.Add(j);
                            //__result = false;
                        }
                    }
                    // Remove all Elements that had either of the two errors above.
                    for (int j = elementIndexesToRemove.Count - 1; j >= 0; j--)
                    {
                        int index = elementIndexesToRemove[j];
                        f.Objects[i].Elements.RemoveAt(index);
                    }
                    // If all child elements are bad and deleted, add index to get rid of the object.
                    if (f.Objects[i].Elements.Count < 1) {
                        objectIndexesToRemove.Add(i);
                    }
                }
                // Remove object entirely if all its elements are gone.
                for (int j = objectIndexesToRemove.Count - 1; j >= 0; j--)
                {
                    int index = objectIndexesToRemove[j];
                    f.Objects.RemoveAt(index);
                }
                objectIndexesToRemove.Clear(); // Not used below anymore.


                for (int k = 0; k < f.Objects.Count; k++)
                {
                    List<int> elementIndexesToRemove = new List<int>();
                    for (int l = 0; l < f.Objects[k].Elements.Count; l++)
                    {
                        string objectID2 = f.Objects[k].Elements[l].ObjectID;
                        FVRObject fvrobject = IM.OD[objectID2];
                        FireArmRoundType roundType = fvrobject.RoundType;

                        List<int> chamberRoundsIndexesToRemove = new List<int>();
                        for (int m = 0; m < f.Objects[k].Elements[l].LoadedRoundsInChambers.Count; m++)
                        {
                            if (!AM.DoesClassExistForType(f.Objects[k].Elements[l].LoadedRoundsInChambers[m], roundType))
                            {
                                // ErrorMessage = "Cannot find round of type: " + roundType.ToString() + " and class: " 
                                //                 + f.Objects[k].Elements[l].LoadedRoundsInChambers[m].ToString();
                                Logger.LogMessage("Cannot find round of type: " + roundType.ToString() + " and class: " 
                                                    + f.Objects[k].Elements[l].LoadedRoundsInChambers[m].ToString());
                                chamberRoundsIndexesToRemove.Add(m);
                                // __result = false;
                            }
                        }
                        // Remove all rounds in chambers which were broken.
                        for (int m = chamberRoundsIndexesToRemove.Count - 1; m >= 0; m--)
                        {
                            int index = chamberRoundsIndexesToRemove[m];
                            f.Objects[k].Elements[l].LoadedRoundsInChambers.RemoveAt(index);
                        }

                        List<int> magRoundsIndexesToRemove = new List<int>();
                        for (int n = 0; n < f.Objects[k].Elements[l].LoadedRoundsInMag.Count; n++)
                        {
                            if (!AM.DoesClassExistForType(f.Objects[k].Elements[l].LoadedRoundsInMag[n], roundType))
                            {
                                // ErrorMessage = "Cannot find round of type: " + roundType.ToString() 
                                //                 + " and class: " + f.Objects[k].Elements[l].LoadedRoundsInMag[n].ToString();
                                Logger.LogMessage("Cannot find round of type: " + roundType.ToString() 
                                                     + " and class: " + f.Objects[k].Elements[l].LoadedRoundsInMag[n].ToString());
                                magRoundsIndexesToRemove.Add(n);
                                // __result = false;
                            }
                        }
                        // Remove all rounds in mags which are broken.
                        for (int m = magRoundsIndexesToRemove.Count - 1; m >= 0; m--)
                        {
                            int index = magRoundsIndexesToRemove[m];
                            f.Objects[k].Elements[l].LoadedRoundsInMag.RemoveAt(index);
                        }
                    }
                }

                // Normalizes all index values at the object and nested element level after deletion.
                for (int i = 0; i < f.Objects.Count; i++)
                {
                    f.Objects[i].Index = i;
                    for (int j = 0; j < f.Objects[i].Elements.Count; j++)
                    {
                        f.Objects[i].Elements[j].Index = j;
                    }
                }

                AnvilManager.Run(VaultSystem.SpawnVaultFileRoutine(f, SpawnRelativeTo, spawnRelativeTo, decodeAsLoadout, 
                                                                    clearScene, spawnOffset, del, displayFileLoad));
                __result = true;
                return false; // Skip vanilla method execution.
            }
        }

        [HarmonyPatch(typeof(VaultSystem))]
		[HarmonyPatch("GetMount")]
		class VaultSystemGetMountHook
		{
            static bool Prefix(ref FVRFireArmAttachmentMount __result, GameObject g, int index) {
                Logger.LogMessage("GetMount HOOKED!");
                if (!g) {
                    Logger.LogMessage("g null!");
                    __result = null;
                    return false;
                }
                if (!g.GetComponent<FVRPhysicalObject>()) {
                    Logger.LogMessage("g.GetComponent<FVRPhysicalObject>() null!");
                    __result = null;
                    return false;
                }
                if (g.GetComponent<FVRPhysicalObject>().AttachmentMounts == null) {
                    Logger.LogMessage("g.GetComponent<FVRPhysicalObject>().AttachmentMounts null!");
                    __result = null;
                    return false;
                }
                if (index > g.GetComponent<FVRPhysicalObject>().AttachmentMounts.Count-1) {
                    Logger.LogMessage("Mount index out of range");
                    __result = null;
                } else {
                    Logger.LogMessage("Good!");
                    __result = g.GetComponent<FVRPhysicalObject>().AttachmentMounts[index];
                }
                return false; // skip original
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
                return false; // skip original
            }
            private static IEnumerator SpawnVaultFileRoutine(VaultFile f, Transform SpawnRelativeTo, bool spawnRelativeTo, 
                                                                bool decodeAsLoadout, bool clearScene, 
                                                                Vector3 spawnOffset, VaultSystem.ReturnObjectListDelegate del, 
                                                                bool displayFileLoad)
            {
                Logger.LogInfo("BASIC");
                if (displayFileLoad)
                {
                    GM.IsAsyncLoading = true;
                }
                Debug.Log("Initiating Spawn Routine for: " + f.Objects.Count + " root objects");
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
                Dictionary<GameObject, int> dicObjectsContainedInIndex = new Dictionary<GameObject, int>();
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
                                Debug.Log("Spawned " + gameObject.name);
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
                                            Logger.LogInfo("0");
                                            gameObject2.transform.rotation = Quaternion.LookRotation(vaultElement.OrientationForward, vaultElement.OrientationUp);
                                            Logger.LogInfo("0.1");
                                            gameObject2.transform.position = VaultSystem.GetPositionRelativeToGun(vaultElement, rootObj.transform);
                                            Logger.LogInfo("0.2");
                                            // TODO
                                            if (mount) {
                                            } else {
                                                toDealWith.RemoveAt(k);
                                                Logger.LogInfo("Bad mount!!");
                                                continue;
                                            }
                                            Logger.LogInfo("1");
                                            if (component5.CanScaleToMount && mount.CanThisRescale())
                                            {
                                                Logger.LogInfo("2");
                                                component5.ScaleToMount(mount);
                                                Logger.LogInfo("2.5");
                                            }
                                            Logger.LogInfo("3");
                                            component5.AttachToMount(mount, false);
                                            Logger.LogInfo("4");
                                            if (component5 is Suppressor)
                                            {
                                                Logger.LogInfo("5");
                                                (component5 as Suppressor).AutoMountWell();
                                                Logger.LogInfo("5.5");
                                            }
                                            Logger.LogInfo("6");
                                            validIndexes.Add(vaultElement.Index);
                                            Logger.LogInfo("7");
                                            toDealWith.RemoveAt(k);
                                            Logger.LogInfo("8");
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
                                Debug.Log(rootObj.gameObject.name + " is contained in index " + obj2.IsContainedIn);
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
                            Debug.Log(spawnedObjs[l].gameObject.name + " is in a player internal quickbelt slot: " + f.Objects[l].QuickbeltSlotIndex);
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
                }
                if (del != null)
                {
                    List<FVRPhysicalObject> list = new List<FVRPhysicalObject>();
                    for (int m = 0; m < spawnedObjs.Length; m++)
                    {
                        list.Add(spawnedObjs[m]);
                    }
                    Debug.Log("Delegate found in spawn routine with " + spawnedObjs.Length + " possible objects spawned");
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
