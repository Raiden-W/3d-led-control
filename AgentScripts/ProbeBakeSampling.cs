using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LedMatrix;

public static class ProbeBakeSampling
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var source=m.contentRoot.GetComponentInChildren<FbxVolumeSource>();
        var w=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault(x=>x.previewing&&x.animationClip==m.clip);
        float before=w!=null?w.time:0;bool own=!AnimationMode.InAnimationMode();
        var rows=new System.Collections.Generic.List<string>();
        try
        {
            if(own)AnimationMode.StartAnimationMode();
            foreach(var t in new[]{0f,7.5f,14.9f})
            {
                AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(m.animationRoot.gameObject,m.clip,t);AnimationMode.EndSampling();
                m.RenderCurrentPose();var request=AsyncGPUReadback.Request(m.AtlasTexture,0,TextureFormat.RGBA32);request.WaitForCompletion();
                var bytes=request.GetData<byte>();int lit=0;for(int i=0;i<bytes.Length;i+=4)if(bytes[i]+bytes[i+1]+bytes[i+2]>0)lit++;
                rows.Add($"t={t} lit={lit} bounds={source.WorldGeometryBounds()}");
            }
            return string.Join("; ",rows);
        }
        finally
        {
            if(own)AnimationMode.StopAnimationMode();
            else if(w!=null){AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(m.animationRoot.gameObject,m.clip,before);AnimationMode.EndSampling();}
            m.RenderCurrentPose();
        }
    }
}
