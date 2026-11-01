using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

// Adds ProBuilder through the Package Manager the first time the project opens.
[InitializeOnLoad]
static class InstallProBuilder
{
    static InstallProBuilder()
    {
        if (File.ReadAllText("Packages/manifest.json").Contains("com.unity.probuilder")) return;
        Debug.Log("Installing ProBuilder...");
        Client.Add("com.unity.probuilder");
    }
}
