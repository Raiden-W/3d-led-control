using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LedMatrix;
using LedMatrix.Editor;

public static class VerifyUndoKeys
{
    public static async Task<string> Run()
    {
        var matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        MatrixWindow.Open();var panel=Resources.FindObjectsOfTypeAll<MatrixWindow>().First();panel.CreateGUI();
        var field=panel.rootVisualElement.Query<Vector3IntField>().First();
        var original=matrix.pointCounts;
        try
        {
            panel.Focus();panel.rootVisualElement.Focus();
            await Task.Delay(100);
            Undo.IncrementCurrentGroup();field.value=new Vector3Int(8,12,5);
            typeof(MatrixWindow).GetMethod("ApplyMatrixSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);Undo.FlushUndoRecordObjects();
            panel.SendEvent(new Event {type=EventType.KeyDown,keyCode=KeyCode.Z,modifiers=EventModifiers.Control});
            await Task.Delay(150);
            var afterUndo=matrix.pointCounts;string nameAfterUndo=Undo.GetCurrentGroupName();
            panel.SendEvent(new Event {type=EventType.KeyDown,keyCode=KeyCode.Z,modifiers=EventModifiers.Control|EventModifiers.Shift});
            await Task.Delay(150);
            if(matrix.pointCounts!=new Vector3Int(8,12,5))throw new Exception("After Undo="+afterUndo+" undo group="+nameAfterUndo+" after redo="+matrix.pointCounts+" group="+Undo.GetCurrentGroupName());
            return "PASS: Ctrl+Z and Ctrl+Shift+Z routed by focused Matrix panel";
        }
        finally {matrix.pointCounts=original;matrix.RenderCurrentPose();}
    }
}
