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
    public class RuleAsset
    {
        public string bundle;
        public string path;
    }

    [Serializable]
    public class RuleBundle
    {
        public string name;
        public string[] assets;
    }

    [Serializable]
    public class BuildRule
    {
        [Tooltip("搜索路径")] public string searchPath;

        [Tooltip("搜索通配符，多个之间请用,(逗号)隔开")] public string searchPattern;

        [Tooltip("命名规则")] public ENameBy nameBy = ENameBy.Path;

        [Tooltip("Explicit的名称")] public string assetBundleName;

        public string[] GetAssets()
        {
            var patterns = searchPattern.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (!Directory.Exists(searchPath))
            {
                Debug.LogWarning("Rule searchPath not exist:" + searchPath);
                return new string[0];
            }

            var getFiles = new List<string>();
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
        private readonly Dictionary<string, string> m_Asset2Bundles = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> m_Conflicted = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly List<string> m_Duplicated = new List<string>();
        private readonly Dictionary<string, HashSet<string>> m_Tracker = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        [Header("Patterns")]
        [Attributes.ReadOnly] public string searchPatternAsset = "*.asset";
        [Attributes.ReadOnly] public string searchPatternController = "*.controller";
        [Attributes.ReadOnly] public string searchPatternDir = "*";
        [Attributes.ReadOnly] public string searchPatternMaterial = "*.mat";
        [Attributes.ReadOnly] public string searchPatternPng = "*.png";
        [Attributes.ReadOnly] public string searchPatternPrefab = "*.prefab";
        [Attributes.ReadOnly] public string searchPatternScene = "*.unity";
        [Attributes.ReadOnly] public string searchPatternText = "*.txt,*.bytes,*.json,*.csv,*.xml,*htm,*.html,*.yaml,*.fnt";
        public bool nameByHash = true;

        [Tooltip("构建的版本号")]
        [Header("Builds")]
        public string version = "0.0.0";

        [Tooltip("BuildPlayer 的时候被打包的场景")]
        public SceneAsset[] scenesInBuild = new SceneAsset[0];

        public BuildRule[] rules = new BuildRule[0];

        [Attributes.ReadOnly] public RuleAsset[] ruleAssets = new RuleAsset[0];
        [Attributes.ReadOnly] public RuleBundle[] ruleBundles = new RuleBundle[0];

        #region API

        public Version AddVersion()
        {
            var versionObj = new Version(version);
            var revision = versionObj.Revision + 1;
            versionObj = new Version(versionObj.Major, versionObj.Minor, versionObj.Build, revision);
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
            var builds = new List<AssetBundleBuild>();
            foreach (var bundle in ruleBundles)
            {
                builds.Add(new AssetBundleBuild
                {
                    assetNames = bundle.assets,
                    assetBundleName = bundle.name
                });
            }

            return builds.ToArray();
        }

        #endregion

        #region Private

        internal static bool ValidateAsset(string asset)
        {
            if (!asset.StartsWith("Assets/")) return false;

            var ext = Path.GetExtension(asset).ToLower();
            return ext != ".dll" && ext != ".cs" && ext != ".meta" && ext != ".js" && ext != ".boo";
        }

        private bool IsScene(string asset)
        {
            return asset.EndsWith(".unity");
        }

        private string RuledAssetBundleName(string name)
        {
            if (nameByHash)
            {
                return Utility.HashUtility.GetMD5Hash(name) + XAsset.k_AssetExtension;
            }
            return name.Replace("\\", "/").ToLower() + XAsset.k_AssetExtension;
        }

        private void Track(string asset, string bundle)
        {
            HashSet<string> assets;
            if (!m_Tracker.TryGetValue(asset, out assets))
            {
                assets = new HashSet<string>();
                m_Tracker.Add(asset, assets);
            }

            assets.Add(bundle);
            if (assets.Count > 1)
            {
                string bundleName;
                m_Asset2Bundles.TryGetValue(asset, out bundleName);
                if (string.IsNullOrEmpty(bundleName))
                {
                    m_Duplicated.Add(asset);
                }
            }
        }

        private Dictionary<string, List<string>> GetBundles()
        {
            var bundles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var item in m_Asset2Bundles)
            {
                var bundle = item.Value;
                List<string> list;
                if (!bundles.TryGetValue(bundle, out list))
                {
                    list = new List<string>();
                    bundles[bundle] = list;
                }

                if (!list.Contains(item.Key)) list.Add(item.Key);
            }

            return bundles;
        }

        private void Clear()
        {
            m_Tracker.Clear();
            m_Duplicated.Clear();
            m_Conflicted.Clear();
            m_Asset2Bundles.Clear();
        }

        private void Save()
        {
            var getBundles = GetBundles();
            ruleBundles = new RuleBundle[getBundles.Count];
            var i = 0;
            foreach (var item in getBundles)
            {
                ruleBundles[i] = new RuleBundle
                {
                    name = item.Key,
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
            int i = 0, max = m_Conflicted.Count;
            foreach (var item in m_Conflicted)
            {
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冲突{0}/{1}", i, max), item.Key,
                    i / (float)max)) break;
                var list = item.Value;
                foreach (var asset in list)
                    if (!IsScene(asset))
                        m_Duplicated.Add(asset);
                i++;
            }

            for (i = 0, max = m_Duplicated.Count; i < max; i++)
            {
                var item = m_Duplicated[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冗余{0}/{1}", i, max), item,
                    i / (float)max)) break;
                OptimizeAsset(item);
            }
        }

        private void AnalysisAssets()
        {
            var getBundles = GetBundles();
            int i = 0, max = getBundles.Count;
            foreach (var item in getBundles)
            {
                var bundle = item.Key;
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("分析依赖{0}/{1}", i, max), bundle,
                    i / (float)max)) break;
                var assetPaths = getBundles[bundle];
                if (assetPaths.Exists(IsScene) && !assetPaths.TrueForAll(IsScene))
                    m_Conflicted.Add(bundle, assetPaths.ToArray());
                var dependencies = AssetDatabase.GetDependencies(assetPaths.ToArray(), true);
                if (dependencies.Length > 0)
                    foreach (var asset in dependencies)
                        if (ValidateAsset(asset))
                            Track(asset, bundle);
                i++;
            }
        }

        private void CollectAssets()
        {
            for (int i = 0, max = rules.Length; i < max; i++)
            {
                var rule = rules[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("收集资源{0}/{1}", i, max), rule.searchPath,
                    i / (float)max))
                    break;
                ApplyRule(rule);
            }

            var list = new List<RuleAsset>();
            foreach (var item in m_Asset2Bundles)
                list.Add(new RuleAsset
                {
                    path = item.Key,
                    bundle = item.Value
                });
            list.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            ruleAssets = list.ToArray();
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

            Debug.Log("[XAsset] ApplyRule. assets: " + assets.Length);

            switch (rule.nameBy)
            {
                case ENameBy.Explicit:
                    {
                        foreach (var asset in assets) m_Asset2Bundles[asset] = RuledAssetBundleName(rule.assetBundleName);

                        break;
                    }
                case ENameBy.Path:
                    {
                        foreach (var asset in assets) m_Asset2Bundles[asset] = RuledAssetBundleName(asset);

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