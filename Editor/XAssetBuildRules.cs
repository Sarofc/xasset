using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Saro.XAsset.Build
{
    public enum ENameBy
    {
        Explicit,
        Path,
        Directory,
        TopDirectory
    }

    [Serializable]
    public struct RuleAsset
    {
        /// <summary>
        /// bundle名称
        /// </summary>
        public string bundle;

        /// <summary>
        /// 资源路径
        /// </summary>
        public string asset;
    }

    [Serializable]
    public struct RuleBundle
    {
        /// <summary>
        /// bundle名称
        /// </summary>
        public string bundle;

        /// <summary>
        /// 资源路径合集
        /// </summary>
        public string[] assets;
    }

    [Serializable]
    public struct BuildRule
    {
        [Tooltip("搜索路径")] public string searchPath;

        [Tooltip("搜索通配符，多个之间请用,(逗号)隔开")] public string searchPattern;

        [Tooltip("命名规则")] public ENameBy nameBy;

        [Tooltip("Explicit的名称")] public string assetBundleName;

        /// <summary>
        /// 
        /// </summary>
        /// <returns>根据搜索规则,获取所有资源的路径</returns>
        public string[] GetAssets()
        {
            var patterns = searchPattern.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (!Directory.Exists(searchPath))
            {
                Debug.LogWarning("Rule searchPath not exist:" + searchPath);
                return new string[0];
            }

            var getFiles = new List<string>(1024);
            foreach (var item in patterns)
            {
                var files = Directory.GetFiles(searchPath, item, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (Directory.Exists(file)) continue;
                    var ext = Path.GetExtension(file).ToLower();
                    if ((ext == ".fbx" || ext == ".anim") && !item.Contains(ext)) continue;
                    if (!XAssetBuildRules.ValidateAsset(file)) continue;
                    var asset = file.Replace("\\", "/");
                    getFiles.Add(asset);
                }
            }

            return getFiles.ToArray();
        }
    }

    public class XAssetBuildRules : ScriptableObject
    {
        private readonly Dictionary<string, string> m_Asset2Bundles = new Dictionary<string, string>(1024, StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> m_Conflicted = new Dictionary<string, string[]>(1024, StringComparer.Ordinal);
        private readonly List<string> m_DuplicatedAssets = new List<string>(128);
        private readonly Dictionary<string, HashSet<string>> m_Tracker = new Dictionary<string, HashSet<string>>(1024, StringComparer.Ordinal);
        [Header("Patterns")]
        public string searchPatternAsset = "*.asset";
        public string searchPatternController = "*.controller";
        public string searchPatternDir = "*";
        public string searchPatternMaterial = "*.mat";
        public string searchPatternPng = "*.png";
        public string searchPatternPrefab = "*.prefab";
        public string searchPatternScene = "*.unity";
        public string searchPatternText = "*.txt,*.bytes,*.json,*.csv,*.xml,*htm,*.html,*.yaml,*.fnt";
        [Tooltip("是否用hash代替bundle名称")]
        public bool nameBundleByHash = true;

        [Tooltip("构建的版本号")]
        [Header("Builds")]
        public string version = "0.0.0";

        [Tooltip("build-in场景")]
        public SceneAsset[] scenesInBuild = new SceneAsset[0];

        public BuildRule[] rules = new BuildRule[0];

        [Attributes.ReadOnly] public RuleAsset[] ruleAssets = new RuleAsset[0];
        [Attributes.ReadOnly] public RuleBundle[] ruleBundles = new RuleBundle[0];

        #region API

        public System.Version AddVersion()
        {
            var versionObj = new System.Version(version);
            var revision = versionObj.Revision + 1;
            versionObj = new System.Version(versionObj.Major, versionObj.Minor, versionObj.Build, revision);
            version = versionObj.ToString();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return versionObj;
        }

        public void Apply()
        {
            Clear();
            CollectAssets();
            AnalysisAssets();
            OptimizeAssets();
            Save();
        }

        public AssetBundleBuild[] GetAssetBundleBuilds()
        {
            // new
            var builds = new AssetBundleBuild[ruleBundles.Length];
            for (int i = 0; i < ruleBundles.Length; i++)
            {
                RuleBundle ruleBundle = ruleBundles[i];
                builds[i] = new AssetBundleBuild
                {
                    assetNames = ruleBundle.assets,
                    assetBundleName = ruleBundle.bundle
                };
            }

            return builds;

            // old
            //var builds = new List<AssetBundleBuild>();
            //foreach (var bundle in ruleBundles)
            //{
            //    builds.Add(new AssetBundleBuild
            //    {
            //        assetNames = bundle.assets,
            //        assetBundleName = bundle.name
            //    });
            //}

            //return builds.ToArray();
        }

        #endregion

        #region Private

        internal static bool ValidateAsset(string asset)
        {
            if (!asset.StartsWith("Assets/")) return false;

            var ext = Path.GetExtension(asset).ToLower();
            return ext != ".dll" && ext != ".cs" && ext != ".meta" && ext != ".js" && ext != ".boo";
        }

        internal static bool IsSceneAsset(string asset)
        {
            return asset.EndsWith(".unity");
        }

        private string RuledAssetBundleName(string name)
        {
            if (nameBundleByHash)
            {
                return Utility.HashUtility.GetMd5Hash(name) + XAssetComponent.k_AssetExtension;
            }
            return name.Replace("\\", "/").ToLower() + XAssetComponent.k_AssetExtension;
        }

        private void Track(string asset, string bundle)
        {
            if (!m_Tracker.TryGetValue(asset, out HashSet<string> bundles))
            {
                bundles = new HashSet<string>();
                m_Tracker.Add(asset, bundles);
            }

            bundles.Add(bundle);

            // 一个asset在多个bundles里, 即冗余了
            if (bundles.Count > 1)
            {
                m_Asset2Bundles.TryGetValue(asset, out string bundleName);
                if (string.IsNullOrEmpty(bundleName))
                {
                    m_DuplicatedAssets.Add(asset);
                }
            }
        }

        private Dictionary<string, List<string>> GetBundle2Assets()
        {
            var assetCount = m_Asset2Bundles.Count;
            var bundles = new Dictionary<string, List<string>>(assetCount / 2, StringComparer.Ordinal);
            foreach (var item in m_Asset2Bundles)
            {
                var bundle = item.Value;
                if (!bundles.TryGetValue(bundle, out List<string> list))
                {
                    list = new List<string>(64);
                    bundles[bundle] = list;
                }
                var asset = item.Key;
                if (!list.Contains(asset)) list.Add(asset);
            }

            return bundles;
        }

        private void Clear()
        {
            m_Tracker.Clear();
            m_DuplicatedAssets.Clear();
            m_Conflicted.Clear();
            m_Asset2Bundles.Clear();
        }

        private void Save()
        {
            var bundle2Assets = GetBundle2Assets();
            ruleBundles = new RuleBundle[bundle2Assets.Count];
            var i = 0;
            foreach (var item in bundle2Assets)
            {
                ruleBundles[i] = new RuleBundle
                {
                    bundle = item.Key,
                    assets = item.Value.ToArray()
                };
                i++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void OptimizeAssets()
        {
            // 剔除冗余资源
            int i = 0, max = m_Conflicted.Count;
            foreach (var item in m_Conflicted)
            {
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冲突{0}/{1}", i, max), item.Key, i / (float)max))
                    break;

                var list = item.Value;
                foreach (string asset in list)
                {
                    if (!IsSceneAsset(asset))
                        m_DuplicatedAssets.Add(asset);
                }
                i++;
            }

            for (i = 0, max = m_DuplicatedAssets.Count; i < max; i++)
            {
                var item = m_DuplicatedAssets[i];

                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冗余{0}/{1}", i, max), item, i / (float)max))
                    break;

                OptimizeAsset(item);
            }
        }

        private void AnalysisAssets()
        {
            var bundle2Assets = GetBundle2Assets();
            int i = 0, max = bundle2Assets.Count;
            foreach (var item in bundle2Assets)
            {
                var bundle = item.Key;

                if (EditorUtility.DisplayCancelableProgressBar(string.Format("分析依赖{0}/{1}", i, max), bundle, i / (float)max))
                    break;

                var assetPaths = bundle2Assets[bundle];

                var pathNames = assetPaths.ToArray();
                if (assetPaths.Exists(IsSceneAsset) && !assetPaths.TrueForAll(IsSceneAsset))
                    m_Conflicted.Add(bundle, pathNames);

                // 获取所有被引用的资源
                var dependencies = AssetDatabase.GetDependencies(pathNames, true);
                if (dependencies.Length > 0)
                {
                    // 获取所有冗余项
                    foreach (var asset in dependencies)
                    {
                        if (ValidateAsset(asset))
                            Track(asset, bundle);
                    }
                }
                i++;
            }
        }

        private void CollectAssets()
        {
            for (int i = 0, max = rules.Length; i < max; i++)
            {
                var rule = rules[i];

                if (EditorUtility.DisplayCancelableProgressBar(string.Format("收集资源{0}/{1}", i, max), rule.searchPath, i / (float)max))
                    break;

                ApplyRule(rule);
            }

            // new
            var array = new RuleAsset[m_Asset2Bundles.Count];
            int index = 0;
            foreach (var item in m_Asset2Bundles)
            {
                array[index++] = new RuleAsset
                {
                    asset = item.Key,
                    bundle = item.Value
                };
            }

            Array.Sort(array, (a, b) => string.Compare(a.asset, b.asset, StringComparison.Ordinal));
            ruleAssets = array;

            // old
            //var list = new List<RuleAsset>();
            //foreach (var item in m_Asset2Bundles)
            //    list.Add(new RuleAsset
            //    {
            //        path = item.Key,
            //        bundle = item.Value
            //    });
            //list.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            //ruleAssets = list.ToArray();
        }

        private void OptimizeAsset(string asset)
        {
            if (asset.EndsWith(".shader"))
                m_Asset2Bundles[asset] = RuledAssetBundleName("shaders");
            else
                m_Asset2Bundles[asset] = RuledAssetBundleName(asset);
        }

        private void ApplyRule(BuildRule rule)
        {
            var assets = rule.GetAssets();

            // 根据类型命名
            switch (rule.nameBy)
            {
                case ENameBy.Explicit:
                    {
                        foreach (var asset in assets)
                            m_Asset2Bundles[asset] = RuledAssetBundleName(rule.assetBundleName);

                        break;
                    }
                case ENameBy.Path:
                    {
                        foreach (var asset in assets)
                            m_Asset2Bundles[asset] = RuledAssetBundleName(asset);

                        break;
                    }
                case ENameBy.Directory:
                    {
                        foreach (var asset in assets)
                            m_Asset2Bundles[asset] = RuledAssetBundleName(Path.GetDirectoryName(asset));

                        break;
                    }
                case ENameBy.TopDirectory:
                    {
                        var startIndex = rule.searchPath.Length;
                        foreach (var asset in assets)
                        {
                            var dir = Path.GetDirectoryName(asset);
                            if (!string.IsNullOrEmpty(dir))
                                if (!dir.Equals(rule.searchPath))
                                {
                                    var pos = dir.IndexOf("/", startIndex + 1, StringComparison.Ordinal);
                                    if (pos != -1) dir = dir.Substring(0, pos);
                                }

                            m_Asset2Bundles[asset] = RuledAssetBundleName(dir);
                        }

                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion
    }
}