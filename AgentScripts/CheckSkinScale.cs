using System;
using System.Linq;
using UnityEngine;
using LedMatrix;
public static class CheckSkinScale
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();m.playing=false;m.RenderAt(0);
        var s=m.contentRoot.GetComponentsInChildren<SkinnedMeshRenderer>().First();
        var a=new Mesh();var b=new Mesh();s.BakeMesh(a,false);s.BakeMesh(b,true);
        var src=s.sharedMesh.vertices;var weights=s.sharedMesh.boneWeights;var bind=s.sharedMesh.bindposes;
        var av=a.vertices;var bv=b.vertices;var unit=Matrix4x4.TRS(s.transform.position,s.transform.rotation,Vector3.one);
        var result=new System.Collections.Generic.List<object>();
        for(int i=0;i<5;i++)
        {
            var w=weights[i];Vector3 truth=Vector3.zero;
            truth+=(s.bones[w.boneIndex0].localToWorldMatrix*bind[w.boneIndex0]).MultiplyPoint3x4(src[i])*w.weight0;
            truth+=(s.bones[w.boneIndex1].localToWorldMatrix*bind[w.boneIndex1]).MultiplyPoint3x4(src[i])*w.weight1;
            truth+=(s.bones[w.boneIndex2].localToWorldMatrix*bind[w.boneIndex2]).MultiplyPoint3x4(src[i])*w.weight2;
            truth+=(s.bones[w.boneIndex3].localToWorldMatrix*bind[w.boneIndex3]).MultiplyPoint3x4(src[i])*w.weight3;
            result.Add(new{truth=truth.ToString(),falseFull=s.transform.TransformPoint(av[i]).ToString(),trueUnit=unit.MultiplyPoint3x4(bv[i]).ToString(),errorTrue=Vector3.Distance(truth,unit.MultiplyPoint3x4(bv[i]))});
        }
        UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
        return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }
}
