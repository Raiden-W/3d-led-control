using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LedMatrix;

public static class FitQilinTimeline
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();m.playing=false;
        var source=m.contentRoot.GetComponentInChildren<FbxVolumeSource>();source.enabled=false;
        m.contentRoot.localPosition=Vector3.zero;m.contentRoot.localScale=Vector3.one;
        Bounds overall=default;bool first=true;int samples=Mathf.RoundToInt(m.clip.length*m.clip.frameRate)+1;
        for(int i=0;i<samples;i++)
        {
            m.RenderAt(i/m.clip.frameRate);var b=source.WorldGeometryBounds();
            if(first){overall=b;first=false;}else overall.Encapsulate(b);
        }
        float scale=Mathf.Min(0.72f/overall.size.x,2.25f/overall.size.y,0.72f/overall.size.z);
        m.contentRoot.localScale=Vector3.one*scale;m.contentRoot.localPosition=new Vector3(0,1.175f,0)-overall.center*scale;
        Bounds fitted=new Bounds(new Vector3(0,1.175f,0),overall.size*scale);
        source.enabled=true;m.RenderAt(7.5f);m.GetComponent<MatrixSpoutOutput>().Sync();m.playing=true;
        EditorSceneManager.MarkSceneDirty(m.gameObject.scene);EditorSceneManager.SaveScene(m.gameObject.scene);
        string report=$"Sampled {samples} animation poses for spatial bounds only; no sequence was baked. Uniform placement scale={scale:0.000000}. Fitted whole-animation geometry: min={fitted.min}, max={fitted.max}. Matrix volume: min={m.VolumeMin}, max={m.VolumeMin+m.VolumeSize}.";
        File.WriteAllText("Verification/Qilin/fit.txt",report);return report;
    }
}
