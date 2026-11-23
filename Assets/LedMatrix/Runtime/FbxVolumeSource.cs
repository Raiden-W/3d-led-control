using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LedMatrix
{
    public sealed class FbxVolumeSource : VolumeSource
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex { public Vector3 position; public Vector2 uv; public Vector4 color; }
        sealed class Entry
        {
            public Renderer renderer;
            public Mesh mesh;
            public bool owned;
            public ComputeBuffer vertices;
            public ComputeBuffer[] indices;
            public ComputeBuffer allIndices;
            public Vertex[] data;
            public readonly List<Vector3> positions=new List<Vector3>();
            public SkinnedMeshRenderer skin;
            public Vector3[] rest;
            public Matrix4x4[] bindposes,boneMatrices;
            public Transform[] bones;
            public BoneWeight1[] weights;
            public int[] weightOffsets;
        }
        readonly List<Entry> entries=new List<Entry>();
        bool initialized;
        public bool showSourceMesh=true;
        void OnDisable() { Dispose(); }
        public void Rebuild() { Dispose(); }
        void Dispose()
        {
            foreach(var e in entries)
            {
                e.vertices?.Release(); foreach(var b in e.indices) b?.Release(); e.allIndices?.Release();
                if(e.owned && e.mesh != null) DestroyImmediate(e.mesh);
                if(e.renderer != null) e.renderer.forceRenderingOff=false;
            }
            entries.Clear(); initialized=false;
        }
        void Initialize()
        {
            foreach(var r in GetComponentsInChildren<Renderer>())
            {
                Mesh mesh=null; bool owned=false;
                if(r is SkinnedMeshRenderer skin) mesh=skin.sharedMesh;
                else if(r.TryGetComponent<MeshFilter>(out var filter)) mesh=filter.sharedMesh;
                if(mesh==null || mesh.vertexCount==0) continue;
                if(!mesh.isReadable) { Debug.LogError("Enable Read/Write on the FBX model importer: "+mesh.name,this); continue; }
                var e=new Entry {renderer=r,mesh=mesh,owned=owned,data=new Vertex[mesh.vertexCount],indices=new ComputeBuffer[mesh.subMeshCount]};
                if(r is SkinnedMeshRenderer skinned)
                {
                    e.skin=skinned;e.rest=mesh.vertices;e.bindposes=mesh.bindposes;e.bones=skinned.bones;
                    e.boneMatrices=new Matrix4x4[e.bones.Length];
                    e.weights=mesh.GetAllBoneWeights().ToArray();
                    var counts=mesh.GetBonesPerVertex();
                    {
                        e.weightOffsets=new int[mesh.vertexCount+1];
                        for(int i=0;i<mesh.vertexCount;i++) e.weightOffsets[i+1]=e.weightOffsets[i]+counts[i];
                    }
                    // Keep the existing BlendShape path; this explicit bone path avoids
                    // BakeMesh transform-scale ambiguity on nested/scaled FBX skeletons.
                    if(mesh.blendShapeCount>0){e.mesh=new Mesh();e.owned=true;skinned.BakeMesh(e.mesh);}
                }
                var uv=mesh.uv; var colors=mesh.colors;
                for(int i=0;i<e.data.Length;i++) { e.data[i].uv=i<uv.Length?uv[i]:Vector2.zero; e.data[i].color=i<colors.Length?(Vector4)colors[i]:Vector4.one; }
                var all=new List<int>();
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    if(mesh.GetTopology(s)!=MeshTopology.Triangles) continue;
                    int[] indices=mesh.GetTriangles(s); if(indices.Length==0) continue;
                    e.indices[s]=new ComputeBuffer(indices.Length,sizeof(int)); e.indices[s].SetData(indices); all.AddRange(indices);
                }
                if(all.Count==0) { if(owned) DestroyImmediate(mesh); continue; }
                e.allIndices=new ComputeBuffer(all.Count,sizeof(int)); e.allIndices.SetData(all);
                e.vertices=new ComputeBuffer(e.data.Length,Marshal.SizeOf<Vertex>());
                entries.Add(e);
            }
            initialized=entries.Count>0;
        }
        public override void WriteVolume(MatrixVolumeRenderer target)
        {
            if(!initialized || entries.Count==0) Initialize();
            foreach(var e in entries)
            {
                if(e.renderer==null) continue;
                UpdatePositions(e);
                e.vertices.SetData(e.data);
                var cs=target.voxelizer;
                cs.SetMatrix("_ToVolume",target.transform.worldToLocalMatrix*(e.skin!=null&&!e.owned?Matrix4x4.identity:e.renderer.localToWorldMatrix));
                foreach(int k in new[]{target.SurfaceKernel,target.FillKernel}) cs.SetBuffer(k,"_Vertices",e.vertices);
                var materials=e.renderer.sharedMaterials;
                for(int s=0;s<e.indices.Length;s++)
                {
                    var indices=e.indices[s]; if(indices==null) continue;
                    var mat=s<materials.Length?materials[s]:null;
                    string texName=mat!=null&&mat.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                    Texture tex=mat!=null&&mat.HasProperty(texName)?mat.GetTexture(texName):null;
                    Color tint=Color.white;
                    if(mat!=null) { if(mat.HasProperty("_BaseColor")) tint=mat.GetColor("_BaseColor"); else if(mat.HasProperty("_Color")) tint=mat.GetColor("_Color"); }
                    var scale=mat!=null&&mat.HasProperty(texName)?mat.GetTextureScale(texName):Vector2.one;
                    var offset=mat!=null&&mat.HasProperty(texName)?mat.GetTextureOffset(texName):Vector2.zero;
                    cs.SetVector("_MapST",new Vector4(scale.x,scale.y,offset.x,offset.y));
                    cs.SetVector("_BaseColor",tint.linear);
                    cs.SetTexture(target.SurfaceKernel,"_BaseMap",tex!=null?tex:Texture2D.whiteTexture);
                    cs.SetBuffer(target.SurfaceKernel,"_Indices",indices);
                    cs.SetInt("_TriangleCount",indices.count/3);
                    cs.Dispatch(target.SurfaceKernel,(indices.count/3+63)/64,1,1);
                }
                if(target.fillInterior)
                {
                    cs.SetBuffer(target.FillKernel,"_Indices",e.allIndices); cs.SetInt("_TriangleCount",e.allIndices.count/3);
                    cs.Dispatch(target.FillKernel,(target.Dimensions.x+7)/8,(target.Dimensions.y+7)/8,1);
                }
                e.renderer.forceRenderingOff=!showSourceMesh;
            }
        }
        static void UpdatePositions(Entry e)
        {
            if(e.skin!=null && !e.owned)
            {
                for(int b=0;b<e.boneMatrices.Length;b++) e.boneMatrices[b]=e.bones[b]!=null?e.bones[b].localToWorldMatrix*e.bindposes[b]:e.renderer.localToWorldMatrix;
                for(int i=0;i<e.data.Length;i++)
                {
                    Vector3 p=Vector3.zero;
                    for(int j=e.weightOffsets[i];j<e.weightOffsets[i+1];j++)
                    {
                        var w=e.weights[j];p+=e.boneMatrices[w.boneIndex].MultiplyPoint3x4(e.rest[i])*w.weight;
                    }
                    e.data[i].position=e.weightOffsets[i+1]>e.weightOffsets[i]?p:e.renderer.transform.TransformPoint(e.rest[i]);
                }
            }
            else
            {
                if(e.skin!=null)e.skin.BakeMesh(e.mesh);
                e.mesh.GetVertices(e.positions);
                for(int i=0;i<e.data.Length;i++)e.data[i].position=e.positions[i];
            }
        }
        public Bounds WorldGeometryBounds()
        {
            if(!initialized || entries.Count==0)Initialize();
            var bounds=new Bounds();bool first=true;
            foreach(var e in entries)
            {
                UpdatePositions(e);
                foreach(var v in e.data)
                {
                    Vector3 p=e.skin!=null&&!e.owned?v.position:e.renderer.transform.TransformPoint(v.position);
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
