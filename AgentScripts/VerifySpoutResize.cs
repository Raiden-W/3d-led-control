using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Klak.Spout;
using LedMatrix;

public static class VerifySpoutResize
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var count=m.pointCounts;var fill=m.fillInterior;var pattern=m.pattern;
        try
        {
            m.fillInterior=false;m.pattern=MatrixVolumeRenderer.TestPattern.Red;m.pointCounts=new Vector3Int(8,12,5);
            await Task.Delay(1200);
            var sender=m.GetComponent<SpoutSender>();
            var buffer=(RenderTexture)typeof(SpoutSender).GetField("_buffer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sender);
            if(m.AtlasTexture.width!=96 || m.AtlasTexture.height!=96 || buffer==null||buffer.width!=96||buffer.height!=96)
                throw new Exception("Spout sender did not resize: atlas="+m.AtlasTexture.width+"x"+m.AtlasTexture.height+" buffer="+(buffer?buffer.width+"x"+buffer.height:"null"));
            if(Array.IndexOf(SpoutManager.GetSourceNames(),"3dLedMatrix_Atlas")<0)throw new Exception("Spout source missing after resize");
            return "PASS: resized atlas and Spout sender buffer to 96×96, source remains discoverable";
        }
        finally{m.pointCounts=count;m.fillInterior=fill;m.pattern=pattern;}
    }
}
