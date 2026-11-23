using UnityEditor;
using UnityEngine;

namespace LedMatrix.Editor
{
    [InitializeOnLoad]
    public static class MatrixScenePreview
    {
        static double next;
        static MatrixScenePreview()
        {
            EditorApplication.update += Update;
            SceneView.duringSceneGui += Draw;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode && AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }
        static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || MatrixBaker.IsBaking || EditorApplication.isCompiling) return;
            double now=EditorApplication.timeSinceStartup;
            if(now<next)return;
            next=now+1.0/30;
            foreach(var matrix in Object.FindObjectsByType<MatrixVolumeRenderer>(FindObjectsSortMode.None))
            {
                if(!matrix.isActiveAndEnabled)continue;
                matrix.RenderCurrentPose();
                matrix.DrawPreview();
                matrix.GetComponent<MatrixSpoutOutput>()?.Sync();
            }
            SceneView.RepaintAll();
        }
        static void Draw(SceneView scene)
        {
            if(EditorApplication.isPlaying || Event.current.type!=EventType.Repaint)return;
            foreach(var matrix in Object.FindObjectsByType<MatrixVolumeRenderer>(FindObjectsSortMode.None))
            {
                if(!matrix.isActiveAndEnabled)continue;
                using(new Handles.DrawingScope(new Color(0.25f,0.85f,1f,0.8f),matrix.transform.localToWorldMatrix))
                    Handles.DrawWireCube(matrix.VolumeMin+matrix.VolumeSize*0.5f,matrix.VolumeSize);
            }
        }
    }
}
