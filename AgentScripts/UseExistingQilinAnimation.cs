using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LedMatrix;

public static class UseExistingQilinAnimation
{
    public static string Run()
    {
        const string path="Assets/Models/02qilin/qilin_15s_.fbx";
        var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var dest=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Qilin Animation.anim");
        var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animations/Qilin Animator Controller.controller");
        if(AnimationUtility.GetCurveBindings(dest).Length!=0 || AnimationUtility.GetObjectReferenceCurveBindings(dest).Length!=0)
            throw new Exception("Existing Qilin Animation is not empty; refusing to replace its curves.");
        var bindings=AnimationUtility.GetCurveBindings(source);
        var curves=bindings.Select(b=>AnimationUtility.GetEditorCurve(source,b)).ToArray();
        if(bindings.Length==0)throw new Exception("FBX contains no editable animation curves");
        Undo.RecordObject(dest,"Populate Qilin animation from FBX");
        dest.frameRate=source.frameRate;
        AnimationUtility.SetEditorCurves(dest,bindings,curves);
        foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            AnimationUtility.SetObjectReferenceCurve(dest,binding,AnimationUtility.GetObjectReferenceCurve(source,binding));
        var settings=AnimationUtility.GetAnimationClipSettings(dest);
        var sourceSettings=AnimationUtility.GetAnimationClipSettings(source);
        settings.startTime=sourceSettings.startTime;settings.stopTime=sourceSettings.stopTime;
        AnimationUtility.SetAnimationClipSettings(dest,settings);
        EditorUtility.SetDirty(dest);
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        Undo.RecordObject(matrix,"Use existing Qilin animation");matrix.clip=dest;
        var animator=matrix.animationRoot.GetComponent<Animator>();Undo.RecordObject(animator,"Use existing Qilin controller");
        animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
        var scene=matrix.gameObject.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return $"Existing controller={controller.name}; existing clip={dest.name}; copied curves={bindings.Length}; duration={dest.length}; fps={dest.frameRate}; loop={settings.loopTime}.";
    }
}
