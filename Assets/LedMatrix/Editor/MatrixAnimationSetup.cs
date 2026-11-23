using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LedMatrix.Editor
{
    public static class MatrixAnimationSetup
    {
        public static void Assign(MatrixVolumeRenderer matrix)
        {
            if(matrix.animationRoot==null || matrix.clip==null)return;
            var animator=matrix.animationRoot.GetComponent<Animator>();
            if(animator==null)animator=Undo.AddComponent<Animator>(matrix.animationRoot.gameObject);
            var existing=animator.runtimeAnimatorController;
            if(existing!=null && !AssetDatabase.GetAssetPath(existing).StartsWith("Assets/LedMatrix/Generated/"))
            {
                Debug.LogWarning("已有 Animator Controller 已保留。请在 Animator 中设置播放片段。",animator);return;
            }
            Directory.CreateDirectory("Assets/LedMatrix/Generated");
            var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(matrix.clip));
            var path=$"Assets/LedMatrix/Generated/{guid}.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine=controller.layers[0].stateMachine;
            AnimatorState state=stateMachine.states.Length>0?stateMachine.states[0].state:stateMachine.AddState("播放");
            Undo.RecordObject(state,"Set matrix animation");state.motion=matrix.clip;
            stateMachine.defaultState=state;
            Undo.RecordObject(animator,"Set matrix Animator");animator.runtimeAnimatorController=controller;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
            EditorUtility.SetDirty(state);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(animator);
        }
    }
}
