using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap.Unity;
using Leap.Unity.Interaction;
using UnityEngine.VFX;
using UnityEngine.Rendering.PostProcessing;

public class ParticleForceFieldLiz : MonoBehaviour
{
    public VisualEffect visualEffect;
    private VFXEventAttribute eventAttribute;
    private Vector3 m_palm;
    private int palmID;

    public ParticleSystem m_particleSystem;

    public ParticleSystemForceField m_forceField;

    public HandModelBase m_LefthandModeBase;
    public HandModelBase m_RighthandModeBase;

    public InteractionHand m_LeftHand;
    public InteractionHand m_RightHand;

    public GameObject m_Video;

    private bool m_IsHandActive;


    public PostProcessLayer postProcessLayer;

    public bool PlayedOnce = false;

    public float timerWait;
    public float timerVideo;


    public void SetPostProcessingLayerIsEnabled(bool _value)
    {
        if (postProcessLayer == null) return;
        postProcessLayer.enabled = _value;
    }

    private void Start()
    {
        SetPostProcessingLayerIsEnabled(false);
        eventAttribute = visualEffect.CreateVFXEventAttribute();
        palmID = Shader.PropertyToID("palm");

        m_forceField.gameObject.SetActive(false);
        m_Video.gameObject.SetActive(false);

        m_LefthandModeBase.OnBegin -= StartForceField;
        m_LefthandModeBase.OnBegin += StartForceField;

        m_LefthandModeBase.OnFinish -= EndForceField;
        m_LefthandModeBase.OnFinish += EndForceField;

        m_RighthandModeBase.OnBegin -= StartForceField;
        m_RighthandModeBase.OnBegin += StartForceField;

        m_RighthandModeBase.OnFinish -= EndForceField;
        m_RighthandModeBase.OnFinish += EndForceField;

        timerVideo = -1;
        timerWait = -1;
    }

    public void StartForceField()
    {
        m_forceField.gameObject.SetActive(true);
        m_IsHandActive = true;
        if(!m_Video.activeSelf)
        {
            timerWait = 4;
        }

    }
    public void EndForceField()
    {
        m_forceField.gameObject.SetActive(false);
        m_IsHandActive = false;
        timerWait = -1;
    }

    private void Update()
    {
        if (m_forceField.gameObject.activeSelf)
        {
            if (m_RightHand._hand != null)
            {
                Vector3 palmPos = m_RightHand._hand.PalmPosition.ToVector3();
                m_forceField.transform.position = palmPos;
                visualEffect.SetVector3(palmID, palmPos);
            }
            else if (m_LeftHand._hand != null)
            {
                Vector3 palmPos = m_LeftHand._hand.PalmPosition.ToVector3();
                m_forceField.transform.position = palmPos;
                visualEffect.SetVector3(palmID, palmPos);
            }
        }

        if (timerWait > 0)
        {
            timerWait -= Time.deltaTime;
        }
        else if (timerWait != -1)
        {
            WaitEnd();
            timerWait = -1;
            timerVideo = 6;
        }

        if(timerVideo>0)
        {
            timerVideo -= Time.deltaTime;
        }
        else if (timerVideo != -1)
        {
            VideoEnd();
            timerVideo = -1;
            if(m_IsHandActive)
            {
                timerWait = 4;
            }
        }

    }

    private void WaitEnd()
    {
            m_Video.SetActive(true);
            m_particleSystem.Stop();
    }

    private void VideoEnd()
    {
        m_Video.SetActive(false);
        m_particleSystem.Play();
        SetPostProcessingLayerIsEnabled(false);


    }
}
