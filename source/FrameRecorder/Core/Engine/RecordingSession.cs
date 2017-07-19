using System;

namespace UnityEngine.FrameRecorder
{

    /// <summary>
    /// What is this: 
    /// Motivation  : 
    /// Notes: 
    /// </summary>    
    public class RecordingSession : IDisposable
    {
        public Recorder m_Recorder;
        public GameObject m_RecorderGO;
        
        public double m_CurrentFrameStartTS;
        public double m_RecordingStartTS;
        int     m_FrameIndex = 0;
        int     m_InitialFrame = 0;
        int     m_FirstRecordedFrameCount = -1;
        float   m_FPSTimeStart;
        float   m_FPSNextTimeStart;
        int     m_FPSNextFrameCount;

        public RecorderSettings settings { get { return m_Recorder.settings; } }
        public bool recording { get { return m_Recorder.recording; } }
        public int frameIndex {get { return m_FrameIndex; }}

        public int RecordedFrameSpan
        {
            get { return m_FirstRecordedFrameCount == -1 ? 0 : Time.renderedFrameCount - m_FirstRecordedFrameCount; }
        }

        public float RecorderTime
        {
            get { return (float)(m_CurrentFrameStartTS - settings.m_StartTime); }
        }

        public bool BeginRecording()
        {
            m_RecordingStartTS = (Time.time / Time.timeScale);

            if (!m_Recorder.BeginRecording(this))
                return false;
            m_InitialFrame = Time.renderedFrameCount;
            m_FPSTimeStart = Time.unscaledTime;

            m_Recorder.SignalSourcesOfStage(ERecordingSessionStage.BeginRecording, this);
            return true;
        }

        public virtual void EndRecording()
        {
            m_Recorder.SignalSourcesOfStage(ERecordingSessionStage.EndRecording, this);
            m_Recorder.EndRecording(this);
        }

        public void RecordFrame()
        {
            m_Recorder.SignalSourcesOfStage(ERecordingSessionStage.NewFrameReady, this);
            if (!m_Recorder.SkipFrame(this))
            {
                m_Recorder.RecordFrame(this);
                m_Recorder.recordedFramesCount++;
                if (m_Recorder.recordedFramesCount == 1)
                    m_FirstRecordedFrameCount = Time.renderedFrameCount;
            }
            m_Recorder.SignalSourcesOfStage(ERecordingSessionStage.FrameDone, this);

            // Note: This is not great when multiple recorders are simultaneously active...
            if (m_Recorder.settings.m_FrameRateMode == FrameRateMode.Variable ||
                (m_Recorder.settings.m_FrameRateMode == FrameRateMode.Constant && m_Recorder.settings.m_SynchFrameRate)
            )
            {
                var frameCount = Time.renderedFrameCount - m_InitialFrame;
                var frameLen = 1.0f / m_Recorder.settings.m_FrameRate;
                var elapsed = Time.unscaledTime - m_FPSTimeStart;
                var target = frameLen * (frameCount+1);
                var sleep =  (int)((target - elapsed) * 1000);

                if (sleep > 2)
                {
                    if(settings.m_Verbose)
                        Debug.Log( string.Format("Recording session info => dT: {0:F1}s, Target dT: {1:F1}s, Retarding: {2}ms, fps: {3:F1}", elapsed, target, sleep, frameCount / elapsed ));
                    System.Threading.Thread.Sleep(sleep);
                }
                else if (sleep < -frameLen)
                    m_InitialFrame--;
                else
                    Debug.Log( string.Format("Recording session info => fps: {0:F1}", frameCount / elapsed ));

                // reset every 30 frames
                if (frameCount % 50 == 49)
                {
                    m_FPSNextTimeStart = Time.unscaledTime;
                    m_FPSNextFrameCount = Time.renderedFrameCount;
                }
                if (frameCount % 100 == 99)
                {
                    m_FPSTimeStart = m_FPSNextTimeStart;
                    m_InitialFrame = m_FPSNextFrameCount;
                }
            }

            m_FrameIndex++;
        }

        public void PrepareNewFrame()
        {
            m_CurrentFrameStartTS = (Time.time / Time.timeScale) - m_RecordingStartTS;
            m_Recorder.SignalSourcesOfStage(ERecordingSessionStage.NewFrameStarting, this);
            m_Recorder.PrepareNewFrame(this);
        }

        public void Dispose()
        {
            if (m_Recorder != null)
            {
                if (recording)
                    EndRecording();

                UnityHelpers.Destroy(m_Recorder);
                UnityHelpers.Destroy(m_RecorderGO);
            }
        }
    }
}
