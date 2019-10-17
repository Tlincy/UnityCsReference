// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using UnityEditor.Rendering;
using System.Globalization;

namespace UnityEditor
{
    public abstract class LightingWindowEnvironmentSection
    {
        public virtual void OnEnable() {}
        public virtual void OnDisable() {}
        public virtual void OnInspectorGUI() {}
    }

    internal class LightingWindowLightingTab
    {
        class Styles
        {
            public static readonly GUIContent OtherSettings = EditorGUIUtility.TrTextContent("Other Settings");
            public static readonly GUIContent DebugSettings = EditorGUIUtility.TrTextContent("Debug Settings");
            public static readonly GUIContent LightProbeVisualization = EditorGUIUtility.TrTextContent("Light Probe Visualization");
            public static readonly GUIContent DisplayWeights = EditorGUIUtility.TrTextContent("Display Weights");
            public static readonly GUIContent DisplayOcclusion = EditorGUIUtility.TrTextContent("Display Occlusion");
            public static readonly GUIContent HighlightInvalidCells = EditorGUIUtility.TrTextContent("Highlight Invalid Cells", "Highlight the invalid cells that cannot be used for probe interpolation.");

            public static readonly GUIStyle LabelStyle = EditorStyles.wordWrappedMiniLabel;
            public static readonly GUIContent ContinuousBakeLabel = EditorGUIUtility.TrTextContent("Auto Generate", "Automatically generates lighting data in the Scene when any changes are made to the lighting systems.");
            public static readonly GUIContent BuildLabel = EditorGUIUtility.TrTextContent("Generate Lighting", "Generates the lightmap data for the current master scene.  This lightmap data (for realtime and baked global illumination) is stored in the GI Cache. For GI Cache settings see the Preferences panel.");

            public static string[] BakeModeStrings =
            {
                "Bake Reflection Probes",
                "Clear Baked Data"
            };

            public static readonly float ButtonWidth = 90;
        }

        class DefaultEnvironmentSectionExtension : LightingWindowEnvironmentSection
        {
            Editor m_EnvironmentEditor;

            Editor environmentEditor
            {
                get
                {
                    if (m_EnvironmentEditor == null || m_EnvironmentEditor.target == null)
                    {
                        Editor.CreateCachedEditor(RenderSettings.GetRenderSettings(), typeof(LightingEditor), ref m_EnvironmentEditor);
                    }

                    return m_EnvironmentEditor;
                }
            }

            public override void OnInspectorGUI()
            {
                environmentEditor.OnInspectorGUI();
            }

            public override void OnDisable()
            {
                if (m_EnvironmentEditor != null)
                {
                    Object.DestroyImmediate(m_EnvironmentEditor);
                    m_EnvironmentEditor = null;
                }
            }
        }

        enum BakeMode
        {
            BakeReflectionProbes = 0,
            Clear = 1
        }

        LightingWindowEnvironmentSection m_EnvironmentSection;
        Editor          m_FogEditor;
        Editor          m_OtherRenderingEditor;
        SavedBool       m_ShowOtherSettings;
        SavedBool       m_ShowDebugSettings;
        SavedBool       m_ShowProbeDebugSettings;
        Object          m_RenderSettings = null;
        Vector2         m_ScrollPosition = Vector2.zero;

        LightingWindowBakeSettings m_BakeSettings;

        SerializedObject m_LightmapSettings;
        SerializedProperty m_WorkflowMode;
        SerializedProperty m_EnabledBakedGI;

        Type m_SRP = GraphicsSettings.currentRenderPipeline?.GetType();

        Object renderSettings
        {
            get
            {
                if (m_RenderSettings == null)
                    m_RenderSettings = RenderSettings.GetRenderSettings();

                return m_RenderSettings;
            }
        }

