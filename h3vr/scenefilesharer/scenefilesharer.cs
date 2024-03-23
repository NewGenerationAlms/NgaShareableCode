using System;
using System.IO;
using BepInEx;

namespace NGA
{
	[BepInPlugin("NGA.SceneFileSharer", "SceneFileSharer", "1.0.0")]
    [BepInProcess("h3vr.exe")]
	public class SceneFileSharer : BaseUnityPlugin
	{
		private void Awake()
        {
            base.Logger.LogInfo("SceneFileSharer starting work!");

            // Get all folders inside Paths.PluginPath
            string pluginsPath = Paths.PluginPath;
            base.Logger.LogInfo("pluginsPath: " + pluginsPath);
            string[] pluginFolders = Directory.GetDirectories(pluginsPath);
            foreach (string pluginFolder in pluginFolders)
            {
                string[] files = Directory.GetFiles(pluginFolder, "shared_*.json");
                if (files.Length > 0) {base.Logger.LogInfo("pluginFolder: " + pluginFolder);}
                foreach (string filePath in files)
                {
                    // Read JSON file and extract ReferencePath value dynamically
                    string jsonContent = File.ReadAllText(filePath);
                    string referencePath = GetJsonValue(jsonContent, "ReferencePath");
                    base.Logger.LogInfo("referencePath: " + referencePath);

                    // Extract folder name from ReferencePath
                    string[] pathSegments = referencePath.Split('\\');
                    string sceneName = pathSegments[4]; // it's the third item
                    base.Logger.LogInfo("sceneName: " + sceneName);
                    
                    // Create scene configs path.
                    string sceneConfigsPath = "\\My Games\\H3VR\\Vault\\SceneConfigs\\" + sceneName;
                    base.Logger.LogInfo("sceneConfigsPath: " + sceneConfigsPath);
                    string fullSceneConfigsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                                                    + sceneConfigsPath;
                    base.Logger.LogInfo("fullSceneConfigsPath: " + fullSceneConfigsPath);

                    // Construct destination path
                    string jsonFullFilePath;
                    string jsonFileName;
                    jsonFileName = Path.GetFileName(filePath);
                    base.Logger.LogInfo("jsonFileName: " + jsonFileName);
                    jsonFullFilePath = filePath;
                    base.Logger.LogInfo("jsonFullFilePath: " + jsonFullFilePath);
                    string destinationFilePath = Path.Combine(fullSceneConfigsPath, jsonFileName);
                    base.Logger.LogInfo("destinationFilePath: " + destinationFilePath);

                    // Copy json to destination, creating scene directory if needed.
                    bool h3vrSceneConfigsPathExists = Directory.Exists(fullSceneConfigsPath);
                    if (h3vrSceneConfigsPathExists)
                    {
                        File.Copy(jsonFullFilePath, destinationFilePath, true);
                        base.Logger.LogInfo("Copied " + jsonFullFilePath + " to " + destinationFilePath);
                    }
                    else
                    {
                        Directory.CreateDirectory(fullSceneConfigsPath);
                        base.Logger.LogInfo("Created new directory and file " + fullSceneConfigsPath);
                        File.Copy(jsonFullFilePath, destinationFilePath, true);
                        base.Logger.LogInfo("Then Copied " + jsonFullFilePath + " to " + destinationFilePath);
                    }
                }
            }

            base.Logger.LogInfo("SceneFileSharer ended work!");
        }

        private string GetJsonValue(string json, string key)
        {
            int startIndex = json.IndexOf($"\"{key}\": ") + key.Length + 4;
            int endIndex = json.IndexOf(',', startIndex);
            if (endIndex == -1)
            {
                base.Logger.LogInfo("Error, bad file, couldn't find comma.");
                endIndex = json.IndexOf('}', startIndex);
            }

            return json.Substring(startIndex, endIndex - startIndex - 1).Trim('\"');
        }
	}
}
