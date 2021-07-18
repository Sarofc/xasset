using Saro.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace Saro.XAsset.Update
{
    public sealed class VersionList
    {
        public Version version;
        public Dictionary<string, VersionAssetInfo> versionAssetInfos;


        [System.Flags]
        public enum EVerifyBy : byte
        {
            None = 0,
            Crc32 = 1,
            Md5 = 2,
        }

        public const string k_VersionFileName = "ver.bytes";
        public const string k_DatFileName = "dat{0}.bytes";

        public static readonly EVerifyBy s_VerifyBy = EVerifyBy.Crc32;

        public static System.Version LoadVersionOnly(BinaryReader reader)
        {
            return new System.Version(reader.ReadString());
        }

        public static System.Version LoadVersionOnly(string versionListPath)
        {
            if (!File.Exists(versionListPath))
                return null;

            FileStream fs = null;
            BinaryReader br = null;

            try
            {
                fs = File.OpenRead(versionListPath);
                br = new BinaryReader(fs);
                return new System.Version(br.ReadString());
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            finally
            {
                if (fs != null) fs.Close();
                if (br != null) br.Close();
            }

            return null;
        }

        public static VersionList LoadVersionList(string versionListPath)
        {
            if (!File.Exists(versionListPath))
            {
                return null;
            }

            var retList = new VersionList();

            using (var fs = File.OpenRead(versionListPath))
            {
                using (var br = new BinaryReader(fs))
                {
                    retList.Deserialize(br);
                }
            }

            return retList;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void BuildVersionList(string outputFolder, string datFolder, string[] bundles, Version version)
        {
#if UNITY_EDITOR

            if (!Directory.Exists(datFolder)) Directory.CreateDirectory(datFolder);

            var versionAssetInfos = new Dictionary<string, VersionAssetInfo>(bundles.Length);
            var versionList = new VersionList
            {
                version = version,
                versionAssetInfos = versionAssetInfos
            };

            // TODO 超过4g时，分文件

            var datFullPath = datFolder + "/" + k_DatFileName;

            using (var vfsStream = new CommonVFileSystemStream(datFullPath, EVFileSystemAccess.Write, true))
            {
                using (var vfs = VFileSystem.Create(datFullPath, EVFileSystemAccess.Write, vfsStream, 1024, 1024 * 32))
                {
                    foreach (var bundle in bundles)
                    {
                        var bundlePath = outputFolder + "/" + bundle;
                        using (var fs = File.OpenRead(bundlePath))
                        {
                            if (vfs.WriteFile(bundle, fs))
                            {
                                fs.Seek(-fs.Length, SeekOrigin.End);

                                var fileInfo = vfs.GetFileInfo(bundle);
                                var assetInfo = new VersionAssetInfo
                                {
                                    file = fileInfo.Name,
                                    hash = GetHashUseEVerifyBy(fs),
                                    length = fileInfo.Length,
                                    offset = fileInfo.Offset,
                                    pack = k_DatFileName
                                };
                                versionAssetInfos.Add(assetInfo.file, assetInfo);
                            }
                            else
                            {
                                UnityEngine.Debug.LogError($"[XAsset]. write vfs failed. vfsName: {k_DatFileName} fileName: {bundle}");
                                break;
                            }
                        }
                    }
                }
            }


            var versionFilePath = datFolder + "/" + k_VersionFileName;

            using (var fs = File.OpenWrite(versionFilePath))
            {
                using (var bw = new BinaryWriter(fs))
                {
                    versionList.Serialize(bw);
                }
            }

            File.WriteAllText(versionFilePath + ".dump.txt", versionList.Dump());

            // test
            //using (var vfsStream = new CommonVFileSystemStream(datFullPath, EVFileSystemAccess.Read, false))
            //{
            //    using (var vfs = VFileSystem.Load(datFullPath, EVFileSystemAccess.Read, vfsStream))
            //    {
            //        var fileInfos = vfs.GetAllFileInfos();
            //        foreach (var fileInfo in fileInfos)
            //        {
            //            var hash = Utility.HashUtility.GetCRC32Hash(vfs.ReadFile(fileInfo.Name));
            //            UnityEngine.Debug.LogError(fileInfo.Name + ": " + hash);
            //        }
            //    }
            //}

#endif
        }

        internal static string GetHashUseEVerifyBy(Stream stream)
        {
            if (s_VerifyBy == EVerifyBy.Crc32)
            {
                return Utility.HashUtility.GetCrc32Hash(stream);
            }
            else if (s_VerifyBy == EVerifyBy.Md5)
            {
                return Utility.HashUtility.GetMd5Hash(stream);
            }

            throw new NotImplementedException("hash function invalid.");
        }

        internal static bool VerifyHashUseEVerifyBy(string hash, Stream stream)
        {
            if (s_VerifyBy == EVerifyBy.Crc32)
            {
                return Utility.HashUtility.VerifyCrc32Hash(hash, Utility.HashUtility.GetCrc32Hash(stream));
            }
            else if (s_VerifyBy == EVerifyBy.Md5)
            {
                return Utility.HashUtility.VerifyMd5Hash(hash, Utility.HashUtility.GetMd5Hash(stream));
            }

            throw new NotImplementedException("hash function invalid.");
        }

        public bool IsValid()
        {
            return version != null &&
                versionAssetInfos != null &&
                versionAssetInfos.Count > 0;
        }

        public string Dump()
        {
            var sb = new System.Text.StringBuilder(1024);

            sb.Append("version: ").Append(version.ToString());
            sb.AppendLine();

            foreach (var item in versionAssetInfos)
            {
                item.Value.Dump(sb);
            }

            return sb.ToString();
        }

        public void Serialize(BinaryWriter writer)
        {
            if (!IsValid())
                throw new Exception("version list is invalid.");

            writer.Write(version.ToString());
            writer.Write(versionAssetInfos.Count);

            foreach (var item in versionAssetInfos)
            {
                item.Value.Serialize(writer);
            }
        }

        public void Deserialize(BinaryReader reader)
        {
            version = new System.Version(reader.ReadString());
            var count = reader.ReadInt32();

            versionAssetInfos = new Dictionary<string, VersionAssetInfo>(count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                var versionFileInfo = new VersionAssetInfo();
                ref var versionFileInfoRef = ref versionFileInfo;
                versionFileInfoRef.Deserialize(reader);

                versionAssetInfos.Add(versionFileInfo.file, versionFileInfo);
            }
        }
    }

    public struct VersionAssetInfo
    {
        public string file;
        public string hash;
        public long length;
        public long offset;
        public string pack;

        public bool IsValid()
        {
            return length > 0L &&
                !string.IsNullOrEmpty(file) &&
                !string.IsNullOrEmpty(pack) &&
                !string.IsNullOrEmpty(hash);
        }

        public bool IsNew(VersionAssetInfo other)
        {
            return string.Compare(hash, other.hash, StringComparison.OrdinalIgnoreCase) != 0;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(file);
            writer.Write(hash);
            writer.Write(length);
            writer.Write(offset);
            writer.Write(pack);
        }

        public void Deserialize(BinaryReader reader)
        {
            file = reader.ReadString();
            hash = reader.ReadString();
            length = reader.ReadInt64();
            offset = reader.ReadInt64();
            pack = reader.ReadString();
        }

        public void Dump(System.Text.StringBuilder builder)
        {
            builder
                .Append("file: ").AppendLine(file)
                .Append("  hash: ").AppendLine(hash)
                .Append("  length: ").AppendLine(length.ToString())
                .Append("  offset: ").AppendLine(offset.ToString())
                .Append("  pack: ").AppendLine(pack);
        }
    }
}