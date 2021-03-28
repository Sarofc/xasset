using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Saro.XAsset
{
    public class Download : DownloadHandlerScript, IDisposable, ICloneable
    {
        public int Id { get; set; }

        public string Error { get; private set; }

        public long Len { get; set; }

        public string Hash { get; set; }

        public string Url { get; set; }

        public long Position { get; private set; }

        public string Name { get; set; }

        public string TempPath
        {
            get
            {
                var dir = Path.GetDirectoryName(savePath);
                return string.Format("{0}/{1}", dir, Hash);
            }
        }

        public bool Finished { get; private set; }

        public string savePath;

        public Action<Download> Completed { get; set; }

        private UnityWebRequest m_Request;
        private FileStream m_Stream;
        private bool m_Running;

        protected override float GetProgress()
        {
            return Position * 1f / Len;
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
            return string.Format("{0}, size:{1}, hash:{2}", Url, Len, Hash);
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
            if (Position < Len)
            {
                m_Stream.Seek(Position, SeekOrigin.Begin);
                m_Request = UnityWebRequest.Get(Url);
                m_Request.SetRequestHeader("Range", "bytes=" + Position + "-");
                m_Request.downloadHandler = this;
                m_Request.SendWebRequest();
                Debug.Log("Start Download：" + Url);
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
                if (m_Request.isDone && m_Request.downloadedBytes < (ulong)Len)
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
            CheckError();
        }

        private void CheckError()
        {
            if (File.Exists(TempPath))
            {
                if (string.IsNullOrEmpty(Error))
                {
                    using (var fs = File.OpenRead(TempPath))
                    {
                        if (fs.Length != Len)
                        {
                            Error = "下载文件长度异常:" + fs.Length;
                        }
                        if (Versions.verifyBy == VerifyBy.Hash)
                        {
                            const StringComparison compare = StringComparison.OrdinalIgnoreCase;
                            if (!Hash.Equals(Utility.GetCRC32Hash(fs), compare))
                            {
                                Error = "下载文件哈希异常:" + Hash;
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(Error))
                {
                    File.Copy(TempPath, savePath, true);
                    File.Delete(TempPath);
                    Debug.Log("Complete Download：" + Url);
                    if (Completed == null)
                        return;
                    Completed.Invoke(this);
                    Completed = null;
                }
                else
                {
                    File.Delete(TempPath);
                }
            }
            else
            {
                Error = "文件不存在";
            }
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
                Len = Len,
                savePath = savePath,
                Completed = Completed,
                Name = Name
            };
        }
    }
}