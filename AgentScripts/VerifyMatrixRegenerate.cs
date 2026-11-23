using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyMatrixRegenerate
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static async Task<string> Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Requires Edit Mode");
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        MatrixWindow.Open();var panel=Resources.FindObjectsOfTypeAll<MatrixWindow>().First();panel.CreateGUI();
        var count=panel.rootVisualElement.Query<Vector3IntField>().First();
        var spacing=panel.rootVisualElement.Query<Vector3Field>().First();
        var resolution=panel.rootVisualElement.Query<IntegerField>().ToList().First(x=>x.label.StartsWith("Resolution"));
        var apply=typeof(MatrixWindow).GetMethod("ApplyMatrixSettings",BindingFlags.Instance|BindingFlags.NonPublic);
        var originalCount=matrix.pointCounts;var originalSpacing=matrix.spacing;var originalResolution=matrix.resolutionScale;
        var originalAtlas=matrix.AtlasTexture;var originalVolume=matrix.Volume;
        try
        {
            count.value=new Vector3Int(8,12,5);spacing.value=new Vector3(25,40,75);resolution.value=2;
            await Task.Delay(250);
            Check(matrix.pointCounts==originalCount && matrix.spacing==originalSpacing && matrix.resolutionScale==originalResolution,"editing draft changed active configuration");
            Check(ReferenceEquals(matrix.AtlasTexture,originalAtlas)&&ReferenceEquals(matrix.Volume,originalVolume),"draft edit rebuilt resources");
            Check(count.value==new Vector3Int(8,12,5)&&spacing.value==new Vector3(25,40,75)&&resolution.value==2,"draft not preserved by update loop");
            count.value=new Vector3Int(0,12,5);apply.Invoke(panel,null);
            Check(ReferenceEquals(matrix.AtlasTexture,originalAtlas)&&matrix.pointCounts==originalCount,"invalid draft changed active configuration");
            count.value=new Vector3Int(8,12,5);Undo.IncrementCurrentGroup();apply.Invoke(panel,null);Undo.FlushUndoRecordObjects();
            Check(matrix.pointCounts==new Vector3Int(8,12,5)&&Vector3.Distance(matrix.spacing,new Vector3(.025f,.04f,.075f))<1e-5f&&matrix.resolutionScale==2,"apply did not commit all values");
            Check(matrix.AtlasTexture.width==48&&matrix.AtlasTexture.height==48&&!ReferenceEquals(matrix.AtlasTexture,originalAtlas),"apply did not rebuild atlas");
            Undo.PerformUndo();await Task.Delay(100);
            Check(matrix.pointCounts==originalCount&&matrix.spacing==originalSpacing&&matrix.resolutionScale==originalResolution,"undo did not restore all values together");
            Undo.PerformRedo();await Task.Delay(100);
            Check(matrix.pointCounts==new Vector3Int(8,12,5)&&matrix.resolutionScale==2&&Vector3.Distance(matrix.spacing,new Vector3(.025f,.04f,.075f))<1e-5f,"redo did not restore all values together");
            var beforeAgain=matrix.AtlasTexture;apply.Invoke(panel,null);
            Check(!ReferenceEquals(matrix.AtlasTexture,beforeAgain),"explicit regenerate did not recreate same-size texture");
            return "PASS: drafts do not rebuild, invalid settings are rejected, apply rebuilds once, one undo/redo restores all settings, same-settings regenerate creates a new texture.";
        }
        finally
        {
            matrix.pointCounts=originalCount;matrix.spacing=originalSpacing;matrix.resolutionScale=originalResolution;
            matrix.RenderCurrentPose();matrix.DrawPreview();matrix.GetComponent<MatrixSpoutOutput>()?.Sync();panel.CreateGUI();
        }
    }
}
