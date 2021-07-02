using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Saro.XAsset.Update
{
    /*
     * TODO
     * 
     * 1.改成通用组件?
     * 2.检测超时?
     * 
     */

    /// <summary>
    /// 断点续传下载器
    /// </summary>
    public class Download : DownloadHandlerScript, IDisposable, ICloneable
    {
        public int Id { get; set; }

        public string Error { get; internal set; }

        public long Offset { get; set; }

        public long Length { get; set; }

        public string Hash { get; set; }

        public string Url { get; set; }

        public long Position { get; private set; }

        public string Name { get; set; }

        public string TempPath
        {
            get
            {
                var dir = Path.GetDirectoryName(SavePath);
                return string.Format("{0}/{1}", dir, Hash);
            }
        }

        public bool Finished { get; private set; }

        public string SavePath { get; set; }

        public Action<Download> Completed { get; set; }

        private UnityWebRequest m_Request;
        private FileStream m_Stream;
        private bool m_Running;

        protected override float GetProgress()
        {
            return Position * 1f / Length;
        }

        protected override bool ReceiveData(byte[] buffer, int dataLength)
        {
            if (!string.IsNullOrEmpty(m_Request.error))
            {
                Error = m_Request.error;
                Complete();
                return true;
            }

            m_Stream.Write(buffer, 0, dataLength);
            Position += dataLength;

            return m_Running;
        }

        protected override void CompleteContent()
        {
            Complete();
        }

        public override string ToString()
        {
            return string.Format("{0}, size:{1}, hash:{2}, offset:{3}", Url, Length, Hash, Offset);
        }

        public void Start()
        {
            if (m_Running)
            {
                return;
            }

            Error = null;
            Finished = false;
            m_Running = true;
            m_Stream = new FileStream(TempPath, FileMode.OpenOrCreate, FileAccess.Write);
            Position = m_Stream.Length;
            if (Position < Length)
            {
                m_Stream.Seek(Position, SeekOrigin.Begin);
                m_Request = UnityWebRequest.Get(Url);
                m_Request.SetRequestHeader("Range", "bytes=" + (Offset + Position) + "-" + (Offset + Length - 1));
                m_Request.downloadHandler = this;
                m_Request.SendWebRequest();
                Debug.Log($"[{nameof(Download)}] Start Download：" + this);
            }
            else
            {
                Complete();
            }
        }

        public void Update()
        {
            if (m_Running)
            {
                if (m_Request.isDone && m_Request.downloadedBytes < (ulong)Length)
                {
                    Error = "unknown error: downloadedBytes < len";
                }
                if (!string.IsNullOrEmpty(m_Request.error))
                {
                    Error = m_Request.error;
                }
            }
        }

        public new void Dispose()
        {
            if (m_Stream != null)
            {
                m_Stream.Close();
                m_Stream.Dispose();
                m_Stream = null;
            }
            if (m_Request != null)
            {
                m_Request.Abort();
                m_Request.Dispose();
                m_Request = null;
            }
            base.Dispose();
            m_Running = false;
            Finished = true;
        }

        public void Complete(bool stop = false)
        {
            Dispose();
            if (stop)
            {
                return;
            }
            Completed?.Invoke(this);
            Completed = null;
        }

        public void Retry()
        {
            Dispose();
            Start();
        }

        public object Clone()
        {
            return new Download()
            {
                Id = Id,
                Hash = Hash,
                Url = Url,
                Offset = Offset,
                Length = Length,
                SavePath = SavePath,
                Completed = Completed,
                Name = Name
            };
        }
    }
}