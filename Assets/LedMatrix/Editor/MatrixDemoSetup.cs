using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Klak.Spout;

namespace LedMatrix.Editor
{
    public static class MatrixDemoSetup
    {
        const string Folder="Assets/LedMatrix/Demo";
        [MenuItem("Tools/LED Matrix/Create Demo")]
        public static void Create()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/3dLedMatrix.unity") throw new InvalidOperationException("Open Assets/Scenes/3dLedMatrix.unity first.");
            if(UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>()!=null) { MatrixWindow.Open(); return; }
            Directory.CreateDirectory(Folder);
            GenerateFbx(Folder+"/AnimatedColorSphere.fbx"); AssetDatabase.Refresh();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/AnimatedColorSphere.fbx");
            importer.isReadable=true; importer.animationType=ModelImporterAnimationType.Generic; importer.importAnimation=true; importer.SaveAndReimport();
            var texture=new Texture2D(128,64,TextureFormat.RGBA32,false,false);
            for(int y=0;y<64;y++) for(int x=0;x<128;x++) texture.SetPixel(x,y,Color.HSVToRGB((float)x/128,0.85f,((x/16+y/16)%2==0)?1:0.35f));
            texture.Apply(); File.WriteAllBytes(Folder+"/ColorGrid.png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Folder+"/ColorGrid.png");
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/ColorGrid.png");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetTexture("_BaseMap",tex);
            AssetDatabase.CreateAsset(material,Folder+"/ColorGrid.mat");
            var root=new GameObject("LED Matrix · 16 × 16 × 48"); Undo.RegisterCreatedObjectUndo(root,"Create LED matrix demo");
            var matrix=root.AddComponent<MatrixVolumeRenderer>();
            matrix.voxelizer=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/LedMatrix/Shaders/MatrixVoxelize.compute"); matrix.pointShader=Shader.Find("LedMatrix/Points");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/AnimatedColorSphere.fbx");
            if(asset==null) throw new InvalidOperationException("Generated FBX failed to import.");
            var pivot=new GameObject("Content Placement"); pivot.transform.SetParent(root.transform,false);
            var content=(GameObject)PrefabUtility.InstantiatePrefab(asset); content.transform.SetParent(pivot.transform,false);
            foreach(var r in content.GetComponentsInChildren<Renderer>()) r.sharedMaterial=material;
            content.AddComponent<FbxVolumeSource>(); matrix.contentRoot=pivot.transform; matrix.animationRoot=content.transform;
            matrix.clip=AssetDatabase.LoadAllAssetsAtPath(Folder+"/AnimatedColorSphere.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
            var spout=root.AddComponent<MatrixSpoutOutput>();
            string resource=AssetDatabase.FindAssets("t:SpoutResources").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            spout.resources=AssetDatabase.LoadAssetAtPath<SpoutResources>(resource);
            var camera=Camera.main;
            if(camera!=null) { camera.transform.position=new Vector3(2.8f,2.0f,-3.5f); camera.transform.LookAt(new Vector3(0,1.15f,0)); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.007f,0.01f,0.02f); camera.nearClipPlane=0.01f; camera.fieldOfView=40; }
            var view=SceneView.lastActiveSceneView; if(view!=null) view.LookAt(new Vector3(0,1.15f,0),Quaternion.Euler(10,-30,0),3);
            matrix.RenderAt(0); spout.Sync();
            Selection.activeGameObject=root; EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); MatrixWindow.Open();
        }
        static string F(float v) => v.ToString("0.######",CultureInfo.InvariantCulture);
        static void GenerateFbx(string path)
        {
            var verts=new StringBuilder(); var normals=new StringBuilder(); var uv=new StringBuilder(); var indices=new StringBuilder();
            const int rings=12,segments=24;
            for(int y=0;y<=rings;y++) for(int x=0;x<=segments;x++)
            {
                float lat=Mathf.PI*y/rings,lon=2*Mathf.PI*x/segments;
                var p=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon));
                verts.Append(F(p.x*28)).Append(',').Append(F(p.y*28)).Append(',').Append(F(p.z*28)).Append(',');
                normals.Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',');
                uv.Append(F((float)x/segments)).Append(',').Append(F((float)y/rings)).Append(',');
            }
            int triCount=0;
            for(int y=0;y<rings;y++) for(int x=0;x<segments;x++)
            {
                int a=y*(segments+1)+x,b=a+1,c=a+segments+1,d=c+1;
                if(y>0) { indices.Append(a).Append(',').Append(c).Append(',').Append(-b-1).Append(',');triCount++; }
                if(y<rings-1) {indices.Append(b).Append(',').Append(c).Append(',').Append(-d-1).Append(',');triCount++;}
            }
            long end=(long)Math.Round(46186158000.0*59/30),middle=46186158000;
            int n=(rings+1)*(segments+1);
            string text=$@"; FBX 7.4.0 project-generated UV/animation test asset
