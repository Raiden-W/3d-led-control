using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyBakeCancel
{
    public static async Task<string> Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        if(EditorApplication.isPlaying || MatrixBaker.IsBaking || AnimationMode.InAnimationMode())throw new Exception("Need idle Edit Mode");
        var oldSelection=Selection.activeObject;var originalPlacement=m.contentRoot.localToWorldMatrix;
        Selection.activeGameObject=m.animationRoot.gameObject;
        EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
        var w=Resources.FindObjectsOfTypeAll<AnimationWindow>().First();
        typeof(AnimationWindow).GetMethod("OnSelectionChange",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(w,null);
        w.animationClip=m.clip;w.time=7.5f;w.previewing=true;
        MatrixAnimationWindowPin.PinBeforePlacement();Selection.activeGameObject=m.contentRoot.gameObject;
        try
        {
            var selectionBefore=Selection.activeObject;float timeBefore=w.time;
            string parent=Path.GetFullPath("Verification/QilinBakeCancel");Directory.CreateDirectory(parent);
            var old=Directory.GetDirectories(parent).ToHashSet();
            MatrixBaker.Begin(m,parent);
            await Task.Delay(400);MatrixBaker.Cancel();
            var deadline=DateTime.UtcNow.AddSeconds(30);
            while(MatrixBaker.IsBaking && DateTime.UtcNow<deadline)await Task.Delay(100);
            if(MatrixBaker.IsBaking)throw new Exception("Cancellation timed out");
            var dir=Directory.GetDirectories(parent).Single(p=>!old.Contains(p));
            var manifest=JsonUtility.FromJson<MatrixBaker.Manifest>(File.ReadAllText(Path.Combine(dir,"manifest.json")));
            if(manifest.complete)throw new Exception("Cancelled manifest marked complete");
            if(!w.previewing||Mathf.Abs(w.time-timeBefore)>0.0001f)throw new Exception("Preview time not restored on cancel");
            if(Selection.activeObject!=selectionBefore)throw new Exception("Selection not restored on cancel");
            if(m.contentRoot.localToWorldMatrix!=originalPlacement)throw new Exception("Placement changed on cancel");
            return $"PASS cancel: incomplete manifest, {manifest.completedFrames} frames retained, preview {w.time:F3}s and placement selection restored";
        }
        finally
        {
            if(w.previewing)w.previewing=false;
            MatrixAnimationWindowPin.ReleaseByUser();Selection.activeObject=oldSelection;
        }
    }
}
