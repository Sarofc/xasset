using System;
using System.Collections.Generic;
using System.IO;

namespace Saro.XAsset.Update
{
    public sealed class VersionList
    {
        public System.Version version;
        public Dictionary<string, VersionAssetInfo> versionAssetInfos;

        public bool IsValid()
        {
            return version != null &&
                versionAssetInfos != null &&
                versionAssetInfos.Count > 0;
        }


        public static System.Version LoadVersionOnly(string versionListPath)
        {
            using (var fs = File.OpenRead(versionListPath))
            {
                using (var br = new BinaryReader(fs))
                {
                    return new System.Version(br.ReadString());
                }
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            if (IsValid())
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

            versionAssetInfos = new Dictionary<string, VersionAssetInfo>(count);

            for (int i = 0; i < count; i++)
            {
                var versionFileInfo = new VersionAssetInfo();

                ref var versionFileInfoRef = ref versionFileInfo;
                versionFileInfoRef.Deserialize(reader);

                versionAssetInfos.Add(versionFileInfo.name, versionFileInfo);
            }
        }
    }

    public struct VersionAssetInfo
    {
        public string name;
        public string hash;
        public long length;
        public long offset;
        public string subName;

        public bool IsValid()
        {
            return length > 0 &&
                !string.IsNullOrEmpty(name) &&
                !string.IsNullOrEmpty(subName) &&
                !string.IsNullOrEmpty(hash);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(name);
            writer.Write(length);
            writer.Write(hash);
            writer.Write(offset);
            writer.Write(subName);
        }

        public void Deserialize(BinaryReader reader)
        {
            name = reader.ReadString();
            length = reader.ReadInt64();
            hash = reader.ReadString();
            offset = reader.ReadInt64();
            subName = reader.ReadString();
        }
    }
}