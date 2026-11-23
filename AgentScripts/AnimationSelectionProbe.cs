using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LedMatrix;

public static class AnimationSelectionProbe
{
    public static async Task<string> Run(bool locked=false)
    {
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var window=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault();
        if(window==null)throw new Exception("Open Animation window before running");
        var old=Selection.activeObject;var oldTime=window.time;var oldPreview=window.previewing;
        try
        {
            Selection.activeGameObject=matrix.animationRoot.gameObject;await Task.Delay(300);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var selectionMethod=typeof(AnimationWindow).GetMethod("OnSelectionChange",flags);
            selectionMethod.Invoke(window,null);
            window.animationClip=matrix.clip;window.time=0;window.previewing=true;await Task.Delay(300);
            var lockField=typeof(AnimationWindow).GetField("m_LockTracker",flags);
            var tracker=lockField.GetValue(window);
            var isLocked=tracker.GetType().GetProperty("isLocked",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            isLocked.SetValue(tracker,locked);
            string before=$"time={window.time} preview={window.previewing} clip={(window.animationClip?window.animationClip.name:"null")}";
            Selection.activeGameObject=matrix.contentRoot.gameObject;selectionMethod.Invoke(window,null);await Task.Delay(300);
            return before+$" -> selection changed: time={window.time} preview={window.previewing} clip={(window.animationClip?window.animationClip.name:"null")}";
        }
        finally {
            var f=typeof(AnimationWindow).GetField("m_LockTracker",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var v=f.GetValue(window);v.GetType().GetProperty("isLocked",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(v,false);
            Selection.activeObject=old;await Task.Delay(100);window.time=oldTime;if(oldPreview&&window.canPreview)window.previewing=true;}
    }
}
