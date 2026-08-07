using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class SteamAppIdBuildPostprocessor
{
    private const string SteamAppId = "480";

    [PostProcessBuild(1000)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        Debug.Log(
            $"SteamAppIdBuildPostprocessor started.\n" +
            $"Target: {target}\n" +
            $"Build path: {pathToBuiltProject}");

        string outputDirectory = Path.GetDirectoryName(pathToBuiltProject);

        if (string.IsNullOrEmpty(outputDirectory))
        {
            Debug.LogError(
                $"Could not determine build directory from: {pathToBuiltProject}");

            return;
        }

        string appIdPath = Path.Combine(outputDirectory, "steam_appid.txt");

        File.WriteAllText(
            appIdPath,
            SteamAppId,
            Encoding.ASCII);

        Debug.Log($"Created steam_appid.txt: {appIdPath}");
    }
}