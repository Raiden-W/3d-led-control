using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyMatrixParameters
{
    static void Require(bool ok,string message) {if(!ok)throw new Exception(message);}
    public static string Run()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode");
        MatrixWindow.Open();var w=Resources.FindObjectsOfTypeAll<MatrixWindow>().First();w.CreateGUI();
        var count=w.rootVisualElement.Query<Vector3IntField>().First();
        var spacing=w.rootVisualElement.Query<Vector3Field>().First();
        var apply=typeof(MatrixWindow).GetMethod("ApplyMatrixSettings",BindingFlags.Instance|BindingFlags.NonPublic);
        var originalCounts=m.pointCounts;var originalSpacing=m.spacing;var originalPattern=m.pattern;
        try
        {
            Require(m.Counts==new Vector3Int(16,48,16),"default dimensions");
            Require(m.AtlasColumns==4 && m.AtlasRows==4 && m.TileWidth*4==256 && m.TileHeight*4==768,"default atlas layout");
            Undo.IncrementCurrentGroup();count.value=new Vector3Int(8,12,5);apply.Invoke(w,null);Undo.FlushUndoRecordObjects();
            Require(m.Counts==new Vector3Int(8,12,5),"UI point counts");
            Require(m.PointCount==480&&m.Dimensions==new Vector3Int(32,48,20),"point/voxel counts");
            Require(m.AtlasTexture.width==96&&m.AtlasTexture.height==96&&m.AtlasColumns==3&&m.AtlasRows==2,"non-square atlas layout");
            m.pattern=MatrixVolumeRenderer.TestPattern.Red;m.RenderCurrentPose();
            var request=AsyncGPUReadback.Request(m.AtlasTexture,0,TextureFormat.RGBA32);request.WaitForCompletion();
            Require(!request.hasError,"atlas readback");var bytes=request.GetData<byte>();
            for(int tile=0;tile<6;tile++)
            {
                int x=(tile%3)*m.TileWidth+m.TileWidth/2,y=(1-tile/3)*m.TileHeight+m.TileHeight/2,i=(y*m.AtlasTexture.width+x)*4;
                Require(bytes[i]==(tile<5?255:0),"active or empty tile "+tile);
            }
            Undo.PerformUndo();Require(m.Counts==originalCounts,"undo dimensions");
            m.RenderCurrentPose();Require(m.AtlasTexture.width==256&&m.AtlasTexture.height==768,"undo reallocated atlas");
            Undo.PerformRedo();Require(m.Counts==new Vector3Int(8,12,5),"redo dimensions");
            Undo.IncrementCurrentGroup();spacing.value=new Vector3(25,40,75);apply.Invoke(w,null);Undo.FlushUndoRecordObjects();
            Require(Vector3.Distance(m.Spacing,new Vector3(.025f,.04f,.075f))<0.00001f,"spacing mm conversion");
            Require(Vector3.Distance(m.VolumeSize,new Vector3(.2f,.48f,.375f))<0.00001f,"physical dimensions");
            Undo.PerformUndo();Require(Vector3.Distance(m.Spacing,originalSpacing)<0.00001f,"undo spacing");
            Undo.PerformRedo();Require(Vector3.Distance(m.Spacing,new Vector3(.025f,.04f,.075f))<0.00001f,"redo spacing");
            return "PASS default 16×16×48 atlas; 8×5×12 parameterized grid, 3×2 layout and empty tile; dimension/spacing undo and redo; anisotropic 25/40/75mm dimensions.";
        }
        finally
        {
            m.pointCounts=originalCounts;m.spacing=originalSpacing;m.pattern=originalPattern;m.RenderCurrentPose();m.DrawPreview();
        }
    }
}
