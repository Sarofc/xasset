using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using Saro.Core;

namespace Saro.XAsset.Build
{
    public sealed class BuildWindow : EditorWindow
    {
        [MenuItem("Tools/Build")]
        static void ShowBuildWindow()
        {
            var window = GetWindow<BuildWindow>();
            window.Show();
        }

        private class GUIStyles
        {
            public GUIStyle style_FontItalic;
            public GUIStyle style_FontBlodAndItalic;

            public GUIStyles()
            {
                style_FontItalic = new GUIStyle()
                {
                    fontStyle = FontStyle.Italic
                };

                style_FontBlodAndItalic = new GUIStyle()
                {
                    fontStyle = FontStyle.BoldAndItalic
                };
            }
        }

        static GUIStyles s_GUIStyles;

        private void OnEnable()
        {
            EnsureXAssetSettings();
            EnsureBuildMethods();
            EnsureProcedureSettings();
        }

        private void OnGUI()
        {
            if (s_GUIStyles == null)
            {
                s_GUIStyles = new GUIStyles();
            }

            DrawToolBar();
        }

        void DrawToolBar()
        {
            m_Selected = GUILayout.Toolbar(m_Selected, s_Toolbar);
            m_ScrolPos = EditorGUILayout.BeginScrollView(m_ScrolPos);
            switch (m_Selected)
            {
                case 0:
                    DrawProcedureSettings();
                    break;
                case 1:
                    DrawBuildSettings();
                    DrawButtons();
                    EditorGUILayout.Space();
                    DrawBuildOptions();
                    break;
                default:
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawProcedureSettings()
        {
            if (m_ProcedureSettings != null)
            {
                Editor.CreateCachedEditor(m_ProcedureSettings, typeof(ProcedureSettingsInspector), ref m_CachedEditor);
                if (m_CachedEditor != null)
                {
                    m_CachedEditor.OnInspectorGUI();
                }
            }
        }

        void DrawBuildSettings()
        {
            EditorGUILayout.LabelField("Platform: " + EditorUserBuildSettings.activeBuildTarget, s_GUIStyles.style_FontBlodAndItalic);

            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    EditorGUILayout.LabelField("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone));
                    break;
                case BuildTarget.iOS:
                    EditorGUILayout.LabelField("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS));
                    break;
                case BuildTarget.Android:
                    EditorGUILayout.LabelField("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
                    break;
                default:
                    break;
            }


            if (GUILayout.Button("Player Settings.."))
            {
                SettingsService.OpenProjectSettings("Project/Player");
            }

            EditorUserBuildSettings.development = EditorGUILayout.Toggle("Devepment Build: ", EditorUserBuildSettings.development);
            if (!EditorUserBuildSettings.development) GUI.enabled = false;
            EditorUserBuildSettings.allowDebugging = EditorGUILayout.Toggle("Script Debugging: ", EditorUserBuildSettings.allowDebugging);
            EditorUserBuildSettings.connectProfiler = EditorGUILayout.Toggle("Connect Profiler: ", EditorUserBuildSettings.connectProfiler);
            if (!EditorUserBuildSettings.development) GUI.enabled = true;


            EditorGUILayout.Space();

            if (m_XAssetSettings != null)
            {
                Editor.CreateCachedEditor(m_XAssetSettings, typeof(XAssetSettingsInspector), ref m_CachedEditor);
                if (m_CachedEditor != null)
                {
                    m_CachedEditor.OnInspectorGUI();
                }
            }
        }

        void DrawBuildOptions()
        {
            if (m_BuildMethods != null)
            {
                var rect = EditorGUILayout.GetControlRect();

                EditorGUI.LabelField(rect, "Build Pass：");

                rect.x = rect.width - 100f;
                rect.width = 100f;

                if (GUI.Button(rect, "Build Selected"))
                {
                    ExecuteAction(() =>
                    {
                        for (int i = 0; i < m_BuildMethods.Count; i++)
                        {
                            var buildMethod = m_BuildMethods[i];
                            if ((m_XAssetSettings.buildMethodOptions & (1 << i)) != 0)
                            {
                                if (buildMethod.callback.Invoke() == false)
                                {
                                    throw new System.Exception(string.Format("Execute {0} Failed, Abort！", buildMethod.description));
                                }

                                Debug.Log($"Execute {buildMethod.description} Successfull");
                            }
                        }
                    });
                }

                for (int i = 0; i < m_BuildMethods.Count; i++)
                {
                    DrawBuildMethod(i, m_BuildMethods[i]);
                }
            }
        }

        void DrawBuildMethod(int index, BuildMethod buildMethod)
        {
            if (buildMethod != null)
            {
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 8);
                EditorGUI.HelpBox(rect, string.Empty, MessageType.None);

                var rect1 = rect;
                rect1.y += 4f;
                rect1.x += 10f;
                rect1.width = 50f;

                buildMethod.selected = (m_XAssetSettings.buildMethodOptions & (1 << index)) != 0;
                buildMethod.selected = EditorGUI.ToggleLeft(rect1, string.Empty, buildMethod.selected);
                if (buildMethod.selected) m_XAssetSettings.buildMethodOptions |= 1 << index;
                else m_XAssetSettings.buildMethodOptions &= ~(1 << index);

                rect1.x = 40f;
                rect1.width = 300f;
                EditorGUI.LabelField(rect1, string.Format("[{0:00}] {1}", buildMethod.order, buildMethod.description), buildMethod.required ? s_GUIStyles.style_FontBlodAndItalic : s_GUIStyles.style_FontItalic);
                rect1.x = rect.width - 40;
                rect1.width = 40;
                rect1.height = EditorGUIUtility.singleLineHeight;

                if (GUI.Button(rect1, "Run"))
                {
                    ExecuteAction(() =>
                    {
                        if (buildMethod.callback.Invoke() == false)
                        {
                            EditorUtility.DisplayDialog("Failed", string.Format("Execute {0} failed!", buildMethod.description), "OK");
                        }
                    });
                }
            }
        }

        void DrawButtons()
        {
            if (GUILayout.Button("Run HFS"))
            {
                var absoluteHfsExe = System.IO.Path.GetFullPath("Packages/com.saro.xasset/Editor/HFS/hfs.exe");
                //Debug.LogError(absoluteHfsExe);
                Common.Cmder.Run(absoluteHfsExe);
            }
        }


        static string[] s_Toolbar = new string[]
        {
             "ProcedureSettings",
             "XAssetSettings"
        };

        Editor m_CachedEditor;
        List<BuildMethod> m_BuildMethods;
        XAssetSettings m_XAssetSettings;
        ProcedureSettings m_ProcedureSettings;
        private int m_Selected;
        private Vector2 m_ScrolPos;

        void EnsureBuildMethods()
        {
            m_BuildMethods = BuildMethod.BuildMethods;
        }

        void EnsureXAssetSettings()
        {
            m_XAssetSettings = BuildScript.GetXAssetSettings();
            BuildScript.GetXAssetManifest();
            BuildScript.GetXAssetBuildRules();
        }

        void EnsureProcedureSettings()
        {
            m_ProcedureSettings = BuildScript.GetAsset<ProcedureSettings>(ProcedureMgr.k_ProcedureSettingsPath);
        }

        void ExecuteAction(System.Action action)
        {
            EditorApplication.delayCall = () =>
            {
                EditorApplication.delayCall = null;
                if (action != null)
                {
                    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                    try
                    {
                        watch.Start();

                        action();

                        watch.Stop();
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            };
            EditorUtility.DisplayProgressBar("Wait...", "", 0);
        }
    }
}