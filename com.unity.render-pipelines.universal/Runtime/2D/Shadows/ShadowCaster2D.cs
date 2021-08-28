using System;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Class <c>ShadowCaster2D</c> contains properties used for shadow casting
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]

    [AddComponentMenu("Rendering/2D/Shadow Caster 2D")]
    [MovedFrom("UnityEngine.Experimental.Rendering.Universal")]

    public class ShadowCaster2D : ShadowCasterGroup2D
    {
        public enum ComponentVersions
        {
            Version_Unserialized = 0,
            Version_1 = 1
        }
        const ComponentVersions k_CurrentComponentVersion = ComponentVersions.Version_1;
        [SerializeField] ComponentVersions m_ComponentVersion = ComponentVersions.Version_Unserialized;

        public enum ShadowCastingSources
        {
            None,
            ShapeEditor,
            ShapeProvider
        }

        [SerializeField] bool m_HasRenderer = false;
        [SerializeField] bool m_UseRendererSilhouette = true;
        [SerializeField] bool m_CastsShadows = true;
        [SerializeField] bool m_SelfShadows = false;
        [SerializeField] int[] m_ApplyToSortingLayers = null;
        [SerializeField] Vector3[] m_ShapePath = null;
        [SerializeField] int  m_ShapePathHash = 0;
        [SerializeField] Mesh m_Mesh;
        [SerializeField] int m_InstanceId;
        [SerializeField] Component m_ShadowShapeProvider;
        [SerializeField] float m_ShadowShapeContract;
        [SerializeField] ShadowCastingSources m_ShadowCastingSource = ShadowCastingSources.ShapeEditor;

        internal ShadowShape2D        m_ShadowShape;
        internal ShadowCasterGroup2D  m_ShadowCasterGroup = null;
        internal ShadowCasterGroup2D  m_PreviousShadowCasterGroup = null;
        internal NativeArray<Vector2> m_ShadowShapeVertices;
        internal NativeArray<ShadowShape2D.Edge> m_ShadowShapeEdges;

        [SerializeField]
        internal BoundingSphere m_ProjectedBoundingSphere;

        public Mesh mesh => m_Mesh;
        public Vector3[] shapePath => m_ShapePath;
        internal int shapePathHash { get { return m_ShapePathHash; } set { m_ShapePathHash = value; } }

        int m_PreviousShadowGroup = 0;
        bool m_PreviousCastsShadows = true;
        int m_PreviousPathHash = 0;

        internal Vector3    m_CachedPosition;
        internal Vector3    m_CachedLossyScale;
        internal Quaternion m_CachedRotation;
        internal Matrix4x4  m_CachedShadowMatrix;
        internal Matrix4x4  m_CachedInverseShadowMatrix;
        internal Matrix4x4  m_CachedLocalToWorldMatrix;

        internal override void CacheValues()
        {
            m_CachedPosition = transform.position;
            m_CachedLossyScale = transform.lossyScale;
            m_CachedRotation = transform.rotation;

            m_CachedShadowMatrix = Matrix4x4.TRS(m_CachedPosition, m_CachedRotation, Vector3.one);
            m_CachedInverseShadowMatrix = m_CachedShadowMatrix.inverse;

            m_CachedLocalToWorldMatrix = transform.localToWorldMatrix;
        }

        /// <summary>
        /// If selfShadows is true, useRendererSilhoutte specifies that the renderer's sihouette should be considered part of the shadow. If selfShadows is false, useRendererSilhoutte specifies that the renderer's sihouette should be excluded from the shadow
        /// </summary>
        public bool useRendererSilhouette
        {
            set { m_UseRendererSilhouette = value; }
            get { return m_UseRendererSilhouette && m_HasRenderer;  }
        }

        /// <summary>
        /// If true, the shadow casting shape is included as part of the shadow. If false, the shadow casting shape is excluded from the shadow.
        /// </summary>
        public bool selfShadows
        {
            set { m_SelfShadows = value; }
            get { return m_SelfShadows; }
        }

        /// <summary>
        /// Specifies if shadows will be cast.
        /// </summary>
        public bool castsShadows
        {
            set { m_CastsShadows = value; }
            get { return m_CastsShadows; }
        }

        static int[] SetDefaultSortingLayers()
        {
            int layerCount = SortingLayer.layers.Length;
            int[] allLayers = new int[layerCount];

            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                allLayers[layerIndex] = SortingLayer.layers[layerIndex].id;
            }

            return allLayers;
        }

        internal bool IsLit(Light2D light)
        {
            // Oddly adding and subtracting vectors is expensive here because of the new structures created...
            Vector3 deltaPos;
            deltaPos.x = m_ProjectedBoundingSphere.position.x + m_CachedPosition.x;
            deltaPos.y = m_ProjectedBoundingSphere.position.y + m_CachedPosition.y;
            deltaPos.z = m_ProjectedBoundingSphere.position.z + m_CachedPosition.z;

            deltaPos.x = light.m_CachedPosition.x - deltaPos.x;
            deltaPos.y = light.m_CachedPosition.y - deltaPos.y;
            deltaPos.z = light.m_CachedPosition.z - deltaPos.z;

            float distanceSq = Vector3.SqrMagnitude(deltaPos);

            float radiiLength = light.boundingSphere.radius + m_ProjectedBoundingSphere.radius;
            return distanceSq <= (radiiLength * radiiLength);
        }

        internal bool IsShadowedLayer(int layer)
        {
            return m_ApplyToSortingLayers != null ? Array.IndexOf(m_ApplyToSortingLayers, layer) >= 0 : false;
        }

        void SetShadowShape()
        {
            if (m_Mesh == null)
                m_Mesh = new Mesh();

            if (m_ShadowShape == null)
                m_ShadowShape = new ShadowShape2D();

            if (m_ShadowCastingSource == ShadowCastingSources.ShapeEditor)
                m_ShadowShape.SetShape(m_ShapePath, null, IShadowShape2DProvider.OutlineTopology.LineStrip);

            m_ShadowShape.GenerateShadowMesh(m_Mesh, ref m_ProjectedBoundingSphere, m_ShadowShapeContract);
        }

        private void Awake()
        {
            if (m_ApplyToSortingLayers == null)
                m_ApplyToSortingLayers = SetDefaultSortingLayers();

            Bounds bounds = new Bounds(transform.position, Vector3.one);

            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                bounds = renderer.bounds;
            }
