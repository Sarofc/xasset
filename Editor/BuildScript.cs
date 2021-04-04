using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Saro.XAsset.Build
{
    public static class BuildScript
    {
        public static string outputPath = "ExtraResources/Res/" + GetPlatformName();

        public static void ClearAssetBundles()
        {
            var allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            for (var i = 0; i < allAssetBundleNames.Length; i++)
            {
                var text = allAssetBundleNames[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                                    string.Format("Clear AssetBundles {0}/{1}", i, allAssetBundleNames.Length), text,
                                    i * 1f / allAssetBundleNames.Length))
                    break;

                AssetDatabase.RemoveAssetBundleName(text, true);
            }
            EditorUtility.ClearProgressBar();
        }

        internal static void ApplyBuildRules()
        {
            var rules = GetXAssetBuildRules();
            rules.Apply();

            //var builds = rules.GetBuilds();

            //Debug.LogError("rule's builds: " + builds.Length);

            //var outputPath = CreateAssetBundleDirectory();
            //const BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;
            //var targetPlatform = EditorUserBuildSettings.activeBuildTarget;

            //var assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, builds, options, targetPlatform);
            //if (assetBundleManifest == null)
            //{
            //    return;
            //}
            //var manifest = GetManifest();
            //var dirs = new List<string>();
            //var assets = new List<AssetRef>();
            //var bundles = assetBundleManifest.GetAllAssetBundles();
            //var bundle2Ids = new Dictionary<string, int>();
            //for (var index = 0; index < bundles.Length; index++)
            //{
            //    var bundle = bundles[index];
            //    bundle2Ids[bundle] = index;
            //}

            //var bundleRefs = new List<BundleRef>();
            //for (var index = 0; index < bundles.Length; index++)
            //{
            //    var bundle = bundles[index];
            //    var deps = assetBundleManifest.GetAllDependencies(bundle);
            //    var path = string.Format("{0}/{1}", outputPath, bundle);
            //    if (File.Exists(path))
            //    {
            //        using (var stream = File.OpenRead(path))
            //        {
            //            bundleRefs.Add(new BundleRef
            //            {
            //                name = bundle,
            //                id = index,
            //                deps = Array.ConvertAll(deps, input => bundle2Ids[input]),
            //                len = stream.Length,
            //                hash = assetBundleManifest.GetAssetBundleHash(bundle).ToString(),
            //            });
            //        }
            //    }
            //    else
            //    {
            //        Debug.LogError(path + " file not exsit.");
            //    }
            //}

            //for (var i = 0; i < rules.ruleAssets.Length; i++)
            //{
            //    var item = rules.ruleAssets[i];
            //    var path = item.path;
            //    var dir = Path.GetDirectoryName(path).Replace("\\", "/");
            //    var index = dirs.FindIndex(o => o.Equals(dir));
            //    if (index == -1)
            //    {
            //        index = dirs.Count;
            //        dirs.Add(dir);
            //    }

            //    var asset = new AssetRef { bundle = bundle2Ids[item.bundle], dir = index, name = Path.GetFileName(path) };
            //    assets.Add(asset);
            //}

            //manifest.dirs = dirs.ToArray();
            //manifest.assets = assets.ToArray();
            //manifest.bundles = bundleRefs.ToArray();

            //EditorUtility.SetDirty(manifest);
            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();
        }

        public static void CopyAssetBundlesTo(string path)
        {
            var files = new HashSet<string>
            {
                Version.Dataname,
                Version.VersionFileName,
            };
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //if (GetSettings().enableVFS)
            //{
            //    foreach (var item in files)
            //    {
            //        var src = outputPath + "/" + item;
            //        var dest = Application.streamingAssetsPath + "/" + item;
            //        if (File.Exists(src))
            //        {
            //            File.Copy(src, dest, true);
            //        }
            //    }
            //}
            //else
            {
                var _files = Directory.GetFiles(outputPath);
                foreach (var src in _files)
                {
                    var fileName = Path.GetFileName(src);

                    if (files.Contains(fileName)) continue;

                    var dest = Application.streamingAssetsPath /*+ "/" + GetPlatformName()*/ + "/" + fileName;
                    if (File.Exists(src))
                    {
                        File.Copy(src, dest, true);
                    }
                }
            }
        }

        public static string GetPlatformName()
        {
            return GetPlatformForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        }

        private static string GetPlatformForAssetBundles(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSX:
                    return "OSX";
                default:
                    return null;
            }
        }

        private static string[] GetLevelsFromBuildSettings()
        {
            List<string> scenes = new List<string>();
            foreach (var item in GetXAssetBuildRules().scenesInBuild)
            {
                var path = AssetDatabase.GetAssetPath(item);
                if (!string.IsNullOrEmpty(path))
                {
                    scenes.Add(path);
                }
            }

            return scenes.ToArray();
        }

        private static string GetAssetBundleManifestFilePath()
        {
            var relativeAssetBundlesOutputPathForPlatform = Path.Combine("Asset", GetPlatformName());
            return Path.Combine(relativeAssetBundlesOutputPathForPlatform, GetPlatformName()) + ".manifest";
        }

        public static void BuildPlayer()
        {
            var outputPath =
                Path.Combine(Environment.CurrentDirectory,
                    "ExtraResources/Build/" + GetPlatformName() + "/" + Application.productName
                        .ToLower()); //EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");
            if (outputPath.Length == 0)
                return;

            var levels = GetLevelsFromBuildSettings();
            if (levels.Length == 0)
            {
                Debug.Log("Nothing to build.");
                return;
            }

            var targetName = GetBuildTargetName(EditorUserBuildSettings.activeBuildTarget);
            if (targetName == null)
                return;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = levels,
                locationPathName = outputPath + targetName,
                assetBundleManifestPath = GetAssetBundleManifestFilePath(),
                target = EditorUserBuildSettings.activeBuildTarget,
                options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None
            };
            BuildPipeline.BuildPlayer(buildPlayerOptions);
            OpenFolderUtility.OpenDirectory(outputPath);
        }

        public static string CreateAssetBundleDirectory()
        {
            // Choose the output path according to the build target.
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            return outputPath;
        }

        public static void BuildAssetBundles()
        {
            // Choose the output path according to the build target.
            var outputPath = CreateAssetBundleDirectory();
            //const BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;
            var options = GetXAssetSettings().buildAssetBundleOptions;

            var targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            var rules = GetXAssetBuildRules();
            var builds = rules.GetAssetBundleBuilds();
            var assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, builds, options, targetPlatform);
            if (assetBundleManifest == null)
            {
                return;
            }

            var manifest = GetXAssetManifest();
            var dirs = new List<string>();
            var assets = new List<AssetRef>();
            var bundles = assetBundleManifest.GetAllAssetBundles();
            var bundle2Ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < bundles.Length; index++)
            {
                var bundle = bundles[index];
                bundle2Ids[bundle] = index;
            }

            var bundleRefs = new List<BundleRef>();
            for (var index = 0; index < bundles.Length; index++)
            {
                var bundle = bundles[index];
                var deps = assetBundleManifest.GetAllDependencies(bundle);
                var path = string.Format("{0}/{1}", outputPath, bundle);
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        bundleRefs.Add(new BundleRef
                        {
                            name = bundle,
                            id = index,
                            deps = Array.ConvertAll(deps, input => bundle2Ids[input]),
                            len = stream.Length,
                            hash = assetBundleManifest.GetAssetBundleHash(bundle).ToString(),
                        });
                    }
                }
                else
                {
                    Debug.LogError(path + " file not exsit.");
                }
            }

            for (var i = 0; i < rules.ruleAssets.Length; i++)
            {
                var item = rules.ruleAssets[i];
                var path = item.path;
                var dir = Path.GetDirectoryName(path).Replace("\\", "/");
                var index = dirs.FindIndex(o => o.Equals(dir));
                if (index == -1)
                {
                    index = dirs.Count;
                    dirs.Add(dir);
                }

                var asset = new AssetRef { bundle = bundle2Ids[item.bundle], dir = index, name = Path.GetFileName(path) };
                assets.Add(asset);
            }

            manifest.dirs = dirs.ToArray();
            manifest.assets = assets.ToArray();
            manifest.bundles = bundleRefs.ToArray();

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var manifestBundleName = "manifest.unity3d";
            builds = new[] {
                new AssetBundleBuild {
                    assetNames = new[] { AssetDatabase.GetAssetPath (manifest), },
                    assetBundleName = manifestBundleName
                }
            };

            BuildPipeline.BuildAssetBundles(outputPath, builds, options, targetPlatform);
            ArrayUtility.Add(ref bundles, manifestBundleName);
        }

        private static string GetBuildTargetName(BuildTarget target)
        {
            string name = string.Empty;
            string time = string.Empty;
            if (GetXAssetSettings().buildSingleFolder)
            {
                name = PlayerSettings.productName;
                time = string.Empty;
            }
            else
            {
                time = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                name = PlayerSettings.productName + "-v" + PlayerSettings.bundleVersion + "." + GetXAssetBuildRules().version;
            }
            switch (target)
            {
                case BuildTarget.Android:
                    return string.Format("/{0}-{1}.apk", name, time);

                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return string.Format("/{0}-{1}.exe", name, time);

                case BuildTarget.StandaloneOSX:
                    return "/" + name + ".app";

                //case BuildTarget.WebGL:
                //case BuildTarget.iOS:
                //    return "";

                // Add more build targets for your own.
                default:
                    Debug.Log("Target not implemented.");
                    return null;
            }
        }

        internal static T GetAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }

        internal static XAssetManifest GetXAssetManifest()
        {
            return GetAsset<XAssetManifest>(XAsset.k_XAssetManifestAsset);
        }

        internal static XAssetBuildRules GetXAssetBuildRules()
        {
            return GetAsset<XAssetBuildRules>("Assets/XAsset/XAssetBuildRules.asset");
        }

        internal static XAssetSettings GetXAssetSettings()
        {
            return GetAsset<XAssetSettings>("Assets/XAsset/XAssetSettings.asset");
        }
    }
}