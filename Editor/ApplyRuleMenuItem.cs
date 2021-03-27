using UnityEngine;
using UnityEditor;

namespace Saro.XAsset
{
    class ApplyRuleMenuItem
    {
        [MenuItem("Assets/Apply Rule/Text", false, 1)]
        private static void ApplyRuleText()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternText);
        }

        [MenuItem("Assets/Apply Rule/Prefab", false, 1)]
        private static void ApplyRulePrefab()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternPrefab);
        }

        [MenuItem("Assets/Apply Rule/PNG", false, 1)]
        private static void ApplyRulePNG()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternPng);
        }

        [MenuItem("Assets/Apply Rule/Material", false, 1)]
        private static void ApplyRuleMaterail()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternMaterial);
        }

        [MenuItem("Assets/Apply Rule/Controller", false, 1)]
        private static void ApplyRuleController()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternController);
        }

        [MenuItem("Assets/Apply Rule/Asset", false, 1)]
        private static void ApplyRuleAsset()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternAsset);
        }

        [MenuItem("Assets/Apply Rule/Scene", false, 1)]
        private static void ApplyRuleScene()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternScene);
        }

        [MenuItem("Assets/Apply Rule/Dir", false, 1)]
        private static void ApplyRuleDir()
        {
            var rules = BuildScript.GetBuildRules();
            AddRulesForSelection(rules, rules.searchPatternDir);
        }

        private static void AddRulesForSelection(BuildRules rules, string searchPattern)
        {
            var isDir = rules.searchPatternDir.Equals(searchPattern);
            foreach (var item in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(item);
                var rule = new BuildRule
                {
                    searchPath = path,
                    searchPattern = searchPattern,
                    nameBy = isDir ? NameBy.Directory : NameBy.Path
                };
                ArrayUtility.Add(ref rules.rules, rule);
            }

            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssets();
        }

    }
}
