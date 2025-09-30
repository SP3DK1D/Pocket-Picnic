// Assets/_Project/Editor/AddATTToPlist.cs
#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine; // <-- needed for Debug.Log

public static class AddATTToPlist
{
    // Runs after Unity generates the Xcode project
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        var root = plist.root;

        const string key = "NSUserTrackingUsageDescription";
        const string value = "We use this to show relevant ads and support the game.";

        // add or overwrite
        root.SetString(key, value);

        File.WriteAllText(plistPath, plist.WriteToString());
        Debug.Log("[PostBuild] Added/updated NSUserTrackingUsageDescription in Info.plist");
    }
}
#endif
