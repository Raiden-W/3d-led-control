using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyQilinBake
{
    static byte[] Frame(string path,out int lit)
    {
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        try
        {
            if(!tex.LoadImage(File.ReadAllBytes(path)))throw new Exception("Cannot read PNG "+path);
            var pixels=tex.GetPixels32();lit=pixels.Count(p=>p.r!=0||p.g!=0||p.b!=0);
            return pixels.SelectMany(p=>new[]{p.r,p.g,p.b,p.a}).ToArray();
        }
        finally{UnityEngine.Object.DestroyImmediate(tex);}
    }
    public static async Task<string> Run()
    {
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        if(EditorApplication.isPlaying||MatrixBaker.IsBaking)throw new Exception("Bake test requires idle Edit Mode");
        string parent=Path.GetFullPath("Verification/QilinBake");Directory.CreateDirectory(parent);
        var old=Directory.GetDirectories(parent).ToHashSet();
        var previews=Resources.FindObjectsOfTypeAll<AnimationWindow>();
        var preview=previews.FirstOrDefault(w=>w.previewing&&w.animationClip==matrix.clip);
        bool startedPreview=false;
        if(preview==null)
        {
            Selection.activeGameObject=matrix.animationRoot.gameObject;
            EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
            preview=Resources.FindObjectsOfTypeAll<AnimationWindow>().First();
            typeof(AnimationWindow).GetMethod("OnSelectionChange",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(preview,null);
            preview.animationClip=matrix.clip;preview.time=3.5f;preview.previewing=true;startedPreview=true;
        }
        float originalTime=preview.time;bool originalPreview=preview.previewing;
        var placement=matrix.contentRoot.localToWorldMatrix;
        MatrixBaker.Begin(matrix,parent);
        var deadline=DateTime.UtcNow.AddMinutes(8);
        while(MatrixBaker.IsBaking && DateTime.UtcNow<deadline)await Task.Delay(250);
        if(MatrixBaker.IsBaking){MatrixBaker.Cancel();throw new Exception("Bake did not finish within eight minutes");}
        string dir=Directory.GetDirectories(parent).Single(x=>!old.Contains(x));
        var manifest=JsonUtility.FromJson<MatrixBaker.Manifest>(File.ReadAllText(Path.Combine(dir,"manifest.json")));
        if(!manifest.complete || manifest.completedFrames!=manifest.expectedFrames)throw new Exception("Incomplete bake: "+MatrixBaker.Status);
        int imageCount=Directory.GetFiles(dir,"frame_*.png").Length;
        if(imageCount!=manifest.expectedFrames)throw new Exception("PNG count mismatch");
        var first=Frame(Path.Combine(dir,"frame_000000.png"),out int firstLit);
        var mid=Frame(Path.Combine(dir,"frame_000225.png"),out int midLit);
        var late=Frame(Path.Combine(dir,"frame_000400.png"),out int lateLit);
        if(midLit==0)throw new Exception("Middle frame is black");
        if(first.SequenceEqual(mid) || mid.SequenceEqual(late))throw new Exception("Animation frames did not change");
        if(matrix.contentRoot.localToWorldMatrix!=placement)throw new Exception("Placement changed during bake");
        if(originalPreview && (!preview.previewing || Mathf.Abs(preview.time-originalTime)>1e-4f))
            throw new Exception($"Animation preview not restored: expected {originalTime}, got {preview.time}, preview={preview.previewing}");
        if(startedPreview && preview.previewing)preview.previewing=false;
        string summary=$"PASS {imageCount} frames at {manifest.fps} fps; pixels lit frame 0={firstLit}, 225={midLit}, 400={lateLit}; animation preview restored at {originalTime:F3}s; output={dir}";
        File.WriteAllText(Path.Combine(dir,"verification.txt"),summary);return summary;
    }
}
