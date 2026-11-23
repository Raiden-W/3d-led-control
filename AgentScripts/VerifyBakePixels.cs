using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LedMatrix;

public static class VerifyBakePixels
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        if(EditorApplication.isPlaying || AnimationMode.InAnimationMode())throw new Exception("Requires Edit Mode without another active animation preview");
        var atlas=Path.GetFullPath("Verification/QilinBake/20261120_110505_648");
        var rows=new System.Collections.Generic.List<string>();
        var bounds=new System.Collections.Generic.List<string>();
        var oldTime=m.time;
        try
        {
            AnimationMode.StartAnimationMode();
            foreach(int frame in new[]{0,9,10,225,400})
            {
                float time=frame/m.clip.frameRate;
                AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(m.animationRoot.gameObject,m.clip,time);AnimationMode.EndSampling();
                m.RenderCurrentPose();var request=AsyncGPUReadback.Request(m.AtlasTexture,0,TextureFormat.RGBA32);request.WaitForCompletion();
                var live=request.GetData<byte>();
                var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                try
                {
                    image.LoadImage(File.ReadAllBytes(Path.Combine(atlas,$"frame_{frame:D6}.png")));
                    var saved=image.GetPixels32();int diff=0,lit=0;
                    for(int i=0;i<saved.Length;i++)
                    {
                        var p=saved[i];if(p.r+p.g+p.b>0)lit++;
                        if(live[4*i]!=p.r||live[4*i+1]!=p.g||live[4*i+2]!=p.b||live[4*i+3]!=p.a)diff++;
                    }
                    if(diff!=0)throw new Exception($"frame {frame} differs from same-time preview at {diff} pixels");
                    rows.Add($"{frame}: {lit} lit pixels, exact RGBA match");
                }
                finally{UnityEngine.Object.DestroyImmediate(image);}
            }
            return "PASS: "+string.Join("; ",rows);
        }
        finally{AnimationMode.StopAnimationMode();m.time=oldTime;m.RenderCurrentPose();}
    }
}
