//Copyright 2017-2021 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

// Based on MIT licensed FFmpegOut - FFmpeg video encoding plugin for Unity
// https://github.com/keijiro/FFmpegOut

using System;
using System.IO;
using System.Collections;
using FFmpegOut;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;
namespace LookingGlass {
    /// <summary>
    /// Provides a way to record quilt videos from within Unity scenes.
    /// </summary>
    [HelpURL("https://look.glass/unitydocs")]
    public sealed class HoloplayRecorder : MonoBehaviour, ISerializationCallbackReceiver {
        [SerializeField, HideInInspector] private SerializableVersion lastSavedVersion;

        [SerializeField] private string outputName = "output";

        [FormerlySerializedAs("_preset")]
        [SerializeField] private FFmpegPreset preset = FFmpegPreset.VP8Default;

        [FormerlySerializedAs("_frameRate")]
        [Min(1)]
        [SerializeField] private float frameRate = 30;

        [Tooltip("Megabits per second")]
        [FormerlySerializedAs("_targetBitrateInMegabits")]
        [SerializeField] private int targetBitrateInMegabits = 60;

        [Tooltip("CRF for VP8/VP9, CQ for NVENC")]
        [FormerlySerializedAs("_compression")]
        [SerializeField] private int compression = 18;

        [Tooltip("A collection of settings that may be applied from the moment recording starts, lasting until recording ends.")]
        [SerializeField] private HoloplayRecorderOverrideSettings overrideSettings = new HoloplayRecorderOverrideSettings();

        [HideInInspector, Obsolete("Use HoloplayRecorder.OverrideSettings instead.")] public bool isOverwrite;
        [HideInInspector, Obsolete("Use HoloplayRecorder.OverrideSettings instead.")] public Quilt.Settings overwriteQuiltSettings = Quilt.GetSettings(HoloplayDevice.Type.Portrait);
        [HideInInspector, Obsolete("Use HoloplayRecorder.OverrideSettings instead.")] public float overwriteNearClipFactor = 0.5f;

        private FFmpegSession session;
        private HoloplayRecorderState state;

        private int previousCaptureFramerate;

        private Quilt.Preset previousPreset;
        private Quilt.Settings previousCustom;
        private bool previousPreviewSettings;
        private float previousAspect;
        private float previousNearClip;
        private bool overridesAreInEffect = false;

        public string OutputPath {
            get { return outputName; }
            set { outputName = value; }
        }

        public FFmpegPreset Preset {
            get { return preset; }
            set { preset = value; }
        }

        public float FrameRate {
            get { return frameRate; }
            set { frameRate = value; }
        }

        public int TargetBitrateInMegabits {
            get { return targetBitrateInMegabits; }
            set { targetBitrateInMegabits = value; }
        }

        public int Compression {
            get { return compression; }
            set { compression = value; }
        }

        public HoloplayRecorderOverrideSettings OverrideSettings => overrideSettings;

        private Holoplay Holoplay => Holoplay.Instance;

        //NOTE: Corresponds to Quilt.Settings.aspect!
        public float Aspect => (overrideSettings.Enabled) ? overrideSettings.QuiltSettings.CalculatedSingleViewAspect : Holoplay.QuiltSettings.CalculatedSingleViewAspect;

        /// <summary>
        /// <para>The quilt settings that will be used when recording.</para>
        /// <para>See also: <seealso cref="HoloplayRecorderOverrideSettings"/></para>
        /// </summary>
        public Quilt.Settings QuiltSettings => (overrideSettings.Enabled) ? overrideSettings.QuiltSettings : Holoplay.QuiltSettings;
        public float NearClipFactor => (overrideSettings.Enabled) ? overrideSettings.NearClipFactor : Holoplay.CameraData.NearClipFactor;

        [Tooltip("If you want to record a video clip, place it here. To remove the clip, click on the box and press backspace.")]
        [SerializeField] VideoPlayer videoClip;
        [Tooltip("When checked, the recording will begin when play is pressed.")]
        [SerializeField] bool recordOnStart;
        [Tooltip("When checked, recording automatically will end when the referenced clip has ended.")]
        [SerializeField] bool matchRecordingDuration;
        [Tooltip("When checked, play mode will exit when the recording has ended.")]
        [SerializeField] bool exitPlayModeOnEnd;

        public HoloplayRecorderState State => state;

        public string AutoCorrectPath {
            get {
                outputName = string.IsNullOrEmpty(outputName) ? "output" : outputName;
                Quilt.Settings quiltSettings = QuiltSettings;

                string outputPath = outputName;
                string ending = "_qs" + quiltSettings.viewColumns + "x" + quiltSettings.viewRows + "a" + quiltSettings.CalculatedSingleViewAspect;
                bool needEnding = !outputPath.Contains(ending);

                if (!needEnding)
                    outputPath = outputPath.Replace(ending, "");

                if (!outputPath.EndsWith(Preset.GetSuffix()))
                    outputPath = Path.ChangeExtension(outputPath, Preset.GetSuffix());

                return outputPath.Insert(outputPath.LastIndexOf('.'), ending);
            }
        }

