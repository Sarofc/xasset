using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Saro.XAsset.Build
{
    public static partial class BuildMethods
    {
        //[BuildMethod(0, "Clear AssetBundleNames", false)]
        //private static void ClearAssetBundles()
        //{
        //    BuildScript.ClearAssetBundles();
        //    Debug.Log("[XAsset] ClearAssetBundles");
        //}

        [BuildMethod(1, "Build Rules", false)]
        private static void ApplyBuildRules()
        {
            var watch = new Stopwatch();
            watch.Start();
            BuildScript.ApplyBuildRules();
            watch.Stop();
            Debug.Log("[XAsset] ApplyBuildRules " + watch.ElapsedMilliseconds + " ms.");
        }

        //[BuildMethod(2, "Build Manifest")]
        //private static void BuildManifest()
        //{
        //    BuildScript.BuildManifest();
        //    Debug.Log("[XAsset] Build Manifest...");
        //}

        [BuildMethod(4, "Build AssetBundles")]
        private static void BuildAssetBundles()
        {
            var watch = new Stopwatch();
            watch.Start();
            //BuildScript.ApplyBuildRules();
            BuildScript.BuildAssetBundles();
            watch.Stop();
            Debug.Log("[XAsset] BuildAssetBundles " + watch.ElapsedMilliseconds + " ms.");
        }

        [BuildMethod(5, "Copy AssetBundles")]
        private static void CopyAssetBundles()
        {
            BuildScript.CopyAssetBundlesTo(Application.streamingAssetsPath);
            AssetDatabase.Refresh();
            Debug.Log("[XAsset] Copy AssetBundles to SreammingFolder");
        }

        [BuildMethod(50, "Build Player")]
        private static void BuildPlayer()
        {
            BuildScript.BuildPlayer();
        }
    }
}