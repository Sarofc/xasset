using UnityEngine;

namespace Saro.XAsset
{
    public class XAssetSettings : ScriptableObject
    {
        [Tooltip("是否在编辑器下开启加载AssetBundle的模式，开启后需要先打AssetBundle")]
        public bool runtimeMode = false;
        //[Tooltip("开启虚拟文件系统")]
        //public bool enableVFS = false;
        [Tooltip("是否开启本地服务器器，可以用来做版本更新测试")]
        public bool localServer = false;
        [Tooltip("打包到单个文件夹（false: 生成带时间戳的文件夹）")]
        public bool buildSingleFolder = true;

        [HideInInspector]
        public int buildOptions;
    }
}