        LightingWindowEnvironmentSection environmentEditor
        {
            get
            {
                var currentSRP = GraphicsSettings.currentRenderPipeline?.GetType();
                if (m_EnvironmentSection != null && m_SRP != currentSRP)
                {
                    m_SRP = currentSRP;
                    m_EnvironmentSection.OnDisable();
                    m_EnvironmentSection = null;
                }

                if (m_EnvironmentSection == null)
                {
                    Type extensionType = RenderPipelineEditorUtility.FetchFirstCompatibleTypeUsingScriptableRenderPipelineExtension<LightingWindowEnvironmentSection>();
                    if (extensionType == null)
                        extensionType = typeof(DefaultEnvironmentSectionExtension);
                    LightingWindowEnvironmentSection extension = (LightingWindowEnvironmentSection)Activator.CreateInstance(extensionType);
                    m_EnvironmentSection = extension;
                    m_EnvironmentSection.OnEnable();
                }

                return m_EnvironmentSection;
            }
        }

        Editor fogEditor
        {
            get
            {
                if (m_FogEditor == null || m_FogEditor.target == null)
                {
                    Editor.CreateCachedEditor(renderSettings, typeof(FogEditor), ref m_FogEditor);
                }

                return m_FogEditor;
            }
        }

        Editor otherRenderingEditor
        {
            get
            {
                if (m_OtherRenderingEditor == null || m_OtherRenderingEditor.target == null)
                {
                    Editor.CreateCachedEditor(renderSettings, typeof(OtherRenderingEditor), ref m_OtherRenderingEditor);
                }

                return m_OtherRenderingEditor;
            }
        }

        public void OnEnable()
        {
            m_BakeSettings = new LightingWindowBakeSettings();
            m_BakeSettings.OnEnable();

            InitLightmapSettings();

            m_ShowOtherSettings = new SavedBool("LightingWindow.ShowOtherSettings", true);
            m_ShowDebugSettings = new SavedBool("LightingWindow.ShowDebugSettings", false);
            m_ShowProbeDebugSettings = new SavedBool("LightingWindow.ShowProbeDebugSettings", false);
        }

        public void OnDisable()
        {
            m_BakeSettings.OnDisable();
            environmentEditor.OnDisable();

            ClearCachedProperties();
        }

        void ClearCachedProperties()
        {
            if (m_EnvironmentSection != null)
            {
                m_EnvironmentSection.OnDisable();
                m_EnvironmentSection = null;
            }
            if (m_FogEditor != null)
            {
                Object.DestroyImmediate(m_FogEditor);
                m_FogEditor = null;
            }
            if (m_OtherRenderingEditor != null)
            {
                Object.DestroyImmediate(m_OtherRenderingEditor);
                m_OtherRenderingEditor = null;
            }
        }

        void DebugSettingsGUI()
        {
            m_ShowDebugSettings.value = EditorGUILayout.FoldoutTitlebar(m_ShowDebugSettings.value, Styles.DebugSettings, true);

            if (m_ShowDebugSettings.value)
            {
                EditorGUI.indentLevel++;

                m_ShowProbeDebugSettings.value = EditorGUILayout.Foldout(m_ShowProbeDebugSettings.value, Styles.LightProbeVisualization, true);

                if (m_ShowProbeDebugSettings.value)
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUI.indentLevel++;
                    LightProbeVisualization.lightProbeVisualizationMode = (LightProbeVisualization.LightProbeVisualizationMode)EditorGUILayout.EnumPopup(LightProbeVisualization.lightProbeVisualizationMode);
                    LightProbeVisualization.showInterpolationWeights = EditorGUILayout.Toggle(Styles.DisplayWeights, LightProbeVisualization.showInterpolationWeights);
                    LightProbeVisualization.showOcclusions = EditorGUILayout.Toggle(Styles.DisplayOcclusion, LightProbeVisualization.showOcclusions);
                    LightProbeVisualization.highlightInvalidCells = EditorGUILayout.Toggle(Styles.HighlightInvalidCells, LightProbeVisualization.highlightInvalidCells);
                    EditorGUI.indentLevel--;

                    if (EditorGUI.EndChangeCheck())
                        EditorApplication.SetSceneRepaintDirty();
                }
                m_BakeSettings.DeveloperBuildSettingsGUI();

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }

