using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LedMatrix;
using LedMatrix.Editor;
public static class VerifyEditPlaying
{
    public static async Task<string> Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var previous=Selection.activeObject;
        var panel=Resources.FindObjectsOfTypeAll<MatrixWindow>().First();panel.CreateGUI();
        var toggle=panel.rootVisualElement.Query<Toggle>().ToList().First(t=>t.label.StartsWith("Playing"));
        var anim=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault();
        try
        {
            if(anim!=null&&anim.previewing)anim.previewing=false;
            toggle.SetValueWithoutNotify(false);toggle.value=true;await Task.Delay(700);
            anim=Resources.FindObjectsOfTypeAll<AnimationWindow>().First();
            if(!anim.previewing || !anim.playing || anim.animationClip!=m.clip)throw new Exception("Panel could not start native animation preview");
            toggle.value=false;await Task.Delay(150);
            if(anim.playing)throw new Exception("Panel could not pause native animation preview");
            return "PASS: Playing starts and pauses native Animation window in Edit mode";
        }
        finally
        {
            if(anim!=null&&anim.previewing)anim.previewing=false;
            MatrixAnimationWindowPin.ReleaseByUser();Selection.activeObject=previous;
        }
    }
}
