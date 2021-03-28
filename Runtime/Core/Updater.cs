using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Saro.UI;

namespace Saro.XAsset
{
    public interface IUpdater
    {
        void OnStart();

        void OnMessage(string msg);

        void OnProgress(float progress);

        void OnVersion(string ver);

        void OnClear();
    }

    [RequireComponent(typeof(Downloader))]
    [RequireComponent(typeof(NetworkMonitor))]
    public class Updater : MonoBehaviour, IUpdater, INetworkMonitorListener
    {
        private enum EStep
        {
            Wait,
            Copy,
            Coping,
            Versions,
            Prepared,
            Download,
        }

        private EStep _step;

        [SerializeField] private string baseURL = "http://127.0.0.1:7888/DLC/";
        //[SerializeField] private string gameScene = "Game.unity";
        //[SerializeField] private bool enableVFS = true;
        [SerializeField] private bool development;

        public IUpdater listener { get; set; }

        private Downloader _downloader;
        private NetworkMonitor _monitor;
        private string _platform;
        private string _savePath;
        private List<VFile> _versions = new List<VFile>();


        public void OnMessage(string msg)
        {
            if (listener != null)
            {
                listener.OnMessage(msg);
            }
        }

        public void OnProgress(float progress)
        {
            if (listener != null)
            {
                listener.OnProgress(progress);
            }
        }

        public void OnVersion(string ver)
        {
            if (listener != null)
            {
                listener.OnVersion(ver);
            }
        }

        private void Start()
        {
            _downloader = gameObject.GetComponent<Downloader>();
            _downloader.onUpdate = OnUpdate;
            _downloader.onFinished = OnComplete;

            _monitor = gameObject.GetComponent<NetworkMonitor>();
            _monitor.listener = this;

            _savePath = string.Format("{0}/DLC/", Application.persistentDataPath);
            _platform = GetPlatformForAssetBundles(Application.platform);

            _step = EStep.Wait;

            XAsset.s_UpdatePath = _savePath;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_reachabilityChanged || _step == EStep.Wait)
            {
                return;
            }

            if (hasFocus)
            {
                UIDialogue.CloseAll();
                if (_step == EStep.Download)
                {
                    _downloader.Restart();
                }
                else
                {
                    StartUpdate();
                }
            }
            else
            {
                if (_step == EStep.Download)
                {
                    _downloader.Stop();
                }
            }
        }

        private bool _reachabilityChanged;

        public void OnReachablityChanged(NetworkReachability reachability)
        {
            if (_step == EStep.Wait)
            {
                return;
            }

            _reachabilityChanged = true;
            if (_step == EStep.Download)
            {
                _downloader.Stop();
            }

            if (reachability == NetworkReachability.NotReachable)
            {
                UIDialogue.Show("提示！", "找不到网络，请确保手机已经联网", "确定", "退出").onComplete += delegate (UIDialogue.EventId id)
                {
                    if (id == UIDialogue.EventId.Ok)
                    {
                        if (_step == EStep.Download)
                        {
                            _downloader.Restart();
                        }
                        else
                        {
                            StartUpdate();
                        }
                        _reachabilityChanged = false;
                    }
                    else
                    {
                        Quit();
                    }
                };
            }
            else
            {
                if (_step == EStep.Download)
                {
                    _downloader.Restart();
                }
                else
                {
                    StartUpdate();
                }
                _reachabilityChanged = false;
                UIDialogue.CloseAll();
            }
        }

        private void OnUpdate(long progress, long size, float speed)
        {
            OnMessage(string.Format("下载中...{0}/{1}, 速度：{2}",
                Downloader.GetDisplaySize(progress),
                Downloader.GetDisplaySize(size),
                Downloader.GetDisplaySpeed(speed)));

            OnProgress(progress * 1f / size);
        }

        public void Clear()
        {
            UIDialogue.Show("提示", "清除数据后所有数据需要重新下载，请确认！", "清除").onComplete += id =>
            {
                if (id != UIDialogue.EventId.Ok)
                    return;
                OnClear();
            };
        }

        public void OnClear()
        {
            OnMessage("数据清除完毕");
            OnProgress(0);
            _versions.Clear();
            _downloader.Clear();
            _step = EStep.Wait;
            _reachabilityChanged = false;

            XAsset.Get().Clear();

            if (listener != null)
            {
                listener.OnClear();
            }

            if (Directory.Exists(_savePath))
            {
                Directory.Delete(_savePath, true);
            }
        }

