using System;
using System.Collections.Generic;
using System.IO;

namespace Saro.XAsset
{
    public class VFile
    {
        public string Name { get; set; }
        public long Id { get; set; }
        public string Hash { get; set; }
        public long Len { get; set; }
        public long Offset { get; set; }

        public VFile()
        {
            Offset = -1;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Len);
            writer.Write(Hash);
        }

        public void Deserialize(BinaryReader reader)
        {
            Name = reader.ReadString();
            Len = reader.ReadInt64();
            Hash = reader.ReadString();
        }
    }

    public class VDisk
    {
        private readonly byte[] _buffers = new byte[1024 * 4];
        private readonly Dictionary<string, VFile> _data = new Dictionary<string, VFile>(StringComparer.Ordinal);
        private readonly List<VFile> _files = new List<VFile>();
        public List<VFile> files { get { return _files; } }
        public string name { get; set; }
        private long _pos;
        private long _len;

        public VDisk()
        {
        }

        public bool Exists()
        {
            return files.Count > 0;
        }

        private void AddFile(VFile file)
        {
            _data[file.Name] = file;
            files.Add(file);
        }

        public void AddFile(string path, long len, string hash)
        {
            var file = new VFile { Name = path, Len = len, Hash = hash };
            AddFile(file);
        }

        private void WriteFile(string path, BinaryWriter writer)
        {
            using (var fs = File.OpenRead(path))
            {
                var len = fs.Length;
                WriteStream(len, fs, writer);
            }
        }

        private void WriteStream(long len, Stream stream, BinaryWriter writer)
        {
            var count = 0L;
            while (count < len)
            {
                var read = (int)Math.Min(len - count, _buffers.Length);
                stream.Read(_buffers, 0, read);
                writer.Write(_buffers, 0, read);
                count += read;
            }
        }

        public bool Load(string path)
        {
            // issue
            // android, webgl error
            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogError("[XAsset] File:Exists() return false.");
                return false;
            }

            Clear();

            name = path;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var file = new VFile { Id = i };
                    file.Deserialize(reader);
                    AddFile(file);
                }
                _pos = reader.BaseStream.Position;
            }
            Reindex();
            return true;
        }

        public void Reindex()
        {
            _len = 0L;
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                file.Offset = _pos + _len;
                _len += file.Len;
            }
        }

        public VFile GetFile(string path, string hash)
        {
            var key = Path.GetFileName(path);
            VFile file;
            _data.TryGetValue(key, out file);
            return file;
        }

        public void Update(string dataPath, List<VFile> newFiles, List<VFile> saveFiles)
        {
            var dir = Path.GetDirectoryName(dataPath);
            using (var stream = File.OpenRead(dataPath))
            {
                foreach (var item in saveFiles)
                {
                    var path = string.Format("{0}/{1}", dir, item.Name);
                    if (File.Exists(path)) { continue; }
                    stream.Seek(item.Offset, SeekOrigin.Begin);
                    using (var fs = File.OpenWrite(path))
                    {
                        var count = 0L;
                        var len = item.Len;
                        while (count < len)
                        {
                            var read = (int)Math.Min(len - count, _buffers.Length);
                            stream.Read(_buffers, 0, read);
                            fs.Write(_buffers, 0, read);
                            count += read;
                        }
                    }
                    newFiles.Add(item);
                }
            }

            if (File.Exists(dataPath))
            {
                File.Delete(dataPath);
            }

            using (var stream = File.OpenWrite(dataPath))
            {
                var writer = new BinaryWriter(stream);
                writer.Write(newFiles.Count);
                foreach (var item in newFiles)
                {
                    item.Serialize(writer);
                }
                foreach (var item in newFiles)
                {
                    var path = string.Format("{0}/{1}", dir, item.Name);
                    WriteFile(path, writer);
                    File.Delete(path);
                    UnityEngine.Debug.Log("Delete:" + path);
                }
            }
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(name);
            using (var stream = File.OpenWrite(name))
            {
                var writer = new BinaryWriter(stream);
                writer.Write(files.Count);
                foreach (var item in files)
                {
                    item.Serialize(writer);
                }
                foreach (var item in files)
                {
                    var path = dir + "/" + item.Name;
                    WriteFile(path, writer);
                }
            }
        }

        public void Clear()
        {
            _data.Clear();
            files.Clear();
        }
    }
}