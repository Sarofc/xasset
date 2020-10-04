//
// Requests.cs
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

using Saro.Core;
using Saro.Core.Jobs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace XAsset
{
    public enum LoadState
    {
        Init,
        LoadAssetBundle,
        LoadAsset,
        Loaded,
        Unload,
    }

    public class AssetRequest : Reference, IAssetRequest, IEnumerator, IUnreliableJobDependency, IJobDependency, IAsyncJobResult
    {
        public Type assetType;
        public string url;

        public LoadState loadState { get; protected set; }

        public AssetRequest()
        {
            asset = null;
            loadState = LoadState.Init;
        }

        public virtual bool isDone
        {
            get { return true; }
        }

        public virtual bool isError
        {
            get
            {
                return !string.IsNullOrEmpty(error);
            }
        }

        public virtual float progress
        {
            get { return 1; }
        }

        public virtual string error { get; protected set; }

        public string text { get; protected set; }

        public byte[] bytes { get; protected set; }

        public Object asset { get; internal set; }

        internal virtual void Load()
        {
            if (!AssetMgr.runtimeMode && AssetMgr.loadDelegate != null)
                asset = AssetMgr.loadDelegate(url, assetType);
            if (asset == null)
            {
                error = "error! file not exist:" + url;
            }
        }

        internal virtual void Unload()
        {
            if (asset == null)
                return;

            if (!AssetMgr.runtimeMode)
            {
                if (!(asset is GameObject))
                    Resources.UnloadAsset(asset);
            }

            asset = null;
        }

        internal bool Update()
        {
            if (!isDone)
                return true;
            if (completed == null)
                return false;
            try
            {
                completed.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            completed = null;
            return false;
        }

        public Action<IAssetRequest> completed { get; set; }

        #region IEnumerator implementation

        public object Current
        {
            get { return null; }
        }

        public bool MoveNext()
        {
            return !isDone;
        }

        public void Reset()
        {
        }

        public void Dispose()
        {

        }

        #endregion

        #region Job

        public bool HasFailed()
        {
            return isDone && isError;
        }

        public bool IsReady()
        {
            return isDone && !isError;
        }

        #endregion
    }

    public class ManifestRequest : AssetRequest
    {
        private BundleRequest _request;
        private string _assetName;

        public override float progress
        {
            get
            {
                switch (loadState)
                {
                    case LoadState.LoadAssetBundle:
                        return _request.progress;

                    case LoadState.Loaded:
                        return 1f;
                }

                return string.IsNullOrEmpty(error) ? 1f : 0f;
            }
        }

        public override bool isDone
        {
            get
            {
                if (!string.IsNullOrEmpty(error))
                {
                    return true;
                }

                return loadState == LoadState.Loaded;
            }
        }

        internal override void Load()
        {
            _assetName = Path.GetFileName(url);
            if (AssetMgr.runtimeMode)
            {
                var assetBundleName = _assetName.Replace(".asset", ".unity3d").ToLower();
                _request = AssetMgr.Get().LoadBundle(assetBundleName, true);
                _request.completed = Request_completed;
                loadState = LoadState.LoadAssetBundle;
            }
            else
            {
                loadState = LoadState.Loaded;
            }
        }

        private void Request_completed(IAssetRequest ar)
        {
            _request.completed = null;
            if (_request.assetBundle == null)
            {
                base.error = "assetBundle == null";
            }
            else
            {
                var manifest = _request.assetBundle.LoadAsset<Manifest>(_assetName);
                if (manifest == null)
                {
                    base.error = "manifest == null";
                }
                else
                {
                    AssetMgr.Get().OnLoadManifest(manifest);
                    _request.assetBundle.Unload(true);
                    _request.assetBundle = null;
                }
            }

            loadState = LoadState.Loaded;
        }

        internal override void Unload()
        {
            if (_request != null)
            {
                _request.Release();
                _request = null;
            }
        }
    }

    public class BundleAssetRequest : AssetRequest
    {
        protected readonly string assetBundleName;
        protected BundleRequest bundle;

        public BundleAssetRequest(string bundle)
        {
            assetBundleName = bundle;
        }

        internal override void Load()
        {
            bundle = AssetMgr.Get().LoadBundle(assetBundleName);
            var assetName = Path.GetFileName(url);
            if (bundle == null) Debug.LogError("bundlerequest is null: " + assetBundleName);
            if (bundle.assetBundle == null) Debug.LogError("bundle is null: " + assetBundleName);
            asset = bundle.assetBundle.LoadAsset(assetName, assetType);
        }

        internal override void Unload()
        {
            if (bundle != null)
            {
                bundle.Release();
                bundle = null;
            }

            asset = null;
        }
    }

    public class BundleAssetAsyncRequest : BundleAssetRequest
    {
        private AssetBundleRequest _request;

        public BundleAssetAsyncRequest(string bundle)
            : base(bundle)
        {
        }

        public override bool isDone
        {
            get
            {
                if (error != null || bundle.error != null)
                    return true;

                for (int i = 0, max = bundle.dependencies.Count; i < max; i++)
                {
                    var item = bundle.dependencies[i];
                    if (item.error != null)
                        return true;
                }

                switch (loadState)
                {
                    case LoadState.Init:
                        return false;
                    case LoadState.Loaded:
                        return true;
                    case LoadState.LoadAssetBundle:
                        {
                            if (!bundle.isDone)
                                return false;

                            for (int i = 0, max = bundle.dependencies.Count; i < max; i++)
                            {
                                var item = bundle.dependencies[i];
                                if (!item.isDone)
                                    return false;
                            }

                            if (bundle.assetBundle == null)
                            {
                                error = "assetBundle == null";
                                return true;
                            }

                            var assetName = Path.GetFileName(url);
                            _request = bundle.assetBundle.LoadAssetAsync(assetName, assetType);
                            loadState = LoadState.LoadAsset;
                            break;
                        }
                    case LoadState.Unload:
                        break;
                    case LoadState.LoadAsset:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (loadState != LoadState.LoadAsset)
                    return false;
                if (!_request.isDone)
                    return false;
                asset = _request.asset;
                loadState = LoadState.Loaded;
                return true;
            }
        }

        public override float progress
        {
            get
            {
                var bundleProgress = bundle.progress;
                if (bundle.dependencies.Count <= 0)
                    return bundleProgress * 0.3f + (_request != null ? _request.progress * 0.7f : 0);
                for (int i = 0, max = bundle.dependencies.Count; i < max; i++)
                {
                    var item = bundle.dependencies[i];
                    bundleProgress += item.progress;
                }

                return bundleProgress / (bundle.dependencies.Count + 1) * 0.3f +
                       (_request != null ? _request.progress * 0.7f : 0);
            }
        }

        internal override void Load()
        {
            bundle = AssetMgr.Get().LoadBundleAsync(assetBundleName);
            loadState = LoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            _request = null;
            loadState = LoadState.Unload;
            base.Unload();
        }
    }

    public class SceneAssetRequest : AssetRequest
    {
        public readonly LoadSceneMode loadSceneMode;
        protected readonly string sceneName;
        public string assetBundleName;
        protected BundleRequest bundle;

        public SceneAssetRequest(string path, bool addictive)
        {
            url = path;
            AssetMgr.Get().GetAssetBundleName(path, out assetBundleName);
            sceneName = Path.GetFileNameWithoutExtension(url);
            loadSceneMode = addictive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        }

        public override float progress
        {
            get { return 1; }
        }

        internal override void Load()
        {
            if (!string.IsNullOrEmpty(assetBundleName))
            {
                bundle = AssetMgr.Get().LoadBundle(assetBundleName);
                if (bundle != null)
                    SceneManager.LoadScene(sceneName, loadSceneMode);
            }
            else
            {
                try
                {
                    SceneManager.LoadScene(sceneName, loadSceneMode);
                    loadState = LoadState.LoadAsset;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    error = e.ToString();
                    loadState = LoadState.Loaded;
                }
            }
        }

        internal override void Unload()
        {
            if (bundle != null)
                bundle.Release();

            if (loadSceneMode == LoadSceneMode.Additive)
            {
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                    SceneManager.UnloadSceneAsync(sceneName);
            }

            bundle = null;
        }
    }

    public class SceneAssetAsyncRequest : SceneAssetRequest
    {
        private AsyncOperation _request;

        public SceneAssetAsyncRequest(string path, bool addictive)
            : base(path, addictive)
        {
        }

        public override float progress
        {
            get
            {
                if (bundle == null)
                    return _request == null ? 0 : _request.progress;

                var bundleProgress = bundle.progress;
                if (bundle.dependencies.Count <= 0)
                    return bundleProgress * 0.3f + (_request != null ? _request.progress * 0.7f : 0);
                for (int i = 0, max = bundle.dependencies.Count; i < max; i++)
                {
                    var item = bundle.dependencies[i];
                    bundleProgress += item.progress;
                }

                return bundleProgress / (bundle.dependencies.Count + 1) * 0.3f +
                       (_request != null ? _request.progress * 0.7f : 0);
            }
        }

        public override bool isDone
        {
            get
            {
                switch (loadState)
                {
                    case LoadState.Loaded:
                        return true;
                    case LoadState.LoadAssetBundle:
                        {
                            if (bundle == null || bundle.error != null)
                                return true;

                            for (int i = 0, max = bundle.dependencies.Count; i < max; i++)
                            {
                                var item = bundle.dependencies[i];
                                if (item.error != null)
                                    return true;
                            }

                            if (!bundle.isDone)
                                return false;

                            for (int i = 0, max = bundle.dependencies.Count; i < max; i++)
                            {
                                var item = bundle.dependencies[i];
                                if (!item.isDone)
                                    return false;
                            }

                            LoadSceneAsync();

                            break;
                        }
                    case LoadState.Unload:
                        break;
                    case LoadState.LoadAsset:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (loadState != LoadState.LoadAsset)
                    return false;
                if (_request != null && !_request.isDone)
                    return false;
                loadState = LoadState.Loaded;
                return true;
            }
        }

        private void LoadSceneAsync()
        {
            try
            {
                _request = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
                loadState = LoadState.LoadAsset;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                error = e.ToString();
                loadState = LoadState.Loaded;
            }
        }

        internal override void Load()
        {
            if (!string.IsNullOrEmpty(assetBundleName))
            {
                bundle = AssetMgr.Get().LoadBundleAsync(assetBundleName);
                loadState = LoadState.LoadAssetBundle;
            }
            else
            {
                LoadSceneAsync();
            }
        }

        internal override void Unload()
        {
            base.Unload();
            _request = null;
        }
    }

    public class WebAssetRequest : AssetRequest
    {
        private UnityWebRequest _www;

        public override bool isDone
        {
            get
            {
                if (loadState == LoadState.Init)
                    return false;
                if (loadState == LoadState.Loaded)
                    return true;

                if (loadState == LoadState.LoadAsset)
                {
                    if (_www == null || !string.IsNullOrEmpty(_www.error))
                        return true;

                    if (_www.isDone)
                    {
                        if (assetType != typeof(Texture2D))
                        {
                            if (assetType != typeof(TextAsset))
                            {
                                if (assetType != typeof(AudioClip))
                                    bytes = _www.downloadHandler.data;
                                else
                                    asset = DownloadHandlerAudioClip.GetContent(_www);
                            }
                            else
                            {
                                text = _www.downloadHandler.text;
                            }
                        }
                        else
                        {
                            asset = DownloadHandlerTexture.GetContent(_www);
                        }

                        loadState = LoadState.Loaded;
                        return true;
                    }

                    return false;
                }

                return true;
            }
        }

        public override string error
        {
            get { return _www.error; }
        }

        public override float progress
        {
            get { return _www.downloadProgress; }
        }

        internal override void Load()
        {
            if (assetType == typeof(AudioClip))
            {
                _www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
            }
            else if (assetType == typeof(Texture2D))
            {
                _www = UnityWebRequestTexture.GetTexture(url);
            }
            else
            {
                _www = new UnityWebRequest(url);
                _www.downloadHandler = new DownloadHandlerBuffer();
            }

            _www.SendWebRequest();
            loadState = LoadState.LoadAsset;
        }

        internal override void Unload()
        {
            if (asset != null)
            {
                Object.Destroy(asset);
                asset = null;
            }

            if (_www != null)
                _www.Dispose();

            bytes = null;
            text = null;
        }
    }

    public class BundleRequest : AssetRequest
    {
        public readonly List<BundleRequest> dependencies = new List<BundleRequest>();

        public AssetBundle assetBundle
        {
            get { return asset as AssetBundle; }
            internal set { asset = value; }
        }

        internal override void Load()
        {
            asset = Versions.LoadAssetBundleFromFile(url);
            if (assetBundle == null)
                error = url + " LoadFromFile failed.";
        }

        internal override void Unload()
        {
            if (assetBundle == null)
                return;
            assetBundle.Unload(true);
            assetBundle = null;
        }
    }

    public class BundleAsyncRequest : BundleRequest
    {
        private AssetBundleCreateRequest _request;

        public override bool isDone
        {
            get
            {
                if (loadState == LoadState.Init)
                    return false;

                if (loadState == LoadState.Loaded)
                    return true;

                if (loadState == LoadState.LoadAssetBundle && _request.isDone)
                {
                    asset = _request.assetBundle;
                    if (_request.assetBundle == null)
                    {
                        error = string.Format("unable to load assetBundle:{0}", url);
                    }

                    loadState = LoadState.Loaded;
                }

                return _request == null || _request.isDone;
            }
        }

        public override float progress
        {
            get { return _request != null ? _request.progress : 0f; }
        }

        internal override void Load()
        {
            _request = Versions.LoadAssetBundleFromFileAsync(url);
            if (_request == null)
            {
                error = url + " LoadFromFile failed.";
                return;
            }

            loadState = LoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            _request = null;
            loadState = LoadState.Unload;
            base.Unload();
        }
    }

    public class WebBundleRequest : BundleRequest
    {
        private UnityWebRequest _request;

        public override string error
        {
            get { return _request != null ? _request.error : null; }
        }

        public override bool isDone
        {
            get
            {
                if (loadState == LoadState.Init)
                    return false;

                if (_request == null || loadState == LoadState.Loaded)
                    return true;

                if (_request.isDone)
                {
                    assetBundle = DownloadHandlerAssetBundle.GetContent(_request);
                    loadState = LoadState.Loaded;
                }

                return _request.isDone;
            }
        }

        public override float progress
        {
            get { return _request != null ? _request.downloadProgress : 0f; }
        }

        internal override void Load()
        {
            _request = UnityWebRequestAssetBundle.GetAssetBundle(url);
            _request.SendWebRequest();
            loadState = LoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            if (_request != null)
            {
                _request.Dispose();
                _request = null;
            }

            loadState = LoadState.Unload;
            base.Unload();
        }
    }
}