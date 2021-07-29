using UnityEditor.EditorTools;
using UnityEditor.Rendering.Universal.Path2D;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    internal class ShadowCasterPath : ScriptablePath
    {
        internal Bounds GetBounds()
        {
            ShadowCaster2D shadowCaster = (ShadowCaster2D)owner;
            Renderer m_Renderer = shadowCaster.GetComponent<Renderer>();
            if (m_Renderer != null)
            {
                return m_Renderer.bounds;
            }
            else
            {
                Collider2D collider = shadowCaster.GetComponent<Collider2D>();
                if (collider != null)
                    return collider.bounds;
            }

            return new Bounds(shadowCaster.transform.position, shadowCaster.transform.lossyScale);
        }

        public override void SetDefaultShape()
        {
            Clear();
            Bounds bounds = GetBounds();

            AddPoint(new ControlPoint(bounds.min));
            AddPoint(new ControlPoint(new Vector3(bounds.min.x, bounds.max.y)));
            AddPoint(new ControlPoint(bounds.max));
            AddPoint(new ControlPoint(new Vector3(bounds.max.x, bounds.min.y)));

            base.SetDefaultShape();
        }
    }


    [CustomEditor(typeof(ShadowCaster2D))]
    [CanEditMultipleObjects]
    internal class ShadowCaster2DEditor : PathComponentEditor<ShadowCasterPath>
    {
        [EditorTool("Edit Shadow Caster Shape", typeof(ShadowCaster2D))]
        class ShadowCaster2DShadowCasterShapeTool : ShadowCaster2DShapeTool {};

        private static class Styles
        {
            public static GUIContent shadowMode = EditorGUIUtility.TrTextContent("Use Renderer Silhouette", "When this and Self Shadows are enabled, the Renderer's silhouette is considered part of the shadow. When this is enabled and Self Shadows disabled, the Renderer's silhouette is excluded from the shadow.");
            public static GUIContent selfShadows = EditorGUIUtility.TrTextContent("Self Shadows", "When enabled, the Renderer casts shadows on itself.");
            public static GUIContent castsShadows = EditorGUIUtility.TrTextContent("Casts Shadows", "Specifies if this renderer will cast shadows");
            public static GUIContent sortingLayerPrefixLabel = EditorGUIUtility.TrTextContent("Target Sorting Layers", "Apply shadows to the specified sorting layers.");
            public static GUIContent shadowShapeProvider = EditorGUIUtility.TrTextContent("Shape Provider", "This allows a selected component provide a different shape from the Shadow Caster 2D shape. This component must implement IShadowShape2DProvider");
            public static GUIContent shadowShapeContract = EditorGUIUtility.TrTextContent("Contract Edge", "This contracts the edge of the shape given by the shape provider by the specified amount");
            
            public static GUIContent castingSource = EditorGUIUtility.TrTextContent("Casting Source", "Specifies the source of the shape used for projected shadows");
        }

        SerializedProperty m_UseRendererSilhouette;
        SerializedProperty m_CastsShadows;
        SerializedProperty m_SelfShadows;
        SerializedProperty m_ShadowShapeProvider;
        SerializedProperty m_ShadowShapeContract;
        SerializedProperty m_CastingSource;

        SortingLayerDropDown m_SortingLayerDropDown;


        public void OnEnable()
        {
            m_UseRendererSilhouette = serializedObject.FindProperty("m_UseRendererSilhouette");
            m_SelfShadows = serializedObject.FindProperty("m_SelfShadows");
            m_CastsShadows = serializedObject.FindProperty("m_CastsShadows");
            m_ShadowShapeProvider = serializedObject.FindProperty("m_ShadowShapeProvider");
            m_ShadowShapeContract = serializedObject.FindProperty("m_ShadowShapeContract");
            m_CastingSource = serializedObject.FindProperty("m_ShadowCastingSource");

            m_SortingLayerDropDown = new SortingLayerDropDown();
            m_SortingLayerDropDown.OnEnable(serializedObject, "m_ApplyToSortingLayers");

            
        }

        public void ShadowCaster2DSceneGUI()
        {
            ShadowCaster2D shadowCaster = target as ShadowCaster2D;

            Transform t = shadowCaster.transform;
            Vector3[] shape = shadowCaster.shapePath;
            Handles.color = Color.white;

            for (int i = 0; i < shape.Length - 1; ++i)
            {
                Handles.DrawAAPolyLine(4, new Vector3[] { t.TransformPoint(shape[i]), t.TransformPoint(shape[i + 1]) });
            }

            if (shape.Length > 1)
                Handles.DrawAAPolyLine(4, new Vector3[] { t.TransformPoint(shape[shape.Length - 1]), t.TransformPoint(shape[0]) });
        }

        public void ShadowCaster2DInspectorGUI<T>() where T : ShadowCaster2DShapeTool
        {
            DoEditButton<T>(PathEditorToolContents.icon, "Edit Shape");
            DoPathInspector<T>();
            DoSnappingInspector<T>();
        }

        public void OnSceneGUI()
        {
            if (m_CastsShadows.boolValue)
                ShadowCaster2DSceneGUI();
        }

        public bool HasRenderer()
        {
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    ShadowCaster2D shadowCaster = (ShadowCaster2D)targets[i];
                    Renderer renderer = shadowCaster.GetComponent<Renderer>();
                    if (renderer != null)
                        return true;
                }
            }

            return false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(!HasRenderer()))  // Done to support multiedit
            {
                EditorGUILayout.PropertyField(m_UseRendererSilhouette, Styles.shadowMode);
            }

            EditorGUILayout.PropertyField(m_CastingSource, Styles.castingSource);

            //EditorGUILayout.PropertyField(m_CastsShadows, Styles.castsShadows);
            EditorGUILayout.PropertyField(m_SelfShadows, Styles.selfShadows);

            m_SortingLayerDropDown.OnTargetSortingLayers(serializedObject, targets, Styles.sortingLayerPrefixLabel, null);

            if ((ShadowCaster2D.CastingSources)m_CastingSource.intValue == ShadowCaster2D.CastingSources.ShapeProvider)
            {
                EditorGUILayout.PropertyField(m_ShadowShapeProvider, Styles.shadowShapeProvider);
                EditorGUILayout.PropertyField(m_ShadowShapeContract, Styles.shadowShapeContract);
            }
            else if ((ShadowCaster2D.CastingSources)m_CastingSource.intValue == ShadowCaster2D.CastingSources.ShapeEditor)
                ShadowCaster2DInspectorGUI<ShadowCaster2DShadowCasterShapeTool>();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
