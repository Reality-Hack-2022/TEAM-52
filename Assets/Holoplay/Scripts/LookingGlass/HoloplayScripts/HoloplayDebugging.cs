using System;
using UnityEngine;

namespace LookingGlass {
    /// <summary>
    /// Contains several options, useful in the inspector, for debugging a <see cref="Holoplay"/> component.
    /// </summary>
    [Serializable]
    public class HoloplayDebugging : ISerializationCallbackReceiver {
        [Tooltip("When set to true, this reveals hidden objects used by this " +
            nameof(Holoplay) + " component, such as the cameras used for rendering.")]
        [SerializeField] private bool showAllObjects = false;

        [NonSerialized] private bool wasShowingObjects = false;

        internal event Action onShowAllObjectsChanged;

        public bool ShowAllObjects {
            get { return showAllObjects; }
            set {
                wasShowingObjects = showAllObjects = value;
                onShowAllObjectsChanged?.Invoke();
            }
        }

        public void OnBeforeSerialize() {
            if (showAllObjects != wasShowingObjects)
                ShowAllObjects = showAllObjects;
        }
        public void OnAfterDeserialize() { }
    }
}