        VideoPlayer[] videoPlayers;
        float[] originSpeeds;

        //TODO: Pull this out into a separate class.
        #region Time Keeping
        private int frameCount;
        private float startTime;
        private float pauseTime = 0;
        private int frameDropCount;

        private float FrameTime {
            get { return startTime + pauseTime + (frameCount - 0.5f) / frameRate; }
        }

        private void WarnFrameDrop() {
            if (++frameDropCount != 10)
                return;

            Debug.LogWarning(
                "Significant frame droppping was detected. This may introduce " +
                "time instability into output video. Decreasing the recording " +
                "frame rate is recommended."
            );
        }
        #endregion

        #region Unity Messages
        private IEnumerator Start() {
            videoPlayers =FindObjectsOfType<VideoPlayer>();
            originSpeeds = new float[videoPlayers.Length];
            for(int i = 0; i < videoPlayers.Length; i++)
            {
                originSpeeds[i] = videoPlayers[i].playbackSpeed;
            }

            if (videoClip != null)
            {
                frameRate = (float)Math.Round(videoClip.frameRate * 100f) / 100f;
                
            }
            if (recordOnStart)
            {
                StartRecord();
            }
            //TODO: Pull this code out of Start, and use a dedicated coroutine method, using while (isActiveAndEnabled) { ... }.
            // Sync with FFmpeg pipe thread at the end of every frame.
            for (var eof = new WaitForEndOfFrame(); ;) {
                yield return eof;
                if (session != null)
                    session.CompletePushFrames();
            }

        }

        private void Update() {
            if (state == HoloplayRecorderState.NotRecording) {
                return;
            }

            Holoplay holoplay = Holoplay;
            RenderTexture quilt = holoplay.QuiltTexture;
            bool frameIncre = true;

            if (!holoplay) {
                Debug.LogWarning("[HoloPlay] Failed to record because no HoloPlay Capture instance exists.");
                return;
            }

            float gap = Time.time - FrameTime;
            float delta = 1 / frameRate;

            if (gap < 0 || state == HoloplayRecorderState.Paused) {
                // Update without frame data.
                session.PushFrame(null);
                frameIncre = false;
            } else if (gap < delta) {
                // Single-frame behind from the current time:
                // Push the current frame to FFmpeg.
                session.PushFrame(quilt);
                frameCount++;
            } else if (gap < delta * 2) {
                // Two-frame behind from the current time:
                // Push the current frame twice to FFmpeg. Actually this is not
                // an efficient way to catch up. We should think about
                // implementing frame duplication in a more proper way. #fixme
                session.PushFrame(quilt);
                session.PushFrame(quilt);
                frameCount += 2;
            } else {
                // Show a warning message about the situation.
                WarnFrameDrop();

                // Push the current frame to FFmpeg.
                session.PushFrame(quilt);

                // Compensate the time delay.
                frameCount += Mathf.FloorToInt(gap * frameRate);
            }

            if(frameIncre && videoPlayers.Length > 0)
            {
                foreach(var videoPlayer in videoPlayers) {
                    if(videoPlayer == null)
                        continue;
                    videoPlayer.StepForward();
                    videoPlayer.Play();
                }
            }
        }

        private void OnDisable() {
            EndRecord();
        }
        #endregion

