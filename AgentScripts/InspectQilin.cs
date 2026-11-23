using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InspectQilin
{
    public static string Run()
    {
        const string path="Assets/Models/02qilin/qilin_15s_.fbx";
        var assets=AssetDatabase.LoadAllAssetsAtPath(path);
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new {
            readable=importer.isReadable,rig=importer.animationType.ToString(),scale=importer.globalScale,
            meshes=assets.OfType<Mesh>().Select(m=>new {m.name,m.vertexCount,m.subMeshCount,triangles=m.subMeshCount>0?Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3):0,bounds=m.bounds.ToString()}).ToArray(),
            clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).Select(c=>new {c.name,c.length,c.frameRate,c.isHumanMotion}).ToArray(),
            materials=assets.OfType<Material>().Select(m=>new{m.name,shader=m.shader.name}).ToArray(),
            ranges=importer.defaultClipAnimations.Select(c=>new{c.name,c.firstFrame,c.lastFrame}).ToArray()
        },Newtonsoft.Json.Formatting.Indented);
    }
}