        public void OnStart()
        {
            if (listener != null)
            {
                listener.OnStart();
            }
        }

        private IEnumerator _checking;

        public void StartUpdate()
        {
            Debug.Log("StartUpdate.Development:" + development);
#if UNITY_EDITOR
            if (development)
            {
                XAsset.s_RuntimeMode = false;
                StartCoroutine(LoadGameScene());
                return;
            }
#endif
            OnStart();

            if (_checking != null)
            {
                StopCoroutine(_checking);
            }

            _checking = Checking();

            StartCoroutine(_checking);
        }

        private void AddDownload(VFile item)
        {
            _downloader.AddDownload(GetDownloadURL(item.Name), item.Name, _savePath + item.Name, item.Hash, item.Len);
        }

        private void PrepareDownloads()
        {
            //if (enableVFS)
            //{
            //    var path = string.Format("{0}{1}", _savePath, Versions.Dataname);
            //    if (!File.Exists(path))
            //    {
            //        AddDownload(_versions[0]);
            //        return;
            //    }

            //    Versions.LoadDisk(path);
            //}

            for (var i = 1; i < _versions.Count; i++)
            {
                var item = _versions[i];
                if (Versions.IsNew(string.Format("{0}{1}", _savePath, item.Name), item.Len, item.Hash))
                {
                    AddDownload(item);
                }
            }
        }

        //private IEnumerator RequestVFS()
        //{
        //    var mb = MessageBox.Show("提示", "是否开启VFS？开启有助于提升IO性能和数据安全。", "开启");
        //    yield return mb;
        //    enableVFS = mb.isOk;
        //}

