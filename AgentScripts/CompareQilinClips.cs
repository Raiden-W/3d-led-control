using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;
public static class CompareQilinClips
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var src=AssetDatabase.LoadAllAssetsAtPath("Assets/Models/02qilin/qilin_15s_.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var dst=m.clip;var lines=new System.Collections.Generic.List<string>();
        try
        {
            AnimationMode.StartAnimationMode();
            foreach(var clip in new[]{src,dst}) foreach(var t in new[]{0f,7.5f})
            {
                AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(m.animationRoot.gameObject,clip,t);AnimationMode.EndSampling();
                m.RenderCurrentPose();var a=MatrixVerification.Read(m);int lit=0;for(int i=0;i<a.Length;i+=4)if(a[i]+a[i+1]+a[i+2]>0)lit++;
                var horse=m.animationRoot.Find("Horse");
                lines.Add($"{clip.name} {t}: lit={lit}; Horse={(horse?horse.localRotation.ToString():"missing")}; skin={m.contentRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Length}");
            }
        }
        finally{AnimationMode.StopAnimationMode();}
        return string.Join("\n",lines);
    }
}
