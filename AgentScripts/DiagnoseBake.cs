using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LedMatrix;
using LedMatrix.Editor;

public static class DiagnoseBake
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var source=m.contentRoot.GetComponentInChildren<FbxVolumeSource>();
        var w=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault(x=>x.previewing&&x.animationClip==m.clip);
        var before=w!=null?w.time:0;var animator=m.animationRoot.GetComponent<Animator>();
        var rows=new System.Collections.Generic.List<string>();
        try
        {
            foreach(var t in new[]{0f,7.5f,14.9f})
            {
                m.RenderAt(t);var r=AsyncGPUReadback.Request(m.AtlasTexture,0,TextureFormat.RGBA32);r.WaitForCompletion();
                var bytes=r.GetData<byte>();int lit=0;for(int i=0;i<bytes.Length;i+=4)if(bytes[i]+bytes[i+1]+bytes[i+2]>0)lit++;
                rows.Add($"t={t} enabled={animator.enabled} lit={lit} bounds={source.WorldGeometryBounds()}");
            }
            return string.Join("; ",rows);
        }
        finally {if(w!=null){w.time=before;w.Repaint();}m.RenderCurrentPose();}
    }
}
