﻿//#define DEBUG_XASSET

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using Saro.Core;
using MGF;
using Saro.Tasks;

namespace Saro.XAsset
{
    [ObjectSystem]
    public sealed class XAssetComponentUpdateSystem : UpdateSystem<XAssetComponent>
    {
        public override void Update(XAssetComponent self)
        {
            self.Update();
        }
    }

    public sealed class XAssetComponent : EntityAssetInterface
    {
        public const string k_XAssetManifestAsset = "Assets/XAsset/XAssetManifest.asset";
        public const string k_AssetExtension = ".unity3d";

        public static bool s_RuntimeMode = true;
        public static Func<string, Type, UnityEngine.Object> s_EditorLoader = null;

        [System.Diagnostics.Conditional("DEBUG_XASSET")]
        private void INFO(string msg)
        {
            Log.INFO("XAsset", msg);
        }

        //[System.Diagnostics.Conditional("DEBUG_XASSET")]
        private void WARN(string msg)
        {
            Log.WARN("XAsset", msg);
        }

        private void ERROR(string msg)
        {
            Log.ERROR("XAsset", msg);
        }

        internal static XAssetComponent Get()
        {
            return Game.Resolve<XAssetComponent>();
        }

        #region API

        /// <summary>
        /// 读取所有资源路径
        /// </summary>
        /// <returns></returns>
        public IReadOnlyDictionary<string, string> GetAllAssetPaths()
        {
            //var assets = new List<string>();
            //assets.AddRange(_assetToBundles.Keys);
            //return assets.ToArray();
            return m_AssetToBundles;
        }

        public static string s_BasePath { get; internal set; }

        public static string s_UpdatePath { get; internal set; }

        public void AddSearchPath(string path)
        {
            m_SearchPaths.Add(path);
        }

        public void Clear()
        {
            if (m_RunningSceneRequest != null)
            {
                m_RunningSceneRequest.Release();
                m_RunningSceneRequest = null;
            }

            RemoveUnusedAssets();
            UpdateAssets();
            UpdateBundles();

            m_SearchPaths.Clear();
            m_ActiveVariants.Clear();
            m_AssetToBundles.Clear();
            m_BundleToDependencies.Clear();
        }

        private SceneAssetRequest m_RunningSceneRequest;

        public override IAssetRequest LoadSceneAsync(string path, bool additive = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                ERROR("invalid path");
                return null;
            }

            path = GetExistPath(path);
            var request = new SceneAssetAsyncRequest(path, additive);
            if (!additive)
            {
                if (m_RunningSceneRequest != null)
                {
                    m_RunningSceneRequest.Release();
                    m_RunningSceneRequest = null;
                }
                m_RunningSceneRequest = request;
            }
            request.Load();
            request.Retain();
            m_SceneRequests.Add(request);
            INFO(string.Format("LoadScene:{0}", path));
            return request;
        }

        public override T LoadAsset<T>(string path)
        {
            var asset = LoadAsset(path, typeof(T));
            if (asset != null && asset.Asset != null)
            {
                return asset.Asset as T;
            }

            return null;
        }

        public override IAssetRequest LoadAssetAsync<T>(string path)
        {
            return LoadAssetInternal(path, typeof(T), true);
        }

        public override IAssetRequest LoadAssetAsync(string path, Type type)
        {
            return LoadAssetInternal(path, type, true);
        }

        public override IAssetRequest LoadAsset(string path, Type type)
        {
            return LoadAssetInternal(path, type, false);
        }

        public override void UnloadAsset(IAssetRequest asset)
        {
            asset.Release();
        }

        public override void RemoveUnusedAssets()
        {
            foreach (var item in m_Assets)
            {
                if (item.Value.IsUnused())
                {
                    m_UnusedAssets.Add(item.Value);
                }
            }
            foreach (var request in m_UnusedAssets)
            {
                m_Assets.Remove(request.Url);
            }
            foreach (var item in m_UrlToBundles)
            {
                if (item.Value.IsUnused())
                {
                    m_UnusedBundles.Add(item.Value);
                }
            }
            foreach (var request in m_UnusedBundles)
            {
                m_UrlToBundles.Remove(request.Url);
            }
        }

        #endregion

        #region Private

