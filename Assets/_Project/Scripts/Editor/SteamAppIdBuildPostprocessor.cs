using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class SteamAppIdBuildPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result != BuildResult.Succeeded)
        {
            return;
        }

        string outputPath = report.summary.outputPath;
        string outputDirectory = Directory.Exists(outputPath)
            ? outputPath
            : Path.GetDirectoryName(outputPath);

        if (string.IsNullOrEmpty(outputDirectory))
        {
            Debug.LogWarning(
                "SteamAppIdBuildPostprocessor could not determine the build directory.");
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        string appIdPath = Path.Combine(outputDirectory, "steam_appid.txt");
        File.WriteAllText(appIdPath, "480", Encoding.ASCII);

        Debug.Log($"Created Steam app ID file: {appIdPath}");
    }
}
