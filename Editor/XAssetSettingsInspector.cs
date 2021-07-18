using UnityEditor;

namespace Saro.XAsset.Build
{
    [CustomEditor(typeof(XAssetSettings))]
    public class XAssetSettingsInspector

#if UNITY_2019_1_OR_NEWER
        : Editor
    { }
#else 
        : BaseEditor<XAssetSettings>
    {
        protected override void GetExcludedPropertiesInInspector(List<string> excluded)
        {
            excluded.Add(Utility.RefelctionUtility.PropertyName(() => Target.buildAssetBundleOptions));
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUI.BeginChangeCheck();

            Target.buildAssetBundleOptions = (BuildAssetBundleOptions)EditorGUILayout.EnumFlagsField(Utility.RefelctionUtility.PropertyName(() => Target.buildAssetBundleOptions), Target.buildAssetBundleOptions);

            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }
    }
#endif

}