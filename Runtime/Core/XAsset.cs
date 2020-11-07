//
// Assets.cs
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

#define LOG_ENABLE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

using Saro.Core;
using Saro.Core.Jobs;
using Saro.Core.Services;

namespace XAsset
{
    public sealed class XAsset : IAssetManager, IService, IHasUpdate
    {
        public static readonly string ManifestAsset = "Assets/XAsset/Manifest.asset";
        public static readonly string Extension = ".unity3d";
        private const string TAG = "[Assets]";

        public static bool runtimeMode = true;
        public static Func<string, Type, Object> loadDelegate = null;

        [Conditional("LOG_ENABLE")]
        private void Log(string s)
        {
            Debug.Log(string.Format("{0}{1}", TAG, s));
        }

        internal static XAsset Get()
        {
            return (XAsset)GameServices.Get().Resolve<IAssetManager>(true);
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
            return _assetToBundles;
        }

        public static string basePath { get; set; }

        public static string updatePath { get; set; }

        public void AddSearchPath(string path)
        {
            _searchPaths.Add(path);
        }

        internal ManifestRequest Initialize()
        {
            //var instance = FindObjectOfType<AssetMgr>();
            //if (instance == null)
            //{
            //    instance = new GameObject("Assets").AddComponent<AssetMgr>();
            //    DontDestroyOnLoad(instance.gameObject);
            //}

            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Application.streamingAssetsPath + Path.DirectorySeparatorChar;
            }

            if (string.IsNullOrEmpty(updatePath))
            {
                updatePath = Application.persistentDataPath + Path.DirectorySeparatorChar;
            }

            var path = string.Format("{0}/{1}", basePath, Versions.Dataname);

            Clear();

            Log(string.Format(
                "Initialize with: runtimeMode={0}\nbasePath：{1}\nupdatePath={2}",
                runtimeMode, basePath, updatePath));

            if (runtimeMode)
            {
                if (!Versions.LoadDisk(path))
                {
                    throw new Exception("vfile load failed! path=" + path);
                }
            }

            var request = new ManifestRequest { url = ManifestAsset };
            AddAssetRequest(request);
            return request;
        }

        public void Clear()
        {
            if (_runningScene != null)
            {
                _runningScene.Release();
                _runningScene = null;
            }

            RemoveUnusedAssets();
            UpdateAssets();
            UpdateBundles();

            _searchPaths.Clear();
            _activeVariants.Clear();
            _assetToBundles.Clear();
            _bundleToDependencies.Clear();
        }

        private SceneAssetRequest _runningScene;

