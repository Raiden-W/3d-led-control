using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using LedMatrix;
using LedMatrix.Editor;
using Klak.Spout;
public static class VerifyQilinSpout
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        try
        {
            var sender=m.GetComponent<SpoutSender>();
            var buffer=(RenderTexture)typeof(SpoutSender).GetField("_buffer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sender);
            var request=AsyncGPUReadback.Request(buffer,0,TextureFormat.RGBA32);request.WaitForCompletion();
            var transport=request.GetData<byte>();var source=MatrixVerification.Read(m);int different=0;
            int width=buffer.width,height=buffer.height;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)for(int c=0;c<4;c++)
                if(source[(y*width+x)*4+c]!=transport[((height-1-y)*width+x)*4+c])different++;
            string report=$"Qilin Spout native transport {width}x{height}: differing RGBA bytes={different} after internal Y-flip compensation.";
            File.WriteAllText("Verification/Qilin/spout.txt",report);
            if(different!=0)throw new Exception(report);return report;
        }
        finally{m.playing=true;}
    }
}