        internal void OnManifestLoaded(XAssetManifest manifest)
        {
            m_ActiveVariants.AddRange(manifest.activeVariants);

            var assets = manifest.assets;
            var dirs = manifest.dirs;
            var bundles = manifest.bundles;

            for (int i = 0; i < bundles.Length; i++)
            {
                BundleRef item = bundles[i];
                m_BundleToDependencies[item.name] = Array.ConvertAll(item.deps, id => bundles[id].name);
            }

            foreach (var item in assets)
            {
                var path = string.Format("{0}/{1}", dirs[item.dir], item.name);
                if (item.bundle >= 0 && item.bundle < bundles.Length)
                {
                    m_AssetToBundles[path] = bundles[item.bundle].name;
                }
                else
                {
                    ERROR(string.Format("{0} bundle {1} not exist.", path, item.bundle));
                }
            }
        }

        private Dictionary<string, AssetRequest> m_Assets = new Dictionary<string, AssetRequest>(StringComparer.Ordinal);

        private List<AssetRequest> m_LoadingAssets = new List<AssetRequest>();

        private List<SceneAssetRequest> m_SceneRequests = new List<SceneAssetRequest>();

        private List<AssetRequest> m_UnusedAssets = new List<AssetRequest>();

        private void UpdateAssets()
        {
            for (var i = 0; i < m_LoadingAssets.Count; ++i)
            {
                var request = m_LoadingAssets[i];
                if (request.Update())
                    continue;
                m_LoadingAssets.RemoveAt(i);
                --i;
            }

            if (m_UnusedAssets.Count > 0)
            {
                for (var i = 0; i < m_UnusedAssets.Count; ++i)
                {
                    var request = m_UnusedAssets[i];
                    if (!request.IsDone) continue;
                    INFO(string.Format("UnloadAsset:{0}", request.Url));
                    request.Unload();
                    m_UnusedAssets.RemoveAt(i);
                    i--;
                }
            }

            for (var i = 0; i < m_SceneRequests.Count; ++i)
            {
                var request = m_SceneRequests[i];
                if (request.Update() || !request.IsUnused())
                    continue;
                m_SceneRequests.RemoveAt(i);
                INFO(string.Format("UnloadScene:{0}", request.Url));
                request.Unload();
                RemoveUnusedAssets();
                --i;
            }
        }

        private void AddAssetRequest(AssetRequest request)
        {
            m_Assets.Add(request.Url, request);
            m_LoadingAssets.Add(request);
            request.Load();
        }

        private AssetRequest LoadAssetInternal(string path, Type type, bool async)
        {
            if (string.IsNullOrEmpty(path))
            {
                ERROR("invalid path");
                return null;
            }

            path = GetExistPath(path);

            AssetRequest request;
            if (m_Assets.TryGetValue(path, out request))
            {
                request.Update();
                request.Retain();
                m_LoadingAssets.Add(request);
                return request;
            }

            if (GetAssetBundleName(path, out string assetBundleName))
            {
                request = async
                    ? new BundleAssetAsyncRequest(assetBundleName)
                    : new BundleAssetRequest(assetBundleName);
            }
            else
            {
                if (path.StartsWith("http://", StringComparison.Ordinal) ||
                    path.StartsWith("https://", StringComparison.Ordinal) ||
                    path.StartsWith("file://", StringComparison.Ordinal) ||
                    path.StartsWith("ftp://", StringComparison.Ordinal) ||
                    path.StartsWith("jar:file://", StringComparison.Ordinal))
                {
                    request = new WebAssetRequest();

                    //INFO("WebAssetRequest 加载: " + path);
                }
                else
                {
                    request = new AssetRequest();

                    //INFO("AssetRequest 加载：" + path);
                }
            }

            request.Url = path;
            request.AssetType = type;
            AddAssetRequest(request);
            request.Retain();

            INFO(string.Format("LoadAsset:{0}", path));

            return request;
        }

        #endregion

        #region Paths

        private List<string> m_SearchPaths = new List<string>();

        private string GetExistPath(string path)
        {
#if UNITY_EDITOR
            if (!s_RuntimeMode)
            {
                if (File.Exists(path))
                    return path;

                foreach (var item in m_SearchPaths)
                {
                    var existPath = string.Format("{0}/{1}", item, path);
                    if (File.Exists(existPath))
                        return existPath;
                }

                ERROR("找不到资源路径" + path);
                return path;
            }
#endif
            if (m_AssetToBundles.ContainsKey(path))
                return path;

            foreach (var item in m_SearchPaths)
            {
                var existPath = string.Format("{0}/{1}", item, path);
                if (m_AssetToBundles.ContainsKey(existPath))
                    return existPath;
            }

            ERROR("资源没有收集打包" + path);
            return path;
        }

        #endregion


        #region Bundles

        private const int k_MAX_BUNDLES_PERFRAME = 0;

        private Dictionary<string, BundleRequest> m_UrlToBundles = new Dictionary<string, BundleRequest>(StringComparer.Ordinal);

