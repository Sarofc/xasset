using System;
using UnityEngine;

namespace Saro.XAsset
{
    [Serializable]
    public struct AssetRef
    {
        public string name;
        public int bundle;
        public int dir;
    }

    [Serializable]
    public struct BundleRef
    {
        public string name;
        public int id;
        public int[] deps;
        public long len;
        public string hash;
    }

    public class XAssetManifest : ScriptableObject
    {
        public string[] activeVariants = new string[0];
        public string[] dirs = new string[0];
        public AssetRef[] assets = new AssetRef[0];
        public BundleRef[] bundles = new BundleRef[0];
    }
}