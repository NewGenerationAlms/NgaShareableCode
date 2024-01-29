using System;
using System.IO;
using BepInEx;

namespace NGA
{
	[BepInPlugin("NGA.VaultGunSharer", "VaultGunSharer", "1.0.0")]
    [BepInProcess("h3vr.exe")]
	public class VaultGunSharer : BaseUnityPlugin
	{
		private void Awake()
        {
            base.Logger.LogInfo("VaultGunSharer starting work!");

            // Get all folders inside Paths.PluginPath
            string pluginsPath = Paths.PluginPath;
            base.Logger.LogInfo("pluginsPath: " + pluginsPath);
            string[] pluginFolders = Directory.GetDirectories(pluginsPath);
            foreach (string pluginFolder in pluginFolders)
            {
                string[] obj_files = Directory.GetFiles(pluginFolder, "gun_shared_*.json");
                if (obj_files.Length > 0) {base.Logger.LogInfo("pluginFolder: " + pluginFolder);}
                foreach (string filePath in obj_files)
                {
                    // Read JSON file and extract ReferencePath value dynamically
                    string jsonContent = File.ReadAllText(filePath);
                    string referencePath = GetJsonValue(jsonContent, "ReferencePath");
                    base.Logger.LogInfo("referencePath: " + referencePath);

                    // Extract folder name from ReferencePath
                    string[] pathSegments = referencePath.Split('\\');
                    if (pathSegments.Length < 5) {
                        base.Logger.LogInfo("SKIPPED ERROR on ABOVE");
                        continue;
                    }
                    string subfolderName = pathSegments[4]; // it's the third item
                    base.Logger.LogInfo("subfolderName: " + subfolderName);
                    
                    // Create  configs path.
                    string ConfigsPath = "\\My Games\\H3VR\\Vault\\Objects\\" + subfolderName;
                    base.Logger.LogInfo("ConfigsPath: " + ConfigsPath);
                    string fullConfigsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                                                    + ConfigsPath;
                    base.Logger.LogInfo("fullConfigsPath: " + fullConfigsPath);

                    // Construct destination path
                    string jsonFullFilePath;
                    string jsonFileName;
                    jsonFileName = Path.GetFileName(filePath);
                    base.Logger.LogInfo("jsonFileName: " + jsonFileName);
                    jsonFullFilePath = filePath;
                    base.Logger.LogInfo("jsonFullFilePath: " + jsonFullFilePath);
                    string destinationFilePath = Path.Combine(fullConfigsPath, jsonFileName);
                    base.Logger.LogInfo("destinationFilePath: " + destinationFilePath);

                    // Copy json to destination, creating  directory if needed.
                    bool h3vrConfigsPathExists = Directory.Exists(fullConfigsPath);
                    if (h3vrConfigsPathExists)
                    {
                        File.Copy(jsonFullFilePath, destinationFilePath, true);
                        base.Logger.LogInfo("Copied " + jsonFullFilePath + " to " + destinationFilePath);
                    }
                    else
                    {
                        Directory.CreateDirectory(fullConfigsPath);
                        base.Logger.LogInfo("Created new directory and file " + fullConfigsPath);
                        File.Copy(jsonFullFilePath, destinationFilePath, true);
                        base.Logger.LogInfo("Then Copied " + jsonFullFilePath + " to " + destinationFilePath);
                    }
                }
            }

            base.Logger.LogInfo("VaultGunSharer ended work!");
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