        void OtherSettingsGUI()
        {
            if (SupportedRenderingFeatures.active.overridesFog && SupportedRenderingFeatures.active.overridesOtherLightingSettings)
                return;

            m_ShowOtherSettings.value = EditorGUILayout.FoldoutTitlebar(m_ShowOtherSettings.value, Styles.OtherSettings, true);

            if (m_ShowOtherSettings.value)
            {
                EditorGUI.indentLevel++;

                if (!SupportedRenderingFeatures.active.overridesFog)
                    fogEditor.OnInspectorGUI();

                if (!SupportedRenderingFeatures.active.overridesOtherLightingSettings)
                    otherRenderingEditor.OnInspectorGUI();

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }

        public void OnGUI()
        {
            EditorGUIUtility.hierarchyMode = true;

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            if (!SupportedRenderingFeatures.active.overridesEnvironmentLighting)
                environmentEditor.OnInspectorGUI();

            m_BakeSettings.OnGUI();
            OtherSettingsGUI();
            DebugSettingsGUI();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            Buttons();
            Summary();
        }

        void BakeDropDownCallback(object data)
        {
            BakeMode mode = (BakeMode)data;

            switch (mode)
            {
                case BakeMode.Clear:
                    DoClear();
                    break;
                case BakeMode.BakeReflectionProbes:
                    DoBakeReflectionProbes();
                    break;
            }
        }

        void InitLightmapSettings()
        {
            if (m_LightmapSettings == null || m_LightmapSettings.targetObject == null)
            {
                m_LightmapSettings = new SerializedObject(LightmapEditorSettings.GetLightmapSettings());
                m_EnabledBakedGI = m_LightmapSettings.FindProperty("m_GISettings.m_EnableBakedLightmaps");
                m_WorkflowMode = m_LightmapSettings.FindProperty("m_GIWorkflowMode");
            }
        }

        void Buttons()
        {
            InitLightmapSettings();

            m_LightmapSettings.Update();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (Lightmapping.lightingDataAsset && !Lightmapping.lightingDataAsset.isValid)
                {
                    EditorGUILayout.HelpBox(Lightmapping.lightingDataAsset.validityErrorMessage, MessageType.Warning);
                }

                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                Rect rect = GUILayoutUtility.GetRect(Styles.ContinuousBakeLabel, GUIStyle.none);
                EditorGUI.BeginProperty(rect, Styles.ContinuousBakeLabel, m_WorkflowMode);

                bool iterative = m_WorkflowMode.intValue == (int)Lightmapping.GIWorkflowMode.Iterative;

                // Continous mode checkbox
                EditorGUI.BeginChangeCheck();
                iterative = GUILayout.Toggle(iterative, Styles.ContinuousBakeLabel);

                if (EditorGUI.EndChangeCheck())
                {
                    m_WorkflowMode.intValue = (int)(iterative ? Lightmapping.GIWorkflowMode.Iterative : Lightmapping.GIWorkflowMode.OnDemand);
                }

                EditorGUI.EndProperty();

                using (new EditorGUI.DisabledScope(iterative))
                {
                    // Bake button if we are not currently baking
                    bool showBakeButton = iterative || !Lightmapping.isRunning;
                    if (showBakeButton)
                    {
                        if (EditorGUI.ButtonWithDropdownList(Styles.BuildLabel, Styles.BakeModeStrings, BakeDropDownCallback, GUILayout.Width(170)))
                        {
                            DoBake();

                            // DoBake could've spawned a save scene dialog. This breaks GUI on mac (Case 490388).
                            // We work around this with an ExitGUI here.
                            GUIUtility.ExitGUI();
                        }
                    }
                    // Cancel button if we are currently baking
                    else
                    {
                        // Only show Force Stop when using the PathTracer backend
                        if (LightmapEditorSettings.lightmapper == LightmapEditorSettings.Lightmapper.ProgressiveCPU &&
                            m_EnabledBakedGI.boolValue &&
                            GUILayout.Button("Force Stop", GUILayout.Width(Styles.ButtonWidth)))
                        {
                            Lightmapping.ForceStop();
                        }
                        if (GUILayout.Button("Cancel", GUILayout.Width(Styles.ButtonWidth)))
                        {
                            Lightmapping.Cancel();
                        }
                    }
                }

                GUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            m_LightmapSettings.ApplyModifiedProperties();
        }

        private void DoBake()
        {
            Lightmapping.BakeAsync();
        }

        private void DoClear()
        {
            Lightmapping.ClearLightingDataAsset();
            Lightmapping.Clear();
        }

        private void DoBakeReflectionProbes()
        {
            Lightmapping.BakeAllReflectionProbesSnapshots();
        }

        void Summary()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            long totalMemorySize = 0;
            int lightmapCount = 0;
            Dictionary<Vector2, int> sizes = new Dictionary<Vector2, int>();
            bool directionalLightmapsMode = false;
            bool shadowmaskMode = false;
            foreach (LightmapData ld in LightmapSettings.lightmaps)
            {
                if (ld.lightmapColor == null)
                    continue;
                lightmapCount++;

                Vector2 texSize = new Vector2(ld.lightmapColor.width, ld.lightmapColor.height);
                if (sizes.ContainsKey(texSize))
                    sizes[texSize]++;
                else
                    sizes.Add(texSize, 1);

                totalMemorySize += TextureUtil.GetStorageMemorySizeLong(ld.lightmapColor);
                if (ld.lightmapDir)
                {
                    totalMemorySize += TextureUtil.GetStorageMemorySizeLong(ld.lightmapDir);
                    directionalLightmapsMode = true;
                }
                if (ld.shadowMask)
                {
                    totalMemorySize += TextureUtil.GetStorageMemorySizeLong(ld.shadowMask);
                    shadowmaskMode = true;
                }
            }
            StringBuilder sizesString = new StringBuilder();
            sizesString.Append(lightmapCount);
            sizesString.Append((directionalLightmapsMode ? " Directional" : " Non-Directional"));
            sizesString.Append(" Lightmap");
            if (lightmapCount != 1) sizesString.Append("s");
            if (shadowmaskMode)
            {
                sizesString.Append(" with Shadowmask");
                if (lightmapCount != 1) sizesString.Append("s");
            }

            bool first = true;
            foreach (var s in sizes)
            {
                sizesString.Append(first ? ": " : ", ");
                first = false;
                if (s.Value > 1)
                {
                    sizesString.Append(s.Value);
                    sizesString.Append("x");
                }
                sizesString.Append(s.Key.x.ToString(CultureInfo.InvariantCulture.NumberFormat));
                sizesString.Append("x");
                sizesString.Append(s.Key.y.ToString(CultureInfo.InvariantCulture.NumberFormat));
                sizesString.Append("px");
            }
            sizesString.Append(" ");

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label(sizesString.ToString(), Styles.LabelStyle);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(EditorUtility.FormatBytes(totalMemorySize), Styles.LabelStyle);
            GUILayout.Label((lightmapCount == 0 ? "No Lightmaps" : ""), Styles.LabelStyle);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            if (LightmapEditorSettings.lightmapper != LightmapEditorSettings.Lightmapper.Enlighten)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("Occupied Texels: " + InternalEditorUtility.CountToString(Lightmapping.occupiedTexelCount), Styles.LabelStyle);
                if (Lightmapping.isRunning)
                {
                    int numLightmapsInView = 0;
                    int numConvergedLightmapsInView = 0;
                    int numNotConvergedLightmapsInView = 0;

                    int numLightmapsNotInView = 0;
                    int numConvergedLightmapsNotInView = 0;
                    int numNotConvergedLightmapsNotInView = 0;

                    int numLightmaps = LightmapSettings.lightmaps.Length;
                    for (int i = 0; i < numLightmaps; ++i)
                    {
                        LightmapConvergence lc = Lightmapping.GetLightmapConvergence(i);
                        if (!lc.IsValid())
                        {
                            continue;
                        }

                        if (Lightmapping.GetVisibleTexelCount(i) > 0)
                        {
                            numLightmapsInView++;
                            if (lc.IsConverged())
                                numConvergedLightmapsInView++;
                            else
                                numNotConvergedLightmapsInView++;
                        }
                        else
                        {
                            numLightmapsNotInView++;
                            if (lc.IsConverged())
                                numConvergedLightmapsNotInView++;
                            else
                                numNotConvergedLightmapsNotInView++;
                        }
                    }
                    if (Lightmapping.atlasCount > 0)
                    {
                        int convergedMaps = numConvergedLightmapsInView + numConvergedLightmapsNotInView;
                        GUILayout.Label("Lightmap convergence: (" + convergedMaps + "/" + Lightmapping.atlasCount + ")", Styles.LabelStyle);
                    }
                    EditorGUILayout.LabelField("Lightmaps in view: " + numLightmapsInView, Styles.LabelStyle);
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.LabelField("Converged: " + numConvergedLightmapsInView, Styles.LabelStyle);
                    EditorGUILayout.LabelField("Not Converged: " + numNotConvergedLightmapsInView, Styles.LabelStyle);
                    EditorGUI.indentLevel -= 1;
                    EditorGUILayout.LabelField("Lightmaps not in view: " + numLightmapsNotInView, Styles.LabelStyle);
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.LabelField("Converged: " + numConvergedLightmapsNotInView, Styles.LabelStyle);
                    EditorGUILayout.LabelField("Not Converged: " + numNotConvergedLightmapsNotInView, Styles.LabelStyle);
                    EditorGUI.indentLevel -= 1;

                    LightProbesConvergence lpc = Lightmapping.GetLightProbesConvergence();
                    if (lpc.IsValid() && lpc.probeSetCount > 0)
                        GUILayout.Label("Light Probes convergence: (" + lpc.convergedProbeSetCount + "/" + lpc.probeSetCount + ")", Styles.LabelStyle);
                }
                float bakeTime = Lightmapping.GetLightmapBakeTimeTotal();
                float mraysPerSec = Lightmapping.GetLightmapBakePerformanceTotal();
                if (mraysPerSec >= 0.0)
                    GUILayout.Label("Bake Performance: " + mraysPerSec.ToString("0.00", CultureInfo.InvariantCulture.NumberFormat) + " mrays/sec", Styles.LabelStyle);
                if (!Lightmapping.isRunning)
                {
                    float bakeTimeRaw = Lightmapping.GetLightmapBakeTimeRaw();
                    if (bakeTime >= 0.0)
                    {
                        int time = (int)bakeTime;
                        int timeH = time / 3600;
                        time -= 3600 * timeH;
                        int timeM = time / 60;
                        time -= 60 * timeM;
                        int timeS = time;

                        int timeRaw = (int)bakeTimeRaw;
                        int timeRawH = timeRaw / 3600;
                        timeRaw -= 3600 * timeRawH;
                        int timeRawM = timeRaw / 60;
                        timeRaw -= 60 * timeRawM;
                        int timeRawS = timeRaw;

                        int oHeadTime = Math.Max(0, (int)(bakeTime - bakeTimeRaw));
                        int oHeadTimeH = oHeadTime / 3600;
                        oHeadTime -= 3600 * oHeadTimeH;
                        int oHeadTimeM = oHeadTime / 60;
                        oHeadTime -= 60 * oHeadTimeM;
                        int oHeadTimeS = oHeadTime;


                        GUILayout.Label("Total Bake Time: " + timeH.ToString("0") + ":" + timeM.ToString("00") + ":" + timeS.ToString("00"), Styles.LabelStyle);
                        if (Unsupported.IsDeveloperBuild())
                            GUILayout.Label("(Raw Bake Time: " + timeRawH.ToString("0") + ":" + timeRawM.ToString("00") + ":" + timeRawS.ToString("00") + ", Overhead: " + oHeadTimeH.ToString("0") + ":" + oHeadTimeM.ToString("00") + ":" + oHeadTimeS.ToString("00") + ")", Styles.LabelStyle);
                    }
                }
                string deviceName = Lightmapping.GetLightmapBakeGPUDeviceName();
                if (deviceName.Length > 0)
                    GUILayout.Label("Baking device: " + deviceName, Styles.LabelStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
        }
    }
} // namespace
