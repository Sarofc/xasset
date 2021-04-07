using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Saro.XAsset.Update
{
    public sealed class Downloader : MonoBehaviour
    {
        private const float k_BYTES_2_MB = 1f / (1024 * 1024);

        public int maxDownloads = 3;

        private readonly List<Download> m_Downloads = new List<Download>();
        private readonly List<Download> m_ToStart = new List<Download>();
        private readonly List<Download> m_Progressing = new List<Download>();
        public Action<long, long, float> onUpdate;
        public Action onFinished;

        private int m_FinishedIndex;
        private int m_DownloadIndex;
        private float m_StartTime;
        private float m_LastTime;
        private long m_LastSize;

        public long Size { get; private set; }

        public long Position { get; private set; }

        public float Speed { get; private set; }

        public IReadOnlyList<Download> Downloads { get { return m_Downloads; } }

        private long GetDownloadSize()
        {
            var len = 0L;
            var downloadSize = 0L;
            foreach (var download in m_Downloads)
            {
                downloadSize += download.Position;
                len += download.Length;
            }
            return downloadSize - (len - Size);
        }

        private bool m_Started;
        [SerializeField] private float m_SampleTime = 0.5f;

        public void StartDownload()
        {
            m_ToStart.Clear();
            m_FinishedIndex = 0;
            m_LastSize = 0L;
            Restart();
        }

        public void Restart()
        {
            m_StartTime = Time.realtimeSinceStartup;
            m_LastTime = 0;
            m_Started = true;
            m_DownloadIndex = m_FinishedIndex;
            var max = Math.Min(m_Downloads.Count, maxDownloads);
            for (var i = m_FinishedIndex; i < max; i++)
            {
                var item = m_Downloads[i];
                m_ToStart.Add(item);
                m_DownloadIndex++;
            }
        }

        public void Stop()
        {
            m_ToStart.Clear();
            foreach (var download in m_Progressing)
            {
                download.Complete(true);
                m_Downloads[download.Id] = download.Clone() as Download;

            }
            m_Progressing.Clear();
            m_Started = false;
        }

        public void Clear()
        {
            Size = 0;
            Position = 0;

            m_DownloadIndex = 0;
            m_FinishedIndex = 0;
            m_LastTime = 0f;
            m_LastSize = 0L;
            m_StartTime = 0;
            m_Started = false;
            foreach (var item in m_Progressing)
            {
                item.Complete(true);
            }
            m_Progressing.Clear();
            m_Downloads.Clear();
            m_ToStart.Clear();
        }

        public void AddDownload(string url, string filename, string savePath, string hash, long offset, long length)
        {
            var download = new Download
            {
                Id = m_Downloads.Count,
                Url = url,
                Name = filename,
                Hash = hash,
                Offset = offset,
                Length = length,
                SavePath = savePath,
                Completed = OnFinished
            };
            m_Downloads.Add(download);
            var info = new FileInfo(download.TempPath);
            if (info.Exists)
            {
                Size += length - info.Length;
            }
            else
            {
                Size += length;
            }
        }

        private void OnFinished(Download download)
        {
            if (m_DownloadIndex < m_Downloads.Count)
            {
                m_ToStart.Add(m_Downloads[m_DownloadIndex]);
                m_DownloadIndex++;
            }
            m_FinishedIndex++;
            Debug.Log(string.Format("OnFinished:{0}, {1}", m_FinishedIndex, m_Downloads.Count));
            if (m_FinishedIndex != m_Downloads.Count)
                return;
            if (onFinished != null)
            {
                onFinished.Invoke();
            }
            m_Started = false;
        }

        public static string GetDisplaySpeed(float downloadSpeed)
        {
            if (downloadSpeed >= 1024 * 1024)
            {
                return string.Format("{0:f2}MB/s", downloadSpeed * k_BYTES_2_MB);
            }
            if (downloadSpeed >= 1024)
            {
                return string.Format("{0:f2}KB/s", downloadSpeed / 1024);
            }
            return string.Format("{0:f2}B/s", downloadSpeed);
        }

        public static string GetDisplaySize(long downloadSize)
        {
            if (downloadSize >= 1024 * 1024)
            {
                return string.Format("{0:f2}MB", downloadSize * k_BYTES_2_MB);
            }
            if (downloadSize >= 1024)
            {
                return string.Format("{0:f2}KB", downloadSize / 1024);
            }
            return string.Format("{0:f2}B", downloadSize);
        }


        private void Update()
        {
            if (!m_Started)
                return;

            if (m_ToStart.Count > 0)
            {
                for (var i = 0; i < Math.Min(maxDownloads, m_ToStart.Count); i++)
                {
                    var item = m_ToStart[i];
                    item.Start();
                    m_ToStart.RemoveAt(i);
                    m_Progressing.Add(item);
                    i--;
                }
            }

            for (var index = 0; index < m_Progressing.Count; index++)
            {
                var download = m_Progressing[index];
                download.Update();
                if (!download.Finished)
                    continue;
                m_Progressing.RemoveAt(index);
                index--;
            }

            Position = GetDownloadSize();

            var elapsed = Time.realtimeSinceStartup - m_StartTime;
            if (elapsed - m_LastTime < m_SampleTime)
                return;

            var deltaTime = elapsed - m_LastTime;
            Speed = (Position - m_LastSize) / deltaTime;
            if (onUpdate != null)
            {
                onUpdate(Position, Size, Speed);
            }

            m_LastTime = elapsed;
            m_LastSize = Position;
        }
    }
}