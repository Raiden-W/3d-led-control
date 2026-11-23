using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyNativeWorkflow
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        if(EditorApplication.isPlaying)throw new Exception("Run in Edit Mode");
        var original=m.contentRoot.localPosition;
        var rotation=m.contentRoot.localRotation;
        var scale=m.contentRoot.localScale;
        byte[] a,b,moved;
        try
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(m.animationRoot.gameObject,m.clip,0);AnimationMode.EndSampling();
            m.RenderCurrentPose();a=MatrixVerification.Read(m);
            AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(m.animationRoot.gameObject,m.clip,7.5f);AnimationMode.EndSampling();
            m.RenderCurrentPose();b=MatrixVerification.Read(m);
            if(a.SequenceEqual(b))throw new Exception("Timeline samples do not change atlas");
            m.contentRoot.localPosition=original+Vector3.right*0.05f;
            m.RenderCurrentPose();moved=MatrixVerification.Read(m);
            if(b.SequenceEqual(moved))throw new Exception("Placement did not change atlas");
            return "PASS: AnimationMode 0s/7.5s changes atlas; placement changes atlas; AnimatorController present="+(m.animationRoot.GetComponent<Animator>().runtimeAnimatorController!=null);
        }
        finally
        {
            AnimationMode.StopAnimationMode();m.contentRoot.localPosition=original;m.contentRoot.localRotation=rotation;m.contentRoot.localScale=scale;
            m.RenderCurrentPose();
        }
    }
}
