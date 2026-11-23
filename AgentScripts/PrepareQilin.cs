using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PrepareQilin
{
    const string Path="Assets/Models/02qilin/qilin_15s_.fbx";
    public static string Inspect()
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(Path);
        if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);
        try
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(Path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            return Newtonsoft.Json.JsonConvert.SerializeObject(new {
                customRanges=importer.clipAnimations.Select(c=>new{c.name,c.firstFrame,c.lastFrame}).ToArray(),
                clipSettings=clips.Select(c=>new{c.name,start=AnimationUtility.GetAnimationClipSettings(c).startTime,stop=AnimationUtility.GetAnimationClipSettings(c).stopTime,rate=c.frameRate,curves=AnimationUtility.GetCurveBindings(c).Length}).ToArray(),
                renderers=go.GetComponentsInChildren<Renderer>(true).Select(r=>new{r.name,active=r.gameObject.activeInHierarchy,r.enabled,type=r.GetType().Name,position=r.transform.position.ToString(),scale=r.transform.lossyScale.ToString(),bounds=r.bounds.ToString(),materials=r.sharedMaterials.Select(m=>m==null?"null":m.name+" texture="+(m.mainTexture==null?"none":AssetDatabase.GetAssetPath(m.mainTexture))).ToArray()}).ToArray()
            },Newtonsoft.Json.Formatting.Indented);
        }
        finally {UnityEngine.Object.DestroyImmediate(go);}
    }
}
