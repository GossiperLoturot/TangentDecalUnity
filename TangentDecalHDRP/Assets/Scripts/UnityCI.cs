#if UNITY_EDITOR
using System.Linq;
using UnityEditor;

public static class UnityCI
{
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Select(x => x.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/TangentDecalHDRP.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildPipeline.BuildPlayer(options);
    }
}
#endif