FBXHeaderExtension: {{ FBXHeaderVersion: 1003
 FBXVersion: 7400
 Creator: ""LED Matrix Demo""
}}
GlobalSettings: {{ Version: 1000
 Properties70: {{
 P: ""UpAxis"", ""int"", ""Integer"", """",1
 P: ""UpAxisSign"", ""int"", ""Integer"", """",1
 P: ""FrontAxis"", ""int"", ""Integer"", """",2
 P: ""FrontAxisSign"", ""int"", ""Integer"", """",-1
 P: ""CoordAxis"", ""int"", ""Integer"", """",0
 P: ""CoordAxisSign"", ""int"", ""Integer"", """",1
 P: ""UnitScaleFactor"", ""double"", ""Number"", """",1
 P: ""TimeMode"", ""enum"", """", """",6
 P: ""TimeSpanStart"", ""KTime"", ""Time"", """",0
 P: ""TimeSpanStop"", ""KTime"", ""Time"", """",{end}
 }}
}}
Documents: {{ Count: 1
 Document: 1, ""Scene"", ""Scene"" {{ RootNode: 0 }}
}}
Definitions: {{ Version: 100
 Count: 6
 ObjectType: ""Geometry"" {{ Count: 1 }}
 ObjectType: ""Model"" {{ Count: 1 }}
 ObjectType: ""AnimationStack"" {{ Count: 1 }}
 ObjectType: ""AnimationLayer"" {{ Count: 1 }}
 ObjectType: ""AnimationCurveNode"" {{ Count: 1 }}
 ObjectType: ""AnimationCurve"" {{ Count: 1 }}
}}
Objects: {{
 Geometry: 100, ""Geometry::Sphere"", ""Mesh"" {{
 Vertices: *{n*3} {{ a: {verts.ToString().TrimEnd(',')} }}
 PolygonVertexIndex: *{triCount*3} {{ a: {indices.ToString().TrimEnd(',')} }}
 GeometryVersion: 124
 LayerElementNormal: 0 {{ Version: 101
 Name: """"
 MappingInformationType: ""ByVertice""
 ReferenceInformationType: ""Direct""
 Normals: *{n*3} {{ a: {normals.ToString().TrimEnd(',')} }}
 }}
 LayerElementUV: 0 {{ Version: 101
 Name: ""UVMap""
 MappingInformationType: ""ByVertice""
 ReferenceInformationType: ""Direct""
 UV: *{n*2} {{ a: {uv.ToString().TrimEnd(',')} }}
 }}
 Layer: 0 {{ Version: 100
 LayerElement: {{ Type: ""LayerElementNormal""
 TypedIndex: 0 }}
 LayerElement: {{ Type: ""LayerElementUV""
 TypedIndex: 0 }}
 }}
 }}
 Model: 200, ""Model::Sphere"", ""Mesh"" {{ Version: 232
 Properties70: {{
 P: ""Lcl Translation"", ""Lcl Translation"", """", ""A"",0,70,0
 P: ""Lcl Rotation"", ""Lcl Rotation"", """", ""A"",0,0,0
 P: ""Lcl Scaling"", ""Lcl Scaling"", """", ""A"",1,1,1
 }}
 Shading: T
 Culling: ""CullingOff""
 }}
 AnimationStack: 300, ""AnimStack::Float"", """" {{ Properties70: {{
 P: ""LocalStart"", ""KTime"", ""Time"", """",0
 P: ""LocalStop"", ""KTime"", ""Time"", """",{end}
 P: ""ReferenceStart"", ""KTime"", ""Time"", """",0
 P: ""ReferenceStop"", ""KTime"", ""Time"", """",{end}
 }} }}
 AnimationLayer: 301, ""AnimLayer::BaseLayer"", """" {{ }}
 AnimationCurveNode: 302, ""AnimCurveNode::T"", """" {{ Properties70: {{
 P: ""d|X"", ""Number"", """", ""A"",0
 P: ""d|Y"", ""Number"", """", ""A"",70
 P: ""d|Z"", ""Number"", """", ""A"",0
 }} }}
 AnimationCurve: 303, ""AnimCurve::TY"", """" {{ Default: 70
 KeyVer: 4008
 KeyTime: *3 {{ a: 0,{middle},{end} }}
 KeyValueFloat: *3 {{ a: 70,170,70 }}
 KeyAttrFlags: *3 {{ a: 24836,24836,24836 }}
 KeyAttrDataFloat: *12 {{ a: 0,0,0,0,0,0,0,0,0,0,0,0 }}
 KeyAttrRefCount: *3 {{ a: 1,1,1 }}
 }}
}}
Connections: {{
 C: ""OO"",100,200
 C: ""OO"",200,0
 C: ""OO"",301,300
 C: ""OO"",302,301
 C: ""OP"",302,200,""Lcl Translation""
 C: ""OP"",303,302,""d|Y""
}}
Takes: {{ Current: ""Float"" }}
";
            text=text.Replace("project-generated UV/animation test asset","project file");
            text=text.Replace("{","{\n").Replace("}","\n}\n");
            File.WriteAllText(path,text);
        }
    }
}
