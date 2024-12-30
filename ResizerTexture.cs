#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEditor;
using UnityEngine;

public class ResizerTexture : EditorWindow
{
    private const string OutputFolderName = "ForConsoleTextures";

    private const string CurrentVersion = "1.0.7";
    private const string VersionUrl = "https://raw.githubusercontent.com/Jeefrect/TextureResizerUnity/main/version.txt";
    private const string ScriptUrl = "https://raw.githubusercontent.com/Jeefrect/TextureResizerUnity/main/ResizerTexture.cs";
    private const string LocalScriptPath = "Assets/Editor/ResizerTexture.cs";
    private static bool updateChecked = false;

    [MenuItem("Tools/Compress Textures in Scene/512px")]
    public static void CompressTexturesInScene512()
    {
        CompressTexturesInScene(512);
    }

    [MenuItem("Tools/Compress Textures in Scene/1024px")]
    public static void CompressTexturesInScene1024()
    {
        CompressTexturesInScene(1024);
    }

    public static void CompressTexturesInScene(int maxSize)
    {
        CheckForScriptUpdateOnce();

        string outputFolderPath = Path.Combine(UnityEngine.Application.dataPath, $"{OutputFolderName}_{maxSize}px");
        if (Directory.Exists(outputFolderPath))
            Directory.Delete(outputFolderPath, true);
        Directory.CreateDirectory(outputFolderPath);

        AssetDatabase.Refresh();

        var textures = FindTexturesInScene();

        foreach (var texturePath in textures)
        {
            Texture2D originalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (originalTexture == null) continue;

            TextureImporterType originalType = EnsureTextureTypeIsDefault(texturePath);

            if (originalTexture.width > maxSize || originalTexture.height > maxSize)
            {
                Texture2D resizedTexture = ResizeTexture(originalTexture, maxSize);
                SaveResizedTexture(resizedTexture, texturePath, outputFolderPath);
                UnityEngine.Object.DestroyImmediate(resizedTexture);
            }

            RestoreTextureType(texturePath, originalType);
        }

        CreateZipArchive(outputFolderPath);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Complete", $"Textures have been compressed and saved in folder: {OutputFolderName}_{maxSize}px.", "OK");
    }

    private static void CheckForScriptUpdateOnce()
    {
        if (updateChecked) return;
        updateChecked = true;

        try
        {
            string latestVersion = GetLatestVersion();
            if (latestVersion != CurrentVersion)
            {
                if (EditorUtility.DisplayDialog("Update Available",
                    $"A new version ({latestVersion}) of the script is available. Do you want to update?",
                    "Yes", "No"))
                {
                    DownloadLatestScript();
                    EditorUtility.DisplayDialog("Update Complete", "The script has been updated. Please restart Unity to apply changes if the script has not changed :).", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error checking for updates: {ex.Message}");
        }
    }

    private static string GetLatestVersion()
    {
        using (WebClient client = new WebClient())
        {
            return client.DownloadString(VersionUrl).Trim();
        }
    }

    private static void DownloadLatestScript()
    {
        try
        {
            string executingScriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(CreateInstance<ResizerTexture>()));

            if (string.IsNullOrEmpty(executingScriptPath))
            {
                UnityEngine.Debug.LogError("Unable to determine the path of the current script.");
                return;
            }

            using (WebClient client = new WebClient())
            {
                string scriptContent = client.DownloadString(ScriptUrl);
                string fullPath = Path.Combine(UnityEngine.Application.dataPath, executingScriptPath.Substring("Assets/".Length));

                File.WriteAllText(fullPath, scriptContent);
                UnityEngine.Debug.Log($"Script updated at path: {fullPath}");
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Script updated and Unity refreshed without restart.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error updating script: {ex.Message}");
        }
    }

    private static List<string> FindTexturesInScene()
    {
        var textures = new HashSet<string>();
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                foreach (var textureName in mat.GetTexturePropertyNames())
                {
                    var texture = mat.GetTexture(textureName);
                    if (texture is Texture2D texture2D)
                    {
                        string path = AssetDatabase.GetAssetPath(texture2D);
                        if (!string.IsNullOrEmpty(path))
                            textures.Add(path);
                    }
                }
            }
        }

        return new List<string>(textures);
    }

    private static TextureImporterType EnsureTextureTypeIsDefault(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            TextureImporterType originalType = importer.textureType;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importer.SaveAndReimport();
            }
            return originalType;
        }
        return TextureImporterType.Default;
    }

    private static void RestoreTextureType(string texturePath, TextureImporterType originalType)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null && importer.textureType != originalType)
        {
            importer.textureType = originalType;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D ResizeTexture(Texture2D originalTexture, int maxSize)
    {
        int newWidth, newHeight;

        if (originalTexture.width > originalTexture.height)
        {
            newWidth = maxSize;
            newHeight = Mathf.RoundToInt((float)originalTexture.height / originalTexture.width * maxSize);
        }
        else
        {
            newHeight = maxSize;
            newWidth = Mathf.RoundToInt((float)originalTexture.width / originalTexture.height * maxSize);
        }

        RenderTexture renderTexture = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(originalTexture, renderTexture);

        RenderTexture.active = renderTexture;
        Texture2D resizedTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);

        return resizedTexture;
    }

    private static void SaveResizedTexture(Texture2D resizedTexture, string originalPath, string outputFolderPath)
    {
        string relativePath = originalPath.Substring("Assets/".Length);
        string newPath = Path.Combine(outputFolderPath, relativePath);
        string directory = Path.GetDirectoryName(newPath);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        byte[] pngData = resizedTexture.EncodeToPNG();
        File.WriteAllBytes(newPath, pngData);
    }

    private static void CreateZipArchive(string folderPath)
    {
        string zipPath = folderPath + ".zip";
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(folderPath, zipPath);
        UnityEngine.Debug.Log("Zip archive created at: " + zipPath);
    }
}
#endif
