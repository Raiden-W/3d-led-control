using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyMatrixPlayback
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var a=m.animationRoot.GetComponent<Animator>();
        var oldPlaying=m.playing;var oldFill=m.fillInterior;
        try
        {
            m.fillInterior=false;MatrixWindow.Open();var panel=Resources.FindObjectsOfTypeAll<MatrixWindow>().First();panel.CreateGUI();
            var toggle=panel.rootVisualElement.Query<Toggle>().ToList().First(t=>t.label.StartsWith("Playing"));
            toggle.value=true;await Task.Delay(350);float t0=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            await Task.Delay(350);float t1=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if(Mathf.Approximately(t0,t1))throw new Exception("Playing did not advance Animator");
            toggle.value=false;await Task.Delay(350);float paused=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            await Task.Delay(350);float pausedAgain=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if(Mathf.Abs(paused-pausedAgain)>0.0001f)throw new Exception("Pause did not stop Animator");
            toggle.value=true;await Task.Delay(350);float resumed=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if(Mathf.Approximately(resumed,pausedAgain))throw new Exception("Resume did not advance Animator");
            return $"PASS: animation advances {t0:F4}→{t1:F4}, stays {paused:F4} while paused, resumes to {resumed:F4}.";
        }
        finally {m.playing=oldPlaying;m.fillInterior=oldFill;}
    }
}