        public IAssetRequest LoadSceneAsync(string path, bool additive)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("invalid path");
                return null;
            }

            path = GetExistPath(path);
            var asset = new SceneAssetAsyncRequest(path, additive);
            if (!additive)
            {
                if (_runningScene != null)
                {
                    _runningScene.Release(); ;
                    _runningScene = null;
                }
                _runningScene = asset;
            }
            asset.Load();
            asset.Retain();
            _scenes.Add(asset);
            Log(string.Format("LoadScene:{0}", path));
            return asset;
        }

        public T LoadAsset<T>(string path) where T : Object
        {
            var asset = LoadAsset(path, typeof(T));
            if (asset != null && asset.asset != null)
            {
                return asset.asset as T;
            }

            return null;
        }

        public IAssetRequest LoadAssetAsync<T>(string path) where T : Object
        {
            return LoadAsset(path, typeof(T), true);
        }

        public IAssetRequest LoadAssetAsync(string path, Type type)
        {
            return LoadAsset(path, type, true);
        }

        public IAssetRequest LoadAsset(string path, Type type)
        {
            return LoadAsset(path, type, false);
        }

        public void UnloadAsset(IAssetRequest asset)
        {
            asset.Release();
        }

        public void RemoveUnusedAssets()
        {
            foreach (var item in _assets)
            {
                if (item.Value.IsUnused())
                {
                    _unusedAssets.Add(item.Value);
                }
            }
            foreach (var request in _unusedAssets)
            {
                _assets.Remove(request.url);
            }
            foreach (var item in _bundles)
            {
                if (item.Value.IsUnused())
                {
                    _unusedBundles.Add(item.Value);
                }
            }
            foreach (var request in _unusedBundles)
            {
                _bundles.Remove(request.url);
            }
        }

        #endregion

        #region Private

        internal void OnLoadManifest(Manifest manifest)
        {
            _activeVariants.AddRange(manifest.activeVariants);

            var assets = manifest.assets;
            var dirs = manifest.dirs;
            var bundles = manifest.bundles;

            foreach (var item in bundles)
            {
                _bundleToDependencies[item.name] = Array.ConvertAll(item.deps, id => bundles[id].name);
                //Log(item.name);
            }

            foreach (var item in assets)
            {
                var path = string.Format("{0}/{1}", dirs[item.dir], item.name);
                if (item.bundle >= 0 && item.bundle < bundles.Length)
                {
                    _assetToBundles[path] = bundles[item.bundle].name;
                }
                else
                {
                    Debug.LogError(string.Format("{0} bundle {1} not exist.", path, item.bundle));
                }
            }
        }

        private static Dictionary<string, AssetRequest> _assets = new Dictionary<string, AssetRequest>(StringComparer.Ordinal);

        private static List<AssetRequest> _loadingAssets = new List<AssetRequest>();

        private static List<SceneAssetRequest> _scenes = new List<SceneAssetRequest>();

        private static List<AssetRequest> _unusedAssets = new List<AssetRequest>();

        private void Update()
        {
            UpdateAssets();
            UpdateBundles();
        }

        private void UpdateAssets()
        {
            for (var i = 0; i < _loadingAssets.Count; ++i)
            {
                var request = _loadingAssets[i];
                if (request.Update())
                    continue;
                _loadingAssets.RemoveAt(i);
                --i;
            }

            if (_unusedAssets.Count > 0)
            {
                for (var i = 0; i < _unusedAssets.Count; ++i)
                {
                    var request = _unusedAssets[i];
                    if (!request.isDone) continue;
                    Log(string.Format("UnloadAsset:{0}", request.url));
                    request.Unload();
                    _unusedAssets.RemoveAt(i);
                    i--;
                }
            }

            for (var i = 0; i < _scenes.Count; ++i)
            {
                var request = _scenes[i];
                if (request.Update() || !request.IsUnused())
                    continue;
                _scenes.RemoveAt(i);
                Log(string.Format("UnloadScene:{0}", request.url));
                request.Unload();
                RemoveUnusedAssets();
                --i;
            }
        }

        private void AddAssetRequest(AssetRequest request)
        {
            _assets.Add(request.url, request);
            _loadingAssets.Add(request);
            request.Load();
        }

        private AssetRequest LoadAsset(string path, Type type, bool async)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("invalid path");
                return null;
            }

            path = GetExistPath(path);

            AssetRequest request;
            if (_assets.TryGetValue(path, out request))
            {
                request.Update();
                request.Retain();
                _loadingAssets.Add(request);
                return request;
            }

            string assetBundleName;
            if (GetAssetBundleName(path, out assetBundleName))
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
                    request = new WebAssetRequest();
                else
                    request = new AssetRequest();
            }

            request.url = path;
            request.assetType = type;
            AddAssetRequest(request);
            request.Retain();
            Log(string.Format("LoadAsset:{0}", path));
            return request;
        }

        #endregion

        #region Paths

        private List<string> _searchPaths = new List<string>();

        private string GetExistPath(string path)
        {
#if UNITY_EDITOR
            if (!runtimeMode)
            {
                if (File.Exists(path))
                    return path;

                foreach (var item in _searchPaths)
                {
                    var existPath = string.Format("{0}/{1}", item, path);
                    if (File.Exists(existPath))
                        return existPath;
                }

                Debug.LogError("找不到资源路径" + path);
                return path;
            }
#endif
            if (_assetToBundles.ContainsKey(path))
                return path;

            foreach (var item in _searchPaths)
            {
                var existPath = string.Format("{0}/{1}", item, path);
                if (_assetToBundles.ContainsKey(existPath))
                    return existPath;
            }

            Debug.LogError("资源没有收集打包" + path);
            return path;
        }

        #endregion

        #region Bundles

        private static readonly int MAX_BUNDLES_PERFRAME = 0;

        private Dictionary<string, BundleRequest> _bundles = new Dictionary<string, BundleRequest>(StringComparer.Ordinal);

        private List<BundleRequest> _loadingBundles = new List<BundleRequest>();

        private List<BundleRequest> _unusedBundles = new List<BundleRequest>();

        private List<BundleRequest> _toloadBundles = new List<BundleRequest>();

        private List<string> _activeVariants = new List<string>();

        private Dictionary<string, string> _assetToBundles = new Dictionary<string, string>(StringComparer.Ordinal);

        private Dictionary<string, string[]> _bundleToDependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);

        internal bool GetAssetBundleName(string path, out string assetBundleName)
        {
            return _assetToBundles.TryGetValue(path, out assetBundleName);
        }

        private string[] GetAllDependencies(string bundle)
        {
            string[] deps;
            if (_bundleToDependencies.TryGetValue(bundle, out deps))
                return deps;

            return new string[0];
        }

        internal BundleRequest LoadBundle(string assetBundleName)
        {
            return LoadBundle(assetBundleName, false);
        }

        internal BundleRequest LoadBundleAsync(string assetBundleName)
        {
            return LoadBundle(assetBundleName, true);
        }

        internal void UnloadBundle(BundleRequest bundle)
        {
            bundle.Release();
        }

        private void UnloadDependencies(BundleRequest bundle)
        {
            for (var i = 0; i < bundle.dependencies.Count; i++)
            {
                var item = bundle.dependencies[i];
                item.Release();
            }

            bundle.dependencies.Clear();
        }

        private void LoadDependencies(BundleRequest bundle, string assetBundleName, bool asyncRequest)
        {
            var dependencies = GetAllDependencies(assetBundleName);
            if (dependencies.Length <= 0)
                return;
            for (var i = 0; i < dependencies.Length; i++)
            {
                var item = dependencies[i];
                bundle.dependencies.Add(LoadBundle(item, asyncRequest));
            }
        }

        internal BundleRequest LoadBundle(string assetBundleName, bool asyncMode)
        {
            if (string.IsNullOrEmpty(assetBundleName))
            {
                Debug.LogError("assetBundleName == null");
                return null;
            }

            assetBundleName = RemapVariantName(assetBundleName);
            var url = GetDataPath(assetBundleName) + assetBundleName;

            BundleRequest bundle;

            if (_bundles.TryGetValue(url, out bundle))
            {
                bundle.Update();
                bundle.Retain();
                _loadingBundles.Add(bundle);
                return bundle;
            }

            if (url.StartsWith("http://", StringComparison.Ordinal) ||
                url.StartsWith("https://", StringComparison.Ordinal) ||
                url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("ftp://", StringComparison.Ordinal))
                bundle = new WebBundleRequest();
            else
                bundle = asyncMode ? new BundleAsyncRequest() : new BundleRequest();

            bundle.url = url;
            _bundles.Add(url, bundle);

            if (MAX_BUNDLES_PERFRAME > 0 && (bundle is BundleAsyncRequest || bundle is WebBundleRequest))
            {
                _toloadBundles.Add(bundle);
            }
            else
            {
                bundle.Load();
                _loadingBundles.Add(bundle);
                Log("LoadBundle: " + url);
            }

            LoadDependencies(bundle, assetBundleName, asyncMode);

            bundle.Retain();
            return bundle;
        }

        private string GetDataPath(string bundleName)
        {
            if (string.IsNullOrEmpty(updatePath))
                return basePath;

            if (File.Exists(updatePath + bundleName))
                return updatePath;

            return basePath;
        }

        private void UpdateBundles()
        {
            var max = MAX_BUNDLES_PERFRAME;
            if (_toloadBundles.Count > 0 && max > 0 && _loadingBundles.Count < max)
                for (var i = 0; i < Math.Min(max - _loadingBundles.Count, _toloadBundles.Count); ++i)
                {
                    var item = _toloadBundles[i];
                    if (item.loadState == LoadState.Init)
                    {
                        item.Load();
                        _loadingBundles.Add(item);
                        _toloadBundles.RemoveAt(i);
                        --i;
                    }
                }


            for (var i = 0; i < _loadingBundles.Count; i++)
            {
                var item = _loadingBundles[i];
                if (item.Update())
                    continue;
                _loadingBundles.RemoveAt(i);
                --i;
            }

            if (_unusedBundles.Count <= 0) return;
            {
                for (var i = 0; i < _unusedBundles.Count; i++)
                {
                    var item = _unusedBundles[i];
                    if (item.isDone)
                    {
                        UnloadDependencies(item);
                        item.Unload();
                        Log("UnloadBundle: " + item.url);
                        _unusedBundles.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        private string RemapVariantName(string assetBundleName)
        {
            var bundlesWithVariant = _activeVariants;
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
                Debug.LogWarning(
                    "Ambiguous asset bundle variant chosen because there was no matching active variant: " +
                    bundlesWithVariant[bestFitIndex]);

            return bestFitIndex != -1 ? bundlesWithVariant[bestFitIndex] : assetBundleName;
        }



        #endregion

        #region Service

        public IEnumerator<IAsyncJobResult> Initialize(ServiceLocator serviceLocator)
        {
            Processor.onUpdate += Update;

            var init = Initialize();

            yield return new WaitForAssetMgrInitialized(init);

            if (!init.isError)
            {
                //AssetMgr.AddSearchPath("Assets/XAsset/Demo/Scenes");
            }
            else
            {
                Debug.LogError("[XAsset] Initialize Error");
            }
            init.Release();

            //Debug.LogError("all asset path: ");
            //foreach (var path in GetAllAssetPaths())
            //{
            //    Debug.LogError("\t" + path);
            //}

            Processor.onUpdate -= Update;
        }

        void IHasUpdate.Update()
        {
            UpdateAssets();
            UpdateBundles();
        }

        public Type[] GetDependencies()
        {
            return null;
        }

        public void Shutdown()
        {

        }

        private class WaitForAssetMgrInitialized : IAsyncJobResult, IJobDependency, IUnreliableJobDependency
        {
            private readonly ManifestRequest m_AssetsInitRequest;

            public WaitForAssetMgrInitialized(ManifestRequest assetsInitRequest)
            {
                m_AssetsInitRequest = assetsInitRequest;
            }

            public bool HasFailed()
            {
                return !string.IsNullOrEmpty(m_AssetsInitRequest.error);
            }

            public bool IsReady()
            {
                return m_AssetsInitRequest.isDone;
            }
        }

        #endregion
    }
}