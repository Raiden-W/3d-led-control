using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using LedMatrix;
using LedMatrix.Editor;
using Klak.Spout;
public static class VerifyRegenerateRuntime
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
        var oldCounts=m.pointCounts;var oldSpacing=m.spacing;var oldResolution=m.resolutionScale;var oldFill=m.fillInterior;
        MatrixWindow.Open();var panel=Resources.FindObjectsOfTypeAll<MatrixWindow>().First();panel.CreateGUI();
        try
        {
            m.fillInterior=false;
            var atlas=m.AtlasTexture;
            panel.rootVisualElement.Query<Vector3IntField>().First().value=new Vector3Int(8,12,5);
            await Task.Delay(400);
            if(m.pointCounts!=oldCounts||!ReferenceEquals(atlas,m.AtlasTexture))throw new Exception("Draft changed running output");
            typeof(MatrixWindow).GetMethod("ApplyMatrixSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
            await Task.Delay(900);
            var sender=m.GetComponent<SpoutSender>();
            var buffer=(RenderTexture)typeof(SpoutSender).GetField("_buffer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sender);
            if(m.AtlasTexture.width!=96||m.AtlasTexture.height!=96||buffer==null||buffer.width!=96||buffer.height!=96)
                throw new Exception("Running Spout did not resize to 96x96");
            return "PASS: running Spout unchanged while editing draft, switched to 96×96 only after Apply";
        }
        finally
        {
            m.pointCounts=oldCounts;m.spacing=oldSpacing;m.resolutionScale=oldResolution;m.fillInterior=oldFill;
            m.RenderCurrentPose();m.DrawPreview();m.GetComponent<MatrixSpoutOutput>()?.Sync();panel.CreateGUI();
        }
    }
}
