
using System.Collections.Generic;
using System.IO;

namespace Saro.XAsset.Update
{
    public static class VersionControl
    {
        [System.Flags]
        public enum EVerifyBy : byte
        {
            None = 0,
            Crc32 = 1,
            Md5 = 2,
        }

        public const string k_VersionFileName = "ver.bytes";

        public static readonly EVerifyBy s_VerifyBy = EVerifyBy.Crc32;

        public static VersionList LoadVersionList(string versionListPath)
        {
            if (File.Exists(versionListPath))
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

        public static void BuildVersionList(string outputPath, string[] bundles, System.Version version)
        {
            var versionFilePath = outputPath + "/" + k_VersionFileName;
            if (File.Exists(versionFilePath))
            {
                File.Delete(versionFilePath);
            }

            var versionAssetInfos = new Dictionary<string, VersionAssetInfo>(bundles.Length);
            var versionList = new VersionList
            {
                version = version,
                versionAssetInfos = versionAssetInfos
            };

            foreach (var bundle in bundles)
            {
                using (var fs = File.OpenRead(outputPath + "/" + bundle))
                {
                    var assetInfo = new VersionAssetInfo
                    {
                        name = bundle,
                        hash = Utility.GetCRC32Hash(fs),
                        length = fs.Length,
                        offset = 0
                    };
                    versionAssetInfos.Add(assetInfo.name, assetInfo);
                }
            }

            using (var fs = File.OpenWrite(versionFilePath))
            {
                using (var bw = new BinaryWriter(fs))
                {
                    versionList.Serialize(bw);
                }
            }
        }
    }
}