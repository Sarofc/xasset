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
        [MenuItem("Assets/Apply Rule/Text", false, 1)]
        private static void ApplyRuleText()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternText);
        }

        [MenuItem("Assets/Apply Rule/Prefab", false, 1)]
        private static void ApplyRulePrefab()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternPrefab);
        }

        [MenuItem("Assets/Apply Rule/PNG", false, 1)]
        private static void ApplyRulePNG()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternPng);
        }

        [MenuItem("Assets/Apply Rule/Material", false, 1)]
        private static void ApplyRuleMaterail()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternMaterial);
        }

        [MenuItem("Assets/Apply Rule/Controller", false, 1)]
        private static void ApplyRuleController()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternController);
        }

        [MenuItem("Assets/Apply Rule/Asset", false, 1)]
        private static void ApplyRuleAsset()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternAsset);
        }

        [MenuItem("Assets/Apply Rule/Scene", false, 1)]
        private static void ApplyRuleScene()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternScene);
        }

        [MenuItem("Assets/Apply Rule/Dir", false, 1)]
        private static void ApplyRuleDir()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternDir);
        }

        private static void AddRulesForSelection(BuildRules rules, string searchPattern)
        {
            var isDir = rules.searchPatternDir.Equals(searchPattern);
            foreach (var item in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(item);
                var rule = new BuildRule
                {
                    searchPath = path,
                    searchPattern = searchPattern,
                    nameBy = isDir ? NameBy.Directory : NameBy.Path
                };
                ArrayUtility.Add(ref rules.rules, rule);
            }

            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssets();
        }

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