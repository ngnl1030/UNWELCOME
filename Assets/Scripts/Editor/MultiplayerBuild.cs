using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class MultiplayerBuild
{
    public static void Begin()
    {
            Directory.CreateDirectory("Builds/MultiplayerTest");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName = "Builds/MultiplayerTest/MakeTrap.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Builds/MultiplayerTest/build-result.txt", report.summary.result + " errors=" + report.summary.totalErrors);
    }
}
