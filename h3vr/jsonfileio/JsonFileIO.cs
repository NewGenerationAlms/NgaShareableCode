using BepInEx;
using BepInEx.Logging;
using FistVR;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.IO;
using System;

namespace NGA
{
    //[BepInAutoPlugin]
    [BepInPlugin("NGA.JsonFileIO", "JsonFileIO", "0.0.1")]
    [BepInProcess("h3vr.exe")]
    public partial class JsonSaveSystem : BaseUnityPlugin
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

        private void Awake()
        {
            Logger = base.Logger;

            Harmony harmony = new Harmony("NGA.JsonSaveSystem");
            Logger.LogMessage("New harmony");
            // SetUpConfigFields();
            // Logger.LogMessage("Setted the fields");
            harmony.PatchAll();
            Logger.LogMessage($"Hello, world! Sent from NGA.JsonSaveSystem 0.0.1");
        }

        // Assigns player-set variables.
        private void SetUpConfigFields()
        {
        }

        [Serializable]
        public class ExtensibleDictionary
        {
            [Serializable]
            public class DictionaryEntry
            {
                public string key;
                public string value;
            }

            public List<DictionaryEntry> entries = new List<DictionaryEntry>();
            public List<string> keys = new List<string>();

            public void InsertOrUpdateEntry(string key, string value)
            {
                if (!keys.Contains(key))
                {
                    entries.Add(new DictionaryEntry { key = key, value = value });
                    keys.Add(key);
                }
                else
                {
                    var existingEntry = entries.Find(entry => entry.key == key);
                    if (existingEntry != null)
                    {
                        existingEntry.value = value;
                    }
                    else
                    {
                        Logger.LogWarning("Failed to update value for key '" + key + "'.");
                    }
                }
            }
            
            public void AddEntry(string key, string value)
            {
                if (!keys.Contains(key))
                {
                    entries.Add(new DictionaryEntry { key = key, value = value });
                    keys.Add(key);
                }
                else
                {
                    Logger.LogWarning("Key '" + key + "' already exists in the dictionary.");
                }
            }

            public void RemoveEntry(string key)
            {
                entries.RemoveAll(entry => entry.key == key);
                keys.Remove(key);
            }

            public string GetValue(string key)
            {
                foreach (var entry in entries)
                {
                    if (entry.key == key)
                    {
                        return entry.value;
                    }
                }
                return null;
            }

            // Serialize the dictionary to JSON
            public string ToJson()
            {
                return JsonUtility.ToJson(this);
            }

            // Deserialize the dictionary from JSON
            public static ExtensibleDictionary FromJson(string json)
            {
                return JsonUtility.FromJson<ExtensibleDictionary>(json);
            }

            // Check if the dictionary contains a key
            public bool ContainsKey(string key)
            {
                return keys.Contains(key);
            }
        }

        public static class FileIO
        {
            // Get the path to the mod's folder within the current profile
            private static string GetModFolderPath(string myModFolderName)
            {
                if (string.IsNullOrEmpty(myModFolderName))
                {
                    Logger.LogError("My mod folder cannot be empty or null");
                    return null;
                }

                string modFolderPath = Paths.PluginPath + "/" + myModFolderName + "/";
                if (Directory.Exists(modFolderPath))
                {
                    return modFolderPath;
                }
                else
                {
                    Logger.LogError("Mod folder provided does not exist: " + modFolderPath);
                    return null;
                }
            }

            // Write string content to a file in the mod's folder
            public static void WriteToFile(string myModFolderName, string fileName, string content)
            {
                string modFolderPath = GetModFolderPath(myModFolderName);
                if (modFolderPath == null)
                {
                    Logger.LogError("WriteToFile: The mod folder is not valid in FileIO");
                    return;
                }
                string filePath = Path.Combine(modFolderPath, fileName);

                // Write the content to the file
                File.WriteAllText(filePath, content);
            }

            // Read the content of a file in the mod's folder
            public static string ReadFile(string myModFolderName, string fileName)
            {
                string modFolderPath = GetModFolderPath(myModFolderName);
                if (modFolderPath == null)
                {
                    Logger.LogError("ReadFile: The mod folder is not valid in FileIO");
                    return null;
                }
                string filePath = Path.Combine(modFolderPath, fileName);

                // Check if the file exists before reading
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
                else
                {
                    Logger.LogError("ReadFile: File not found: " + filePath);
                    return null;
                }
            }
        }

        internal new static ManualLogSource Logger { get; private set; }
    }
}
