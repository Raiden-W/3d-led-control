using System;
using System.Linq;
using UnityEngine;
using LedMatrix;

public static class InspectQilinPose
{
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();m.playing=false;
        var mesh=new Mesh();var list=new System.Collections.Generic.List<Vector3>();var lines=new System.Collections.Generic.List<object>();
        try
        {
            foreach(float time in new[]{0f,7.5f,14.9f})
            {
                m.RenderAt(time);
                foreach(var skin in m.contentRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(mesh,false);mesh.GetVertices(list);var b=new Bounds(skin.transform.TransformPoint(list[0]),Vector3.zero);foreach(var p in list)b.Encapsulate(skin.transform.TransformPoint(p));
                    lines.Add(new{time,skin.name,baked=b.ToString(),local=mesh.bounds.ToString(),scale=skin.transform.lossyScale.ToString(),placement=m.contentRoot.position.ToString(),root=m.animationRoot.position.ToString()});
                }
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(lines,Newtonsoft.Json.Formatting.Indented);
        }
        finally{UnityEngine.Object.DestroyImmediate(mesh);m.playing=true;}
    }
}
