using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LedMatrix;

public static class TestQilinRealtime
{
    public static string Setup()
    {
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        matrix.playing=false;
        const string path="Assets/Models/02qilin/qilin_15s_.fbx";
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(matrix.contentRoot!=null) Undo.DestroyObjectImmediate(matrix.contentRoot.gameObject);
        var placement=new GameObject("Content Placement · Qilin");
        Undo.RegisterCreatedObjectUndo(placement,"Test Qilin FBX");placement.transform.SetParent(matrix.transform,false);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(asset);Undo.RegisterCreatedObjectUndo(model,"Test Qilin FBX");
        model.transform.SetParent(placement.transform,false);
        matrix.contentRoot=placement.transform;matrix.animationRoot=model.transform;
        matrix.clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        // This untextured cube is a remote export helper, not part of the character.
        var helper=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Box002");
        if(helper!=null) helper.gameObject.SetActive(false);
        var source=model.AddComponent<FbxVolumeSource>();source.enabled=false;
        var samples=new List<object>();Bounds overall=default;bool first=true;
        int triangles=model.GetComponentsInChildren<Renderer>().Sum(r=>{var sm=r as SkinnedMeshRenderer;var mesh=sm!=null?sm.sharedMesh:r.GetComponent<MeshFilter>().sharedMesh;return Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)mesh.GetIndexCount(s)/3);});
        {
            for(int sample=0;sample<=30;sample++)
            {
                float time=matrix.clip.length*sample/30;matrix.RenderAt(time);
                Bounds pose=source.WorldGeometryBounds();
                if(first){overall=pose;first=false;}else overall.Encapsulate(pose);
                if(sample%5==0)samples.Add(new{time,bounds=pose.ToString()});
            }
        }
        float factor=Mathf.Min(0.72f/Mathf.Max(overall.size.x,0.001f),2.25f/Mathf.Max(overall.size.y,0.001f),0.72f/Mathf.Max(overall.size.z,0.001f));
        placement.transform.localScale=Vector3.one*factor;
        placement.transform.localPosition=new Vector3(0,1.175f,0)-overall.center*factor;
        source.enabled=true;matrix.fillInterior=false;matrix.time=0;matrix.outputFps=60;matrix.pattern=MatrixVolumeRenderer.TestPattern.Content;
        matrix.RenderAt(0);matrix.GetComponent<MatrixSpoutOutput>().Sync();
        EditorSceneManager.MarkSceneDirty(matrix.gameObject.scene);EditorSceneManager.SaveScene(matrix.gameObject.scene);
        LedMatrix.Editor.MatrixWindow.Open();
        string report=Newtonsoft.Json.JsonConvert.SerializeObject(new{model=path,clip=matrix.clip.name,matrix.clip.length,matrix.clip.frameRate,triangles,fullAnimationBounds=overall.ToString(),placementScale=factor,placementPosition=placement.transform.localPosition.ToString(),excludedHelper=helper!=null?helper.name:"none",samples},Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("Verification/Qilin");File.WriteAllText("Verification/Qilin/setup.json",report);
        return report;
    }
}
