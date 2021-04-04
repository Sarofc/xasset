
using Saro.UI;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Saro.XAsset.Update
{
    [RequireComponent(typeof(Downloader))]
    [RequireComponent(typeof(NetworkMonitor))]
    public sealed class ResourceUpdater : MonoBehaviour, IUpdater, INetworkMonitorListener
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

        private EStep m_Step;
        private Downloader m_Downloader;
        private NetworkMonitor m_NetworkMonitor;
        private string m_SavePath;

        private string m_BaseURL;
        private string m_Platform;
        private bool m_NetReachabilityChanged;

        private VersionList m_LoacalVersionList;
        private VersionList m_RemoteVersionList;

        public IUpdater Listener { get; set; }

        private IEnumerator m_Checking;

        private void Start()
        {
            m_Downloader = GetComponent<Downloader>();
            m_NetworkMonitor = GetComponent<NetworkMonitor>();
            m_NetworkMonitor.listener = this;

            m_Step = EStep.Wait;
        }

        private void OnDestroy()
        {
            UI.UIDialogue.Dispose();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (m_NetReachabilityChanged || m_Step == EStep.Wait) return;

            if (hasFocus)
            {
                UIDialogue.CloseAll();
                if (m_Step == EStep.Download)
                {
                    m_Downloader.Restart();
                }
                else
                {
                    StartUpdate();
                }
            }
            else
            {
                if (m_Step == EStep.Download)
                {
                    m_Downloader.Stop();
                }
            }
        }

        public void StartUpdate()
        {
            ((IUpdater)this).OnStart();

            if (m_Checking != null)
            {
                StopCoroutine(m_Checking);
            }

            m_Checking = Checking();
            StartCoroutine(m_Checking);
        }

        IEnumerator Checking()
        {
            if (!Directory.Exists(m_SavePath))
            {
                Directory.CreateDirectory(m_SavePath);
            }

            if (m_Step == EStep.Wait)
            {
                //yield return RequestVFS();
                m_Step = EStep.Copy;
            }

            if (m_Step == EStep.Copy)
            {
                yield return RequestCopy();
            }

            if (m_Step == EStep.Coping)
            {
                var tmpLocalVersionFile = GetTmpLocalVersionListPath();
                var versionList = VersionControl.LoadVersionList(tmpLocalVersionFile);
                var basePath = GetStreamingAssetsPath() + "/";
                yield return UpdateCopy(versionList, basePath);
                m_Step = EStep.Versions;
            }

            if (m_Step == EStep.Versions)
            {
                yield return RequestVersionList();
            }

            if (m_Step == EStep.Prepared)
            {
                ((IUpdater)this).OnMessage("正在检查版本信息...");
                var totalSize = m_Downloader.Size;
                if (totalSize > 0)
                {
                    var tips = string.Format("发现内容更新，总计需要下载 {0} 内容", Downloader.GetDisplaySize(totalSize));
                    var mb = UI.UIDialogue.Show("提示", tips, "下载", "退出");
                    yield return mb;
                    if (mb.isOk)
                    {
                        m_Downloader.StartDownload();
                        m_Step = EStep.Download;
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

        private string GetTmpLocalVersionListPath()
        {
            return m_SavePath + VersionControl.k_VersionFileName + ".tmp";
        }

        private IEnumerator RequestCopy()
        {
            var versionFilePath = m_SavePath + VersionControl.k_VersionFileName;
            var localVersion_1 = VersionList.LoadVersionOnly(versionFilePath);

            var basePath = GetStreamingAssetsPath() + "/";
            var request = UnityWebRequest.Get(basePath + VersionControl.k_VersionFileName);
            var tmpLocalVersionFilePath = GetTmpLocalVersionListPath();
            request.downloadHandler = new DownloadHandlerFile(tmpLocalVersionFilePath);
            yield return request.SendWebRequest();

            if (string.IsNullOrEmpty(request.error))
            {
                var localVersion_2 = VersionList.LoadVersionOnly(tmpLocalVersionFilePath);
                if (localVersion_1 == null && localVersion_2 != null)
                {
                    if (localVersion_2 > localVersion_1)
                    {
                        var mb = UIDialogue.Show("提示", "是否将资源解压到本地？", "解压", "跳过");
                        yield return mb;
                        m_Step = mb.isOk ? EStep.Coping : EStep.Versions;
                    }
                    else
                    {
                        m_Step = EStep.Versions;
                    }
                }
            }
            else
            {
                m_Step = EStep.Versions;
            }
            request.Dispose();
        }

        private IEnumerator RequestVersionList()
        {
            ((IUpdater)this).OnMessage("正在获取版本信息...");
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                var mb = UIDialogue.Show("提示", "请检查网络连接状态", "重试", "推出");
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

            var versionFilePath = m_SavePath + VersionControl.k_VersionFileName;

            var request = UnityWebRequest.Get(GetDownloadURL(VersionControl.k_VersionFileName));
            request.downloadHandler = new DownloadHandlerFile(versionFilePath);
            yield return request.SendWebRequest();
            var error = request.error;
            request.Dispose();
            if (!string.IsNullOrEmpty(error))
            {
                var mb = UI.UIDialogue.Show("提示", string.Format("获取服务器版本失败: {0}", error), "重试");
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
                m_RemoteVersionList = VersionControl.LoadVersionList(versionFilePath);
                if (m_RemoteVersionList.IsValid())
                {
                    PrepareDownloads();
                    m_Step = EStep.Prepared;
                }
                else
                {
                    OnComplete();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                UI.UIDialogue.Show("提示", "版本文件加载失败", "重试", "退出").onComplete +=
                     delegate (UI.UIDialogue.EventId id)
                     {
                         if (id == UI.UIDialogue.EventId.Ok)
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

        //private IEnumerator RequestVFS()
        //{
        //    var mb = UI.UIDialogue.Show("提示", "是否开启VFS？开启有助于提升IO性能和数据安全。", "开启");
        //    yield return mb;
        //    // TODO
        //    //enableVFS = mb.isOk;
        //}

        private IEnumerator UpdateCopy(VersionList versionList, string basePath)
        {
            var versionAssetInfos = versionList.versionAssetInfos;

            var index = 1;
            foreach (var item in versionAssetInfos)
            {
                var request = UnityWebRequest.Get(basePath + item.Value.name);
                yield return request.SendWebRequest();
                request.Dispose();
                ((IUpdater)this).OnMessage(string.Format("正在复制文件：{0}/{1}", index, versionAssetInfos.Count));
                ((IUpdater)this).OnProgress(index * 1f / versionAssetInfos.Count);
                index++;
            }
        }

        public void Clear()
        {
            UIDialogue.Show("提示", "清除数据后所有数据需要重新下载，请确认！", "清除").onComplete += id =>
            {
                if (id != UIDialogue.EventId.Ok)
                    return;
                ((IUpdater)this).OnClear();
            };
        }

        private void OnComplete()
        {
            // TODO 合并文件

            ((IUpdater)this).OnProgress(1);
            ((IUpdater)this).OnMessage("更新完成");

            var version = VersionList.LoadVersionOnly(m_SavePath + VersionControl.k_VersionFileName);
            if (version > new System.Version("0"))
            {
                ((IUpdater)this).OnVersion(version.ToString());
            }

            // TODO 加载主界面
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

        private void PrepareDownloads()
        {
            var assetInfos = m_RemoteVersionList.versionAssetInfos;
            foreach (var item in assetInfos)
            {
                AddDownload(item.Value);
            }
        }

        private void AddDownload(VersionAssetInfo item)
        {
            m_Downloader.AddDownload(GetDownloadURL(item.name), item.name, m_SavePath + item.name, item.hash, item.length);
        }

        private string GetDownloadURL(string k_VersionFileName)
        {
            return string.Format("{0}{1}/{2}", m_BaseURL, m_Platform, k_VersionFileName);
        }


        void IUpdater.OnClear()
        {
            ((IUpdater)this).OnMessage("数据清除完毕");
            ((IUpdater)this).OnProgress(0);

            m_Downloader.Clear();
            m_Step = EStep.Wait;
            m_NetReachabilityChanged = false;
            m_LoacalVersionList = null;
            m_RemoteVersionList = null;

            XAsset.Get().Clear();

            if (Listener != null)
            {
                Listener.OnClear();
            }

            if (Directory.Exists(m_SavePath))
            {
                Directory.Delete(m_SavePath, true);
            }
        }

        void IUpdater.OnMessage(string msg)
        {
            if (Listener != null)
            {
                Listener.OnMessage(msg);
            }
        }

        void IUpdater.OnProgress(float progress)
        {
            if (Listener != null)
            {
                Listener.OnProgress(progress);
            }
        }

        void IUpdater.OnStart()
        {
            if (Listener != null)
            {
                Listener.OnStart();
            }
        }

        void IUpdater.OnVersion(string ver)
        {
            if (Listener != null)
            {
                Listener.OnVersion(ver);
            }
        }

        void INetworkMonitorListener.OnReachablityChanged(NetworkReachability reachability)
        {
            if (m_Step == EStep.Wait) return;

            m_NetReachabilityChanged = true;
            if (m_Step == EStep.Download)
            {
                m_Downloader.Stop();
            }

            if (reachability == NetworkReachability.NotReachable)
            {
                UI.UIDialogue.Show("提示！", "找不到网络，请确保手机已经联网", "确定", "退出").onComplete += delegate (UIDialogue.EventId id)
                {
                    if (id == UIDialogue.EventId.Ok)
                    {
                        if (m_Step == EStep.Download)
                        {
                            m_Downloader.Restart();
                        }
                        else
                        {
                            StartUpdate();
                        }
                        m_NetReachabilityChanged = false;
                    }
                    else
                    {
                        Quit();
                    }
                };
            }
            else
            {
                if (m_Step == EStep.Download)
                {
                    m_Downloader.Restart();
                }
                else
                {
                    StartUpdate();
                }

                m_NetReachabilityChanged = false;
                UIDialogue.CloseAll();
            }
        }
    }
}