#if USING_PHYSICS2D_MODULE
            else
            {
                Collider2D collider = GetComponent<Collider2D>();
                if (collider != null)
                    bounds = collider.bounds;
            }
#endif
            Vector3 inverseScale = Vector3.zero;
            Vector3 relOffset = transform.position;

            if (transform.lossyScale.x != 0 && transform.lossyScale.y != 0)
            {
                inverseScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y);
                relOffset = new Vector3(inverseScale.x * -transform.position.x, inverseScale.y * -transform.position.y);
            }

            if (m_ShapePath == null || m_ShapePath.Length == 0)
            {
                m_ShapePath = new Vector3[]
                {
                    relOffset + new Vector3(inverseScale.x * bounds.min.x, inverseScale.y * bounds.min.y),
                    relOffset + new Vector3(inverseScale.x * bounds.min.x, inverseScale.y * bounds.max.y),
                    relOffset + new Vector3(inverseScale.x * bounds.max.x, inverseScale.y * bounds.max.y),
                    relOffset + new Vector3(inverseScale.x * bounds.max.x, inverseScale.y * bounds.min.y),
                };
            }
        }

        protected void OnEnable()
        {
            if (m_Mesh == null || m_InstanceId != GetInstanceID())
            {
                SetShadowShape();
                m_InstanceId = GetInstanceID();
            }

            m_ShadowCasterGroup = null;
        }

        protected void OnDisable()
        {
            ShadowCasterGroup2DManager.RemoveFromShadowCasterGroup(this, m_ShadowCasterGroup);
        }

        public void Update()
        {
            Renderer renderer;
            m_HasRenderer = TryGetComponent<Renderer>(out renderer);

            bool rebuildMesh = LightUtility.CheckForChange(m_ShapePathHash, ref m_PreviousPathHash);
            if (rebuildMesh)
            {
                SetShadowShape();
            }

            m_PreviousShadowCasterGroup = m_ShadowCasterGroup;
            bool addedToNewGroup = ShadowCasterGroup2DManager.AddToShadowCasterGroup(this, ref m_ShadowCasterGroup);
            if (addedToNewGroup && m_ShadowCasterGroup != null)
            {
                if (m_PreviousShadowCasterGroup == this)
                    ShadowCasterGroup2DManager.RemoveGroup(this);

                ShadowCasterGroup2DManager.RemoveFromShadowCasterGroup(this, m_PreviousShadowCasterGroup);
                if (m_ShadowCasterGroup == this)
                    ShadowCasterGroup2DManager.AddGroup(this);
            }

            if (LightUtility.CheckForChange(m_ShadowGroup, ref m_PreviousShadowGroup))
            {
                ShadowCasterGroup2DManager.RemoveGroup(this);
                ShadowCasterGroup2DManager.AddGroup(this);
            }

            if (LightUtility.CheckForChange(m_CastsShadows, ref m_PreviousCastsShadows))
            {
                if (m_CastsShadows || m_SelfShadows)
                    ShadowCasterGroup2DManager.AddGroup(this);
                else
                    ShadowCasterGroup2DManager.RemoveGroup(this);
            }

            //if (m_ShadowCastingSource == ShadowCastingSources.ShapeProvider && m_ShadowShapeProvider != null)
            //{
            //    IShadowShape2DProvider shadowShapeProvider = (IShadowShape2DProvider)m_ShadowShapeProvider;
            //    shadowShapeProvider?.OnShapeObjectCreated(m_ShadowShape);

            //    m_ShadowShape.GetEdges(m_ShadowShapeContract, out m_ShadowShapeVertices, out m_ShadowShapeEdges);
            //    DrawDebugShadowShapes();
            //}
            //else if (m_ShadowCastingSource == ShadowCastingSources.ShapeEditor)
            //{
            //    m_ShadowShape.CreateShape(m_ShapePath, null, IShadowShape2DProvider.OutlineTopology.LineStrip);
            //    //m_ShadowShape.CreateShape(0, out m_ShadowShapeVertices, out m_ShadowShapeEdges);
                
            //    DrawDebugShadowShapes();
            //}
        }

        // Delete this code later...
        void DrawDebugShadowShapes()
        {
            for(int i=0;i<m_ShadowShapeEdges.Length;i++)
            {
                ShadowShape2D.Edge edge = m_ShadowShapeEdges[i];
                Vector2 pt0 = transform.TransformPoint(m_ShadowShapeVertices[edge.v0]);
                Vector2 pt1 = transform.TransformPoint(m_ShadowShapeVertices[edge.v1]);

                Debug.DrawLine(pt0, pt1, Color.red);
            }
        }

#if UNITY_EDITOR
        void Reset()
        {
            Awake();
            OnEnable();
        }

#endif
    }
}
