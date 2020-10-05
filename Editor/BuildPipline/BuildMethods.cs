//
// AssetsMenuItem.cs
//
// Author:
//       fjy <jiyuan.feng@live.com>
//
// Copyright (c) 2020 fjy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XAsset
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