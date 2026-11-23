using System;
using System.IO;
using UnityEngine;
using LedMatrix;
public static class VerifyQilinFit
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();m.playing=false;
        var source=m.contentRoot.GetComponentInChildren<FbxVolumeSource>();source.enabled=false;
        int count=Mathf.RoundToInt(m.clip.length*m.clip.frameRate)+1,violations=0;Bounds overall=default;
        for(int i=0;i<count;i++)
        {
            m.RenderAt(i/m.clip.frameRate);var b=source.WorldGeometryBounds();
            if(i==0)overall=b;else overall.Encapsulate(b);
            var lo=m.VolumeMin;var hi=lo+m.VolumeSize;
            if(b.min.x<lo.x||b.min.y<lo.y||b.min.z<lo.z||b.max.x>hi.x||b.max.y>hi.y||b.max.z>hi.z)violations++;
        }
        source.enabled=true;source.showSourceMesh=false;m.RenderAt(7.5f);m.playing=true;
        string text=$"Checked {count} animation poses after placement: out-of-matrix poses={violations}; actual union min={overall.min}, max={overall.max}.";
        File.AppendAllText("Verification/Qilin/fit.txt","\n"+text);
        if(violations!=0)throw new Exception(text);return text;
    }
}
