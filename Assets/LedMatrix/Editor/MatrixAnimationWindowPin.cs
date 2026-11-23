using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LedMatrix.Editor
{
    /// <summary>Keep Unity's Animation window on its current FBX while its placement parent is selected.</summary>
    [InitializeOnLoad]
    public static class MatrixAnimationWindowPin
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static readonly FieldInfo TrackerField=typeof(AnimationWindow).GetField("m_LockTracker",Flags);
        static readonly PropertyInfo LockedProperty=TrackerField?.FieldType.GetProperty("isLocked",Flags);
        static AnimationWindow ownedWindow;
        static bool suspended;
        public static bool Supported => TrackerField!=null && LockedProperty!=null;
        public static bool Pinned => ownedWindow!=null && IsLocked(ownedWindow);

        static MatrixAnimationWindowPin()
        {
            EditorApplication.update+=Update;
            EditorApplication.playModeStateChanged+=OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=Release;
        }
        static bool IsLocked(AnimationWindow window)
        {
            var tracker=TrackerField?.GetValue(window);
            return tracker!=null && LockedProperty!=null && (bool)LockedProperty.GetValue(tracker);
        }
        static void SetLocked(AnimationWindow window,bool value)
        {
            var tracker=TrackerField?.GetValue(window);
            if(tracker!=null && LockedProperty!=null)LockedProperty.SetValue(tracker,value);
        }
        static void Update()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !Supported)return;
            if(ownedWindow!=null)
            {
                if(!IsLocked(ownedWindow)) {ownedWindow=null;suspended=true;return;}
                if(!ownedWindow.previewing) {Release();suspended=false;return;}
                var matrix=Object.FindFirstObjectByType<MatrixVolumeRenderer>();
                var selected=Selection.activeTransform;
                if(matrix==null || matrix.contentRoot==null || selected==null ||
                   (selected!=matrix.contentRoot && !selected.IsChildOf(matrix.contentRoot)))
                { Release();return; }
                return;
            }
            var windows=Resources.FindObjectsOfTypeAll<AnimationWindow>();
            bool anyPreview=false;
            foreach(var window in windows)
            {
                if(!window.previewing)continue;
                anyPreview=true;
                if(!suspended)TryPin(window);
            }
            if(!anyPreview)suspended=false;
        }
        static void TryPin(AnimationWindow window)
        {
            if(IsLocked(window) || window.animationClip==null)return;
            var matrix=Object.FindFirstObjectByType<MatrixVolumeRenderer>();
            var selected=Selection.activeTransform;
            if(matrix==null || matrix.animationRoot==null || selected==null ||
                (selected!=matrix.animationRoot && !selected.IsChildOf(matrix.animationRoot)))return;
            SetLocked(window,true);ownedWindow=window;
        }
        public static void PinBeforePlacement()
        {
            if(!Supported || suspended || ownedWindow!=null)return;
            foreach(var window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
                if(window.previewing)TryPin(window);
        }
        public static void ReleaseByUser() {Release();suspended=true;}
        static void Release()
        {
            if(ownedWindow!=null && Supported && IsLocked(ownedWindow))SetLocked(ownedWindow,false);
            ownedWindow=null;
        }
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode) {Release();suspended=false;}
        }
    }
}
