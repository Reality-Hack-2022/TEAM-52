using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LookingGlass {
    [Serializable]
    public class HoloplayGizmos {
        [SerializeField] private bool drawHandles = true;
        [SerializeField] private Color frustumColor = new Color32(0, 255, 0, 255);
        [SerializeField] private Color middlePlaneColor = new Color32(150, 50, 255, 255);
        [SerializeField] private Color handleColor = new Color32(75, 100, 255, 255);

        private float[] cornerDists = new float[3];
        private Vector3[] frustumCorners = new Vector3[12];

        public bool DrawHandles {
            get { return drawHandles; }
            set { drawHandles = value; }
        }

        public Color FrustumColor {
            get { return frustumColor; }
            set { frustumColor = value; }
        }

        public Color MiddlePlaneColor {
            get { return middlePlaneColor; }
            set { middlePlaneColor = value; }
        }

        public Color HandleColor {
            get { return handleColor; }
            set { handleColor = value; }
        }

        public void DrawGizmos(Holoplay holoplay) {
#if UNITY_EDITOR
            //Thanks to https://forum.unity.com/threads/solved-how-to-force-update-in-edit-mode.561436/
            //Ensure continuous Update calls:
            if (!Application.isPlaying) {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
#endif

            Gizmos.color = QualitySettings.activeColorSpace == ColorSpace.Gamma ? frustumColor.gamma : frustumColor;
            float focalDist = holoplay.GetCameraDistance();
            Camera singleViewCamera = holoplay.SingleViewCamera;

            cornerDists[0] = focalDist;
            cornerDists[1] = singleViewCamera.nearClipPlane;
            cornerDists[2] = singleViewCamera.farClipPlane;

            for (int i = 0; i < cornerDists.Length; i++) {
                float dist = cornerDists[i];
                int offset = i * 4;
                frustumCorners[offset + 0] = singleViewCamera.ViewportToWorldPoint(new Vector3(0, 0, dist));
                frustumCorners[offset + 1] = singleViewCamera.ViewportToWorldPoint(new Vector3(0, 1, dist));
                frustumCorners[offset + 2] = singleViewCamera.ViewportToWorldPoint(new Vector3(1, 1, dist));
                frustumCorners[offset + 3] = singleViewCamera.ViewportToWorldPoint(new Vector3(1, 0, dist));

                // draw each square
                for (int j = 0; j < 4; j++) {
                    Vector3 start = frustumCorners[offset + j];
                    Vector3 end = frustumCorners[offset + (j + 1) % 4];
                    if (i > 0) {
                        // draw a normal line for front and back
                        Gizmos.color = QualitySettings.activeColorSpace == ColorSpace.Gamma ?
                            frustumColor.gamma : frustumColor;
                        Gizmos.DrawLine(start, end);
                    } else {
                        // draw a broken, target style frame for focal plane
                        Gizmos.color = QualitySettings.activeColorSpace == ColorSpace.Gamma ?
                            middlePlaneColor.gamma : middlePlaneColor;
                        Gizmos.DrawLine(start, Vector3.Lerp(start, end, 0.333f));
                        Gizmos.DrawLine(end, Vector3.Lerp(end, start, 0.333f));
                    }
                }
            }

            // connect them
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(frustumCorners[4 + i], frustumCorners[8 + i]);

        }
    }
}
