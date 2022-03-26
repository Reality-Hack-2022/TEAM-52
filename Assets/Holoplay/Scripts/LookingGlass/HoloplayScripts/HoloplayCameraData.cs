using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LookingGlass {
    /// <summary>
    /// A set of fields that correspond to fields on a Unity <see cref="Camera"/>, with some extra holoplay fields.
    /// </summary>
    [Serializable]
    public class HoloplayCameraData : ISerializationCallbackReceiver {
        //NOTE: This is NOT orthographicSize.. TODO: Document this...
        [SerializeField] private float size = 5;
        [Range(0.01f, 5)]
        [SerializeField] private float nearClipFactor = HoloplayDevice.GetSettings(HoloplayDevice.Type.Portrait).nearClip;
        [Range(0.01f, 40)]
        [SerializeField] private float farClipFactor = 4;
        [SerializeField] private bool scaleFollowsSize = false;

        [UnityImitatingClearFlags]
        [SerializeField] private CameraClearFlags clearFlags = CameraClearFlags.Color;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private LayerMask cullingMask = -1;
        [Range(5, 90)]
        [SerializeField] private float fieldOfView = 14;
        [SerializeField] private float depth = 0;

        [Tooltip("The rendering path to use for rendering each of the single-views.\n\nYou may choose to use the player settings, or explicitly use deferred or forward rendering.")]
        [SerializeField] private RenderingPath renderingPath = RenderingPath.UsePlayerSettings;

        [SerializeField] private bool useOcclusionCulling = true;
        [SerializeField] private bool allowHDR = true;
        [SerializeField] private bool allowMSAA = true;
#if UNITY_2017_3_OR_NEWER
        [SerializeField] private bool allowDynamicResolution = false;
#endif

        [Tooltip("Determines whether or not the frustum target will be used.")]
        [SerializeField] private bool useFrustumTarget;
        [SerializeField] private Transform frustumTarget;

        [Tooltip("Represents how 3-dimensional the final screen image on the LKG device will appear, as a percentage in the range of [0, 1].\n\n" +
            "The default value is 1.")]
        [Range(0, 1)]
        [SerializeField] private float viewconeModifier = 1;

        [Tooltip("Offsets the cycle of horizontal views based on the observer's viewing angle, represented as a percentage on a scale of [-0.5, 0.5].\n\n" +
            "The default value is 0.")]
        [Range(-0.5f, 0.5f)]
        [SerializeField] private float centerOffset;
        [Range(-90, 90)]
        [SerializeField] private float horizontalFrustumOffset;
        [Range(-90, 90)]
        [SerializeField] private float verticalFrustumOffset;

        public float Size {
            get { return size; }
            set { size = Mathf.Max(0.01f, value); }
        }

        public float NearClipFactor {
            get { return nearClipFactor; }
            set { nearClipFactor = Mathf.Clamp(value, 0.01f, 5); }
        }

        public float FarClipFactor {
            get { return farClipFactor; }
            set { farClipFactor = Mathf.Clamp(value, 0.01f, 40); }
        }

        public bool ScaleFollowsSize {
            get { return scaleFollowsSize; }
            set { scaleFollowsSize = value; }
        }

        public CameraClearFlags ClearFlags {
            get { return clearFlags; }
            set { clearFlags = value; }
        }

        public Color BackgroundColor {
            get { return backgroundColor; }
            set { backgroundColor = value; }
        }

        public LayerMask CullingMask {
            get { return cullingMask; }
            set { cullingMask = value; }
        }

        public float FieldOfView {
            get { return fieldOfView; }
            set { fieldOfView = Mathf.Clamp(value, 5, 90); }
        }

        public float Depth {
            get { return depth; }
            set { depth = Mathf.Clamp(value, -100, 100); }
        }

        public RenderingPath RenderingPath {
            get { return renderingPath; }
            set { renderingPath = value; }
        }

        public bool UseOcclusionCulling {
            get { return useOcclusionCulling; }
            set { useOcclusionCulling = value; }
        }

        public bool AllowHDR {
            get { return allowHDR; }
            set { allowHDR = value; }
        }

        public bool AllowMSAA {
            get { return allowMSAA; }
            set { allowMSAA = value; }
        }

#if UNITY_2017_3_OR_NEWER
        public bool AllowDynamicResolution {
            get { return allowDynamicResolution; }
            set { allowDynamicResolution = value; }
        }
#endif

        public bool UseFrustumTarget {
            get { return useFrustumTarget; }
            set { useFrustumTarget = value; }
        }

        public Transform FrustumTarget {
            get { return frustumTarget; }
            set { frustumTarget = value; }
        }

        public float ViewconeModifier {
            get { return viewconeModifier; }
            set { viewconeModifier = Mathf.Clamp01(value); }
        }

        public float CenterOffset {
            get { return centerOffset; }
            set { centerOffset = Mathf.Clamp01(value); }
        }

        public float HorizontalFrustumOffset {
            get { return horizontalFrustumOffset; }
            set { horizontalFrustumOffset = Mathf.Clamp(value, -90, 90); }
        }

        public float VerticalFrustumOffset {
            get { return verticalFrustumOffset; }
            set { verticalFrustumOffset = Mathf.Clamp(value, -90, 90); }
        }

        public void OnBeforeSerialize() {
            Depth = depth;
        }

        public void OnAfterDeserialize() { }

        public void SetCamera(Camera camera, Transform scaleTarget, float aspect, float distance) {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            if (scaleFollowsSize)
                scaleTarget.localScale = new Vector3(size, size, size);

            camera.orthographic = false;
            if (useFrustumTarget)
                camera.fieldOfView = 2 * Mathf.Atan(Mathf.Abs(size / frustumTarget.localPosition.z)) * Mathf.Rad2Deg;
            else
                camera.fieldOfView = fieldOfView;

            camera.ResetWorldToCameraMatrix();
            camera.ResetProjectionMatrix();
            Matrix4x4 centerViewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 centerProjMatrix = camera.projectionMatrix;
            centerViewMatrix.m23 -= distance;

            if (useFrustumTarget) {
                Vector3 targetPos = -frustumTarget.localPosition;
                centerViewMatrix.m03 += targetPos.x;
                centerProjMatrix.m02 += targetPos.x / (size * aspect);
                centerViewMatrix.m13 += targetPos.y;
                centerProjMatrix.m12 += targetPos.y / size;
            } else {
                if (horizontalFrustumOffset != 0) {
                    float offset = distance * Mathf.Tan(Mathf.Deg2Rad * horizontalFrustumOffset);
                    centerViewMatrix.m03 += offset;
                    centerProjMatrix.m02 += offset / (size * aspect);
                }
                if (verticalFrustumOffset != 0) {
                    float offset = distance * Mathf.Tan(Mathf.Deg2Rad * verticalFrustumOffset);
                    centerViewMatrix.m13 += offset;
                    centerProjMatrix.m12 += offset / size;
                }
            }
            camera.worldToCameraMatrix = centerViewMatrix;
            camera.projectionMatrix = centerProjMatrix;


            camera.nearClipPlane = Mathf.Max(distance - size * nearClipFactor, 0.1f);
            camera.farClipPlane = Mathf.Max(distance + size * farClipFactor, camera.nearClipPlane);

            camera.clearFlags = clearFlags;
            
            //TODO: Does this work properly in HDRP?
            //(I had seen somewhere that we need to change a field on the HDAdditionalCameraData component)
            camera.backgroundColor = backgroundColor;

            camera.depth = depth;

            camera.cullingMask = cullingMask;
            camera.renderingPath = renderingPath;
            camera.useOcclusionCulling = useOcclusionCulling;
            camera.allowHDR = allowHDR;
            camera.allowMSAA = allowMSAA;
#if UNITY_2017_3_OR_NEWER
            camera.allowDynamicResolution = allowDynamicResolution;
#endif
        }
    }
}