        public void OnBeforeSerialize() {
            Version previous = lastSavedVersion.Value;
            if (previous < Holoplay.Version) {
                //NOTE: ONLY perform this force update if going from pre-v1.5.0 to post-v1.5.0!
                //If someone updates to (for example) v1.4.3 to v1.5.0,
                //      - Yes, update the lastSavedVersion field,
                //      - But NO, do NOT re-force the serialization layout changes! That would overwrite the user's values properly-set values!
                if (Holoplay.IsUpdatingBetween(previous, Holoplay.Version, new Version(1, 5, 0))) {
                    Debug.Log(this + " transferring to new serialization layout for " + Holoplay.Version + "!");
#pragma warning disable 0618
                    overrideSettings.Enabled = isOverwrite;
                    overrideSettings.QuiltSettings = overwriteQuiltSettings;
                    overrideSettings.NearClipFactor = overwriteNearClipFactor;
#pragma warning restore 0618
                }

                lastSavedVersion.CopyFrom(Holoplay.Version);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        public void OnAfterDeserialize() { }

        public void SetupHoloplayQuiltSize() {
            Holoplay holoplay = Holoplay;
            if (!holoplay) {
                Debug.LogWarning("[Holoplay] Failed to set up quilt settings because no Holoplay Capture instance exists");
                return;
            }

            if (!overridesAreInEffect) {
                previousPreset = holoplay.QuiltPreset;
                previousCustom = holoplay.CustomQuiltSettings;
                previousPreviewSettings = holoplay.Preview2D;
                previousAspect = holoplay.cal.aspect;
                previousNearClip = holoplay.CameraData.NearClipFactor;
            }
            overridesAreInEffect = true;

            holoplay.Preview2D = false;
            holoplay.SetQuiltPresetAndSettings(Quilt.Preset.Custom, overrideSettings.QuiltSettings);
            holoplay.CameraData.NearClipFactor = overrideSettings.NearClipFactor;
            holoplay.cal.aspect = QuiltSettings.aspect;
        }

        public void RestoreHoloplayQuiltSize() {
            Holoplay holoplay = Holoplay;
            if (!holoplay) {
                Debug.LogWarning("[Holoplay] Failed to restore quilt settings because no Holoplay Capture instance exists");
                return;
            }
            holoplay.Preview2D = previousPreviewSettings;
            holoplay.SetQuiltPresetAndSettings(previousPreset, previousCustom);
            holoplay.CameraData.NearClipFactor = previousNearClip;
            holoplay.cal.aspect = previousAspect;

            overridesAreInEffect = false;
        }

        /// <summary>
        /// Starts a recording session that will output a video file to a file at the default <see cref="AutoCorrectPath"/>.
        /// </summary>
        public void StartRecord() => StartRecordWithPath(AutoCorrectPath);

        /// <summary>
        /// Starts a recording session that will output a video file to a file at the given <paramref name="outputFilePath"/>.
        /// </summary>
        public void StartRecordWithPath(string outputFilePath) {
            // manually update frame of video play during recording
            if (matchRecordingDuration)
            {
                if(videoClip != null)
                {
                    StartCoroutine(MatchRecDur());
                }
                else
                {
                    Debug.LogWarning("No video clip referenced. Cannot match recording duration.");
                }
            }
            for(int i = 0; i < videoPlayers.Length; i++)
            {
                videoPlayers[i].playbackSpeed = 0;
            }

            Holoplay holoplay = Holoplay;
            if (!holoplay) {
                Debug.LogWarning("[Holoplay] Fail to start recorder because no HoloPlay Capture instance exists.");
                return;
            }

            if (session != null)
                session.Dispose();

            if (overrideSettings.Enabled)
                SetupHoloplayQuiltSize();

            string fullpath = Path.GetFullPath(outputFilePath);

            // Start an FFmpeg session.
            RenderTexture quilt = holoplay.QuiltTexture;
            Debug.Log("Creating FFmpeg session with size "
                + quilt.width + "x" + quilt.height + ", will be saved at " + fullpath);

            string extraFfmpegOptions = "-b:v " + targetBitrateInMegabits + "M";

#if !UNITY_EDITOR_OSX && !UNITY_STANDALONE_OSX
            if (Preset == FFmpegPreset.H264Nvidia || Preset == FFmpegPreset.HevcNvidia) {
                extraFfmpegOptions += " -cq:v " + compression;
            } else {
                extraFfmpegOptions += " -crf " + compression;
            }
#endif

            session = FFmpegSession.CreateWithOutputPath(
                outputFilePath,
                quilt.width,
                quilt.height,
                frameRate, Preset, extraFfmpegOptions
            );

            startTime = Time.time;
            frameCount = 0;
            frameDropCount = 0;

            if (GetComponent<FrameRateController>() == null) {
                previousCaptureFramerate = Time.captureFramerate;
                Time.captureFramerate = Mathf.RoundToInt(FrameRate);
            }

            state = HoloplayRecorderState.Recording;
        }

        public void PauseRecord() {
            if (state == HoloplayRecorderState.Recording)
                state = HoloplayRecorderState.Paused;
            else
                Debug.LogWarning("[Holoplay] Can't pause recording when it's not started.");
        }

        public void ResumeRecord() {
            if (state == HoloplayRecorderState.Paused)
                state = HoloplayRecorderState.Recording;
            else
                Debug.LogWarning("[Holoplay] Can't resume recording when it's not paused.");
        }

        public void EndRecord() {
            // set the playback speed back to original
            for(int i = 0; i < videoPlayers.Length; i++)
            {
                if(videoPlayers[i] == null)
                    continue;
                videoPlayers[i].playbackSpeed = originSpeeds[i];
            }
            state = HoloplayRecorderState.NotRecording;
            pauseTime = 0;

            if (session != null) {
                Debug.Log("Closing FFmpegSession after " + frameCount + " frames.");

                session.Close();
                session.Dispose();
                session = null;
            }
#if UNITY_EDITOR
            if (exitPlayModeOnEnd)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#endif
            if (GetComponent<FrameRateController>() == null) {
                Time.captureFramerate = previousCaptureFramerate;
            }

            if (overrideSettings.Enabled)
                RestoreHoloplayQuiltSize();
        }
        // Recording the DepthKit clip
        IEnumerator MatchRecDur()
        {
            float clipLength = (float)videoClip.length;
            clipLength = (float)Math.Round(clipLength * 100f) / 100f;
            yield return new WaitForSeconds(clipLength);
            EndRecord();
            //Debug.Log("Recording Ended. Duration: " + clipLength);
        }
    }
}
