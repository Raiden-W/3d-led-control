using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;

public static class CaptureQilin
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        float original=m.time; bool playing=m.playing;m.playing=false;
        var report=new System.Text.StringBuilder();
        try
        {
            foreach(float time in new[]{0f,3.75f,7.5f,11.25f,14.9f})
            {
                m.RenderAt(time);var bytes=MatrixVerification.Read(m);int lit=0;
                for(int i=0;i<bytes.Length;i+=4)if(bytes[i]+bytes[i+1]+bytes[i+2]>0)lit++;
                File.WriteAllBytes($"Verification/Qilin/atlas_{time:0.00}.png",ImageConversion.EncodeArrayToPNG(bytes,UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,(uint)m.AtlasTexture.width,(uint)m.AtlasTexture.height));
                report.AppendLine($"time={time:0.00}s; lit pixels={lit}; CPU submit={m.LastRenderMs:0.000}ms");
            }
            return report.ToString();
        }
        finally {m.RenderAt(original);m.playing=playing;}
    }
}
