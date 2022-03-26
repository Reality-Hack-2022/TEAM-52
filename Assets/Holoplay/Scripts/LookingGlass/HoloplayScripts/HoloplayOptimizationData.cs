using System;
using UnityEngine;

namespace LookingGlass {
    [Serializable]
    public class HoloplayOptimizationData {
        [SerializeField] private Holoplay.ViewInterpolationType viewInterpolation = Holoplay.ViewInterpolationType.None;
        [SerializeField] private bool reduceFlicker;
        [SerializeField] private bool fillGaps;
        [SerializeField] private bool blendViews;

        public Holoplay.ViewInterpolationType ViewInterpolation {
            get { return viewInterpolation; }
            set { viewInterpolation = value; }
        }

        //TODO: Better document what this means.. the API isn't that self-descriptive.
        public int GetViewInterpolation(int numViews) {
            switch (viewInterpolation) {
                case Holoplay.ViewInterpolationType.None:
                default:
                    return 1;
                case Holoplay.ViewInterpolationType.EveryOther:
                    return 2;
                case Holoplay.ViewInterpolationType.Every4th:
                    return 4;
                case Holoplay.ViewInterpolationType.Every8th:
                    return 8;
                case Holoplay.ViewInterpolationType._4Views:
                    return numViews / 3;
                case Holoplay.ViewInterpolationType._2Views:
                    return numViews;
            }
        }

        public bool ReduceFlicker {
            get { return reduceFlicker; }
            set { reduceFlicker = value; }
        }

        public bool FillGaps {
            get { return fillGaps; }
            set { fillGaps = value; }
        }

        public bool BlendViews {
            get { return blendViews; }
            set { blendViews = value; }
        }
    }
}
