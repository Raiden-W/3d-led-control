using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;
public static class VerifyBakeNoPreview
{
    public static async Task<string> Run()
    {
        if(AnimationMode.InAnimationMode()||EditorApplication.isPlaying||MatrixBaker.IsBaking)throw new Exception("Requires idle Edit Mode with no animation preview");
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var oldSelection=Selection.activeObject;string parent=Path.GetFullPath("Verification/QilinBakeNoPreview");Directory.CreateDirectory(parent);
        var before=Directory.GetDirectories(parent).ToHashSet();
        MatrixBaker.Begin(matrix,parent);
        string dir=Directory.GetDirectories(parent).Single(x=>!before.Contains(x));
        var deadline=DateTime.UtcNow.AddMinutes(4);
        while(MatrixBaker.IsBaking && !File.Exists(Path.Combine(dir,"frame_000225.png")) && DateTime.UtcNow<deadline)await Task.Delay(200);
        if(!File.Exists(Path.Combine(dir,"frame_000225.png"))){if(MatrixBaker.IsBaking)MatrixBaker.Cancel();throw new Exception("Frame 225 not reached: "+MatrixBaker.Status);}
        MatrixBaker.Cancel();
        while(MatrixBaker.IsBaking && DateTime.UtcNow<deadline)await Task.Delay(100);
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        try
        {
            tex.LoadImage(File.ReadAllBytes(Path.Combine(dir,"frame_000225.png")));
            int lit=tex.GetPixels32().Count(p=>p.r+p.g+p.b>0);
            if(lit==0)throw new Exception("Middle frame black without prior preview");
            if(AnimationMode.InAnimationMode())throw new Exception("Animation mode was not released after cancel");
            if(Selection.activeObject!=oldSelection)throw new Exception("Selection changed");
            return $"PASS: no prior Animation preview; middle frame has {lit} lit pixels, cancel released sampling and restored selection";
        }
        finally{UnityEngine.Object.DestroyImmediate(tex);}
    }
}
