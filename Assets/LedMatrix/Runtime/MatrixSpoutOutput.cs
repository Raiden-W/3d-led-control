using UnityEngine;
using Klak.Spout;

namespace LedMatrix
{
    [ExecuteAlways]
    [RequireComponent(typeof(MatrixVolumeRenderer))]
    public sealed class MatrixSpoutOutput : MonoBehaviour
    {
        public string senderName="3dLedMatrix_Atlas";
        public SpoutResources resources;
        SpoutSender sender;
        MatrixVolumeRenderer matrix;
        RenderTexture encodedView;
        public RenderTexture DisplayTexture => encodedView;
        void OnEnable() { matrix=GetComponent<MatrixVolumeRenderer>(); }
        void OnDisable()
        {
            if(sender!=null) { sender.enabled=false; sender.sourceTexture=null; }
            ReleaseView();
        }
        void ReleaseView()
        {
            if(encodedView!=null) { encodedView.Release(); DestroyImmediate(encodedView); encodedView=null; }
        }
        public void Sync()
        {
            if(matrix==null) matrix=GetComponent<MatrixVolumeRenderer>();
            var atlas=matrix.AtlasTexture; if(atlas==null) return;
            if(encodedView==null || encodedView.width!=atlas.width || encodedView.height!=atlas.height)
            {
                ReleaseView();
                encodedView=new RenderTexture(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB)
                { name="MatrixAtlas_sRGBView",filterMode=FilterMode.Point,hideFlags=HideFlags.DontSave };
                encodedView.Create();
            }
            // Bit copy gives encoded bytes an sRGB view, avoiding double gamma in Spout's blit.
            Graphics.CopyTexture(atlas,encodedView);
            if(sender==null) sender=GetComponent<SpoutSender>();
            if(sender==null) sender=gameObject.AddComponent<SpoutSender>();
            sender.SetResources(resources); sender.captureMethod=CaptureMethod.Texture;
            sender.spoutName=senderName; sender.keepAlpha=true; sender.sourceTexture=encodedView;
            sender.enabled=enabled && resources!=null;
        }
        void LateUpdate() { Sync(); }
    }
}