        private static string GetPlatformForAssetBundles(RuntimePlatform target)
        {
            switch (target)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "Windows";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return "iOS"; // OSX
                default:
                    return null;
            }
        }

        private string GetDownloadURL(string filename)
        {
            return string.Format("{0}{1}/{2}", baseURL, _platform, filename);
        }

        private IEnumerator Checking()
        {
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
            }

            //if (_step == EStep.Wait)
            //{
            //    yield return RequestVFS();
            //    _step = EStep.Copy;
            //}

            _step = EStep.Copy;

            if (_step == EStep.Copy)
            {
                yield return RequestCopy();
            }

            if (_step == EStep.Coping)
            {
                var path = _savePath + Versions.Filename + ".tmp";
                var versions = Versions.LoadVersions(path);
                var basePath = GetStreamingAssetsPath() + "/";
                yield return UpdateCopy(versions, basePath);
                _step = EStep.Versions;
            }

            if (_step == EStep.Versions)
            {
                yield return RequestVersions();
            }

            if (_step == EStep.Prepared)
            {
                OnMessage("正在检查版本信息...");
                var totalSize = _downloader.size;
                if (totalSize > 0)
                {
                    var tips = string.Format("发现内容更新，总计需要下载 {0} 内容", Downloader.GetDisplaySize(totalSize));
                    var mb = UIDialogue.Show("提示", tips, "下载", "退出");
                    yield return mb;
                    if (mb.isOk)
                    {
                        _downloader.StartDownload();
                        _step = EStep.Download;
                    }
                    else
                    {
                        Quit();
                    }
                }
                else
                {
                    OnComplete();
                }
            }
        }

        private IEnumerator RequestVersions()
        {
            OnMessage("正在获取版本信息...");
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                var mb = UIDialogue.Show("提示", "请检查网络连接状态", "重试", "退出");
                yield return mb;
                if (mb.isOk)
                {
                    StartUpdate();
                }
                else
                {
                    Quit();
                }
                yield break;
            }

            var request = UnityWebRequest.Get(GetDownloadURL(Versions.Filename));
            request.downloadHandler = new DownloadHandlerFile(_savePath + Versions.Filename);
            yield return request.SendWebRequest();
            var error = request.error;
            request.Dispose();
            if (!string.IsNullOrEmpty(error))
            {
                var mb = UIDialogue.Show("提示", string.Format("获取服务器版本失败：{0}", error), "重试", "退出");
                yield return mb;
                if (mb.isOk)
                {
                    StartUpdate();
                }
                else
                {
                    Quit();
                }
                yield break;
            }
            try
            {
                _versions = Versions.LoadVersions(_savePath + Versions.Filename, true);
                if (_versions.Count > 0)
                {
                    PrepareDownloads();
                    _step = EStep.Prepared;
                }
                else
                {
                    OnComplete();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UIDialogue.Show("提示", "版本文件加载失败", "重试", "退出").onComplete +=
                    delegate (UIDialogue.EventId id)
                    {
                        if (id == UIDialogue.EventId.Ok)
                        {
                            StartUpdate();
                        }
                        else
                        {
                            Quit();
                        }
                    };
            }
        }

        private static string GetStreamingAssetsPath()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return Application.streamingAssetsPath;
            }

            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                return "file:///" + Application.streamingAssetsPath;
            }

            return "file://" + Application.streamingAssetsPath;
        }

        private IEnumerator RequestCopy()
        {
            var v1 = Versions.LoadVersion(_savePath + Versions.Filename);
            var basePath = GetStreamingAssetsPath() + "/";
            var request = UnityWebRequest.Get(basePath + Versions.Filename);
            var path = _savePath + Versions.Filename + ".tmp";
            request.downloadHandler = new DownloadHandlerFile(path);
            yield return request.SendWebRequest();
            if (string.IsNullOrEmpty(request.error))
            {
                var v2 = Versions.LoadVersion(path);
                if (v2 > v1)
                {
                    var mb = UIDialogue.Show("提示", "是否将资源解压到本地？", "解压", "跳过");
                    yield return mb;
                    _step = mb.isOk ? EStep.Coping : EStep.Versions;
                }
                else
                {
                    Versions.LoadVersions(path);
                    _step = EStep.Versions;
                }
            }
            else
            {
                _step = EStep.Versions;
            }
            request.Dispose();
        }

        private IEnumerator UpdateCopy(IList<VFile> versions, string basePath)
        {
            var version = versions[0];
            if (version.Name.Equals(Versions.Dataname))
            {
                var request = UnityWebRequest.Get(basePath + version.Name);
                request.downloadHandler = new DownloadHandlerFile(_savePath + version.Name);
                var req = request.SendWebRequest();
                while (!req.isDone)
                {
                    OnMessage("正在复制文件");
                    OnProgress(req.progress);
                    yield return null;
                }

                request.Dispose();
            }
            else
            {
                for (var index = 0; index < versions.Count; index++)
                {
                    var item = versions[index];
                    var request = UnityWebRequest.Get(basePath + item.Name);
                    request.downloadHandler = new DownloadHandlerFile(_savePath + item.Name);
                    yield return request.SendWebRequest();
                    request.Dispose();
                    OnMessage(string.Format("正在复制文件：{0}/{1}", index, versions.Count));
                    OnProgress(index * 1f / versions.Count);
                }
            }
        }

        private void OnComplete()
        {
            //if (enableVFS)
            //{
            //    var dataPath = _savePath + Versions.Dataname;
            //    var downloads = _downloader.downloads;
            //    if (downloads.Count > 0 && File.Exists(dataPath))
            //    {
            //        OnMessage("更新本地版本信息");
            //        var files = new List<VFile>(downloads.Count);
            //        foreach (var download in downloads)
            //        {
            //            files.Add(new VFile
            //            {
            //                name = download.name,
            //                hash = download.hash,
            //                len = download.len,
            //            });
            //        }

            //        var file = files[0];
            //        if (!file.name.Equals(Versions.Dataname))
            //        {
            //            Versions.UpdateDisk(dataPath, files);
            //        }
            //    }

            //    Versions.LoadDisk(dataPath);
            //}

            OnProgress(1);
            OnMessage("更新完成");
            var version = Versions.LoadVersion(_savePath + Versions.Filename);
            if (version > 0)
            {
                OnVersion(version.ToString());
            }

            StartCoroutine(LoadGameScene());
        }

        private IEnumerator LoadGameScene()
        {
            yield break;

            //OnMessage("正在初始化");
            //var init = AssetMgr.Initialize();
            //yield return init;
            //if (string.IsNullOrEmpty(init.error))
            //{
            //    AssetMgr.AddSearchPath("Assets/XAsset/Demo/Scenes");
            //    init.Release();
            //    OnProgress(0);
            //    OnMessage("加载游戏场景");
            //    var scene = AssetMgr.LoadSceneAsync(gameScene, false);
            //    while (!scene.isDone)
            //    {
            //        OnProgress(scene.progress);
            //        yield return null;
            //    }
            //}
            //else
            //{
            //    init.Release();
            //    var mb = MessageBox.Show("提示", "初始化异常错误：" + init.error + "请联系技术支持");
            //    yield return mb;
            //    Quit();
            //}
        }

        private void OnDestroy()
        {
            UIDialogue.Dispose();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
