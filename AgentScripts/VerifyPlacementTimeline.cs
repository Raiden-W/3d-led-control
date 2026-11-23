using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyPlacementTimeline
{
    public static async Task<string> Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var w=Resources.FindObjectsOfTypeAll<AnimationWindow>().First();
        var oldSelection=Selection.activeObject;
        Vector3 originalPosition=m.contentRoot.localPosition,originalScale=m.contentRoot.localScale;
        Quaternion originalRotation=m.contentRoot.localRotation;
        var lines=new System.Collections.Generic.List<string>();
        var method=typeof(AnimationWindow).GetMethod("OnSelectionChange",BindingFlags.NonPublic|BindingFlags.Instance);
        try
        {
            foreach(float testTime in new[]{0f,7.5f,14.966667f})
            {
                MatrixAnimationWindowPin.ReleaseByUser();
                if(w.previewing)w.previewing=false;
                await Task.Delay(100);
                Selection.activeGameObject=m.animationRoot.gameObject;method.Invoke(w,null);
                w.animationClip=m.clip;w.time=testTime;w.previewing=true;await Task.Delay(300);
                if(!MatrixAnimationWindowPin.Pinned)throw new Exception("Timeline did not auto-pin at "+testTime);
                float before=w.time;var pose=m.animationRoot.GetComponentsInChildren<Transform>().Select(t=>t.localToWorldMatrix).ToArray();
                MatrixAnimationWindowPin.PinBeforePlacement();
                Selection.activeGameObject=m.contentRoot.gameObject;method.Invoke(w,null);await Task.Delay(150);
                if(!w.previewing||w.animationClip!=m.clip||Mathf.Abs(w.time-before)>0.001f)throw new Exception("Timeline changed on placement selection");
                m.contentRoot.localPosition=originalPosition+new Vector3(0.03f,0.02f,0.01f);
                m.contentRoot.localRotation=Quaternion.Euler(0,12,0)*originalRotation;
                m.contentRoot.localScale=originalScale*1.1f;
                await Task.Delay(150);m.RenderCurrentPose();
                if(!w.previewing||Mathf.Abs(w.time-before)>0.001f)throw new Exception("Timeline changed on transform edit");
                var after=m.animationRoot.GetComponentsInChildren<Transform>().Select(t=>t.localToWorldMatrix).ToArray();
                if(pose.SequenceEqual(after))throw new Exception("Placement transform did not change model geometry");
                lines.Add($"t={testTime:F3}: selection + position/rotation/scale retained clip, preview and time={w.time:F3}");
                m.contentRoot.localPosition=originalPosition;m.contentRoot.localRotation=originalRotation;m.contentRoot.localScale=originalScale;
            }
            return "PASS: "+string.Join("; ",lines);
        }
        finally
        {
            if(w.previewing)w.previewing=false;
            MatrixAnimationWindowPin.ReleaseByUser();
            m.contentRoot.localPosition=originalPosition;m.contentRoot.localRotation=originalRotation;m.contentRoot.localScale=originalScale;
            Selection.activeObject=oldSelection;
        }
    }
}