        private List<BundleRequest> m_LoadingBundles = new List<BundleRequest>();

        private List<BundleRequest> m_UnusedBundles = new List<BundleRequest>();

        private List<BundleRequest> m_ToLoadBundles = new List<BundleRequest>();

        private List<string> m_ActiveVariants = new List<string>();

        private Dictionary<string, string> m_AssetToBundles = new Dictionary<string, string>(StringComparer.Ordinal);

        private Dictionary<string, string[]> m_BundleToDependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);

        internal bool GetAssetBundleName(string path, out string assetBundleName)
        {
            return m_AssetToBundles.TryGetValue(path, out assetBundleName);
        }

        private string[] GetAllDependencies(string bundle)
        {
            if (m_BundleToDependencies.TryGetValue(bundle, out string[] deps))
                return deps;

            return new string[0];
        }

        internal BundleRequest LoadBundle(string assetBundleName)
        {
            return LoadBundleInternal(assetBundleName, false);
        }

        internal BundleRequest LoadBundleAsync(string assetBundleName)
        {
            return LoadBundleInternal(assetBundleName, true);
        }

        internal void UnloadBundle(BundleRequest bundle)
        {
            bundle.Release();
        }

        private void UnloadDependencies(BundleRequest bundle)
        {
            for (var i = 0; i < bundle.Dependencies.Count; i++)
            {
                var item = bundle.Dependencies[i];
                item.Release();
            }

            bundle.Dependencies.Clear();
        }

        private void LoadDependencies(BundleRequest bundle, string assetBundleName, bool asyncRequest)
        {
            var dependencies = GetAllDependencies(assetBundleName);
            if (dependencies.Length <= 0)
                return;
            for (var i = 0; i < dependencies.Length; i++)
            {
                var item = dependencies[i];
                bundle.Dependencies.Add(LoadBundleInternal(item, asyncRequest));
            }
        }

        private BundleRequest LoadBundleInternal(string assetBundleName, bool asyncMode)
        {
            if (string.IsNullOrEmpty(assetBundleName))
            {
                ERROR("assetBundleName == null");
                return null;
            }

            assetBundleName = RemapVariantName(assetBundleName);
            var url = GetDataPath(assetBundleName) + assetBundleName;

            if (m_UrlToBundles.TryGetValue(url, out BundleRequest bundleRequest))
            {
                bundleRequest.Update();
                bundleRequest.Retain();
                m_LoadingBundles.Add(bundleRequest);
                return bundleRequest;
            }

            if (url.StartsWith("http://", StringComparison.Ordinal) ||
                url.StartsWith("https://", StringComparison.Ordinal) ||
                url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("ftp://", StringComparison.Ordinal))
                bundleRequest = new WebBundleRequest();
            else
                bundleRequest = asyncMode ? new BundleAsyncRequest() : new BundleRequest();

            bundleRequest.Url = url;
            m_UrlToBundles.Add(url, bundleRequest);

            if (k_MAX_BUNDLES_PERFRAME > 0 && (bundleRequest is BundleAsyncRequest || bundleRequest is WebBundleRequest))
            {
                m_ToLoadBundles.Add(bundleRequest);
            }
            else
            {
                bundleRequest.Load();
                m_LoadingBundles.Add(bundleRequest);
                INFO("LoadBundle: " + url);
            }

            LoadDependencies(bundleRequest, assetBundleName, asyncMode);

            bundleRequest.Retain();
            return bundleRequest;
        }

        private string GetDataPath(string bundleName)
        {
            if (string.IsNullOrEmpty(s_UpdatePath))
                return s_BasePath;

            if (File.Exists(s_UpdatePath + bundleName))
                return s_UpdatePath;

            return s_BasePath;
        }

        private void UpdateBundles()
        {
            if (m_ToLoadBundles.Count > 0 &&
                k_MAX_BUNDLES_PERFRAME > 0 &&
                m_LoadingBundles.Count < k_MAX_BUNDLES_PERFRAME
                )
            {
                for (var i = 0; i < Math.Min(k_MAX_BUNDLES_PERFRAME - m_LoadingBundles.Count, m_ToLoadBundles.Count); ++i)
                {
                    var item = m_ToLoadBundles[i];
                    if (item.m_LoadState == ELoadState.Init)
                    {
                        item.Load();
                        m_LoadingBundles.Add(item);
                        m_ToLoadBundles.RemoveAt(i);
                        --i;
                    }
                }
            }


            for (var i = 0; i < m_LoadingBundles.Count; i++)
            {
                var item = m_LoadingBundles[i];
                if (item.Update())
                    continue;
                m_LoadingBundles.RemoveAt(i);
                --i;
            }

            if (m_UnusedBundles.Count <= 0) return;
            {
                for (var i = 0; i < m_UnusedBundles.Count; i++)
                {
                    var item = m_UnusedBundles[i];
                    if (item.IsDone)
                    {
                        UnloadDependencies(item);
                        item.Unload();
                        INFO("UnloadBundle: " + item.Url);
                        m_UnusedBundles.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        private string RemapVariantName(string assetBundleName)
        {
            var bundlesWithVariant = m_ActiveVariants;
            // Get base bundle path
            var baseName = assetBundleName.Split('.')[0];

            var bestFit = int.MaxValue;
            var bestFitIndex = -1;
            // Loop all the assetBundles with variant to find the best fit variant assetBundle.
            for (var i = 0; i < bundlesWithVariant.Count; i++)
            {
                var curSplit = bundlesWithVariant[i].Split('.');
                var curBaseName = curSplit[0];
                var curVariant = curSplit[1];

                if (curBaseName != baseName)
                    continue;

                var found = bundlesWithVariant.IndexOf(curVariant);

                // If there is no active variant found. We still want to use the first
                if (found == -1)
                    found = int.MaxValue - 1;

                if (found >= bestFit)
                    continue;
                bestFit = found;
                bestFitIndex = i;
            }

            if (bestFit == int.MaxValue - 1)
            {
                WARN("Ambiguous asset bundle variant chosen because there was no matching active variant: " +
                   bundlesWithVariant[bestFitIndex]);
            }

            return bestFitIndex != -1 ?
                bundlesWithVariant[bestFitIndex] :
                assetBundleName;
        }

        #endregion

        #region Service

        public async FTask<bool> InitializeAsync()
        {
            if (string.IsNullOrEmpty(s_BasePath))
            {
                s_BasePath = Application.streamingAssetsPath + Path.DirectorySeparatorChar;
            }

            if (string.IsNullOrEmpty(s_UpdatePath))
            {
                s_UpdatePath = Application.persistentDataPath + Path.DirectorySeparatorChar;
            }

            //var path = string.Format("{0}/{1}", s_BasePath, Versions.Dataname);

            Clear();

            INFO(string.Format(
                "Initialize with: runtimeMode={0}\nbasePath：{1}\nupdatePath={2}",
                s_RuntimeMode, s_BasePath, s_UpdatePath));

            //if (runtimeMode)
            //{
            //    if (!Versions.LoadDisk(path))
            //    {
            //        throw new Exception("vfile load failed! path=" + path);
            //    }
            //}

            var request = new ManifestRequest { Url = k_XAssetManifestAsset };
            AddAssetRequest(request);

            var tcs = FTask<bool>.Create(true);

            request.Completed += (req) =>
            {
                req.Release();
                if (req.IsError)
                {
                    ERROR("XAssetComponent Error: " + req.Error);
                }

                tcs.SetResult(!req.IsError);
            };

            return await tcs;
        }

        public void Update()
        {
            UpdateAssets();
            UpdateBundles();
        }

        //IEnumerator<IAsyncJobResult> IService.Initialize(ServiceLocator serviceLocator)
        //{
        //    Processor.onUpdate += ((IHasUpdate)this).Update;

        //    var init = Initialize();

        //    yield return new WaitForAssetMgrInitialized(init);

        //    if (!init.IsError)
        //    {
        //        //AssetMgr.AddSearchPath("Assets/XAsset/Demo/Scenes");
        //    }
        //    else
        //    {
        //        ERROR("[XAsset] Initialize Error");
        //    }
        //    init.Release();

        //    //ERROR("all asset path: ");
        //    //foreach (var path in GetAllAssetPaths())
        //    //{
        //    //    ERROR("\t" + path);
        //    //}

        //    Processor.onUpdate -= ((IHasUpdate)this).Update;
        //}


        //Type[] IService.GetDependencies()
        //{
        //    return null;
        //}

        //void IService.Shutdown()
        //{ }

        //private sealed class WaitForAssetMgrInitialized : IAsyncJobResult, IJobDependency, IUnreliableJobDependency
        //{
        //    private readonly ManifestRequest m_AssetsInitRequest;

        //    public WaitForAssetMgrInitialized(ManifestRequest assetsInitRequest)
        //    {
        //        m_AssetsInitRequest = assetsInitRequest;
        //    }

        //    public bool HasFailed()
        //    {
        //        return !string.IsNullOrEmpty(m_AssetsInitRequest.Error);
        //    }

        //    public bool IsReady()
        //    {
        //        return m_AssetsInitRequest.IsDone;
        //    }
        //}

        #endregion
    }
}