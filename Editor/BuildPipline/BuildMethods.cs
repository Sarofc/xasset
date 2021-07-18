using UnityEditor;
using UnityEngine;

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

        [XAssetBuildMethod(1, "Build Rules", false)]
        private static void ApplyBuildRules()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            BuildScript.ApplyBuildRules();
            watch.Stop();
            Debug.Log("[XAsset] ApplyBuildRules " + watch.ElapsedMilliseconds + " ms.");
        }

        [XAssetBuildMethod(4, "Build AssetBundles")]
        private static void BuildAssetBundles()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            BuildScript.BuildAssetBundles();
            watch.Stop();
            Debug.Log("[XAsset] BuildAssetBundles " + watch.ElapsedMilliseconds + " ms.");
        }

        [XAssetBuildMethod(5, "Copy AssetBundles")]
        private static void CopyAssetBundles()
        {
            var destFolder = Application.streamingAssetsPath + "/" + XAssetComponent.k_AssetBundles;
            BuildScript.CopyAssetBundlesTo(destFolder);
            AssetDatabase.Refresh();
            Debug.Log($"[XAsset] Copy AssetBundles to SreammingFolder: {destFolder}");
        }

        [XAssetBuildMethod(50, "Build Player")]
        private static void BuildPlayer()
        {
            BuildScript.BuildPlayer();
        }
    }
}