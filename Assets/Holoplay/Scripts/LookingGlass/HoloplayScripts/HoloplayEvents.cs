using System;
using UnityEngine;
using UnityEngine.Events;

namespace LookingGlass {
    /// <summary>
    /// Contains the events that a <see cref="Holoplay"/> component will fire off.
    /// </summary>
    [Serializable]
    public class HoloplayEvents {
        [Tooltip("If you have any functions that rely on the calibration having been loaded " +
            "and the screen size having been set, let them trigger here")]
        [SerializeField] private HoloplayLoadEvent onHoloplayReady;
        [Tooltip("Will fire before each individual view is rendered. " +
            "Passes [0, numViews), then fires once more passing numViews (in case cleanup is needed)")]
        [SerializeField] private HoloplayViewRenderEvent onViewRender;

        public HoloplayLoadEvent OnHoloplayReady {
            get { return onHoloplayReady; }
            internal set { onHoloplayReady = value; } //NOTE: Setter available for serialization layout updates
        }

        public HoloplayViewRenderEvent OnViewRender {
            get { return onViewRender; }
            internal set { onViewRender = value; } //NOTE: Setter available for serialization layout updates
        }
    }
}
