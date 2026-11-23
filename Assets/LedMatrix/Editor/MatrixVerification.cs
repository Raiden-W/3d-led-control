using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace LedMatrix.Editor
{
    public static class MatrixVerification
    {
        static void Require(bool value,string message) { if(!value) throw new Exception("Verification: "+message); }
        public static byte[] Read(MatrixVolumeRenderer matrix)
        {
            var request=AsyncGPUReadback.Request(matrix.AtlasTexture,0,TextureFormat.RGBA32);
            request.WaitForCompletion(); Require(!request.hasError,"GPU readback"); return request.GetData<byte>().ToArray();
        }
        static int Lit(byte[] bytes) {int n=0;for(int i=0;i<bytes.Length;i+=4)if(bytes[i]+bytes[i+1]+bytes[i+2]>0)n++;return n;}
        public static string Run()
        {
            var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
            var pattern=m.pattern; bool fill=m.fillInterior,playing=m.playing; float time=m.time;
            Vector3 placement=m.contentRoot.localPosition;
            try
            {
                m.playing=false;m.fillInterior=false;
                m.pattern=MatrixVolumeRenderer.TestPattern.Red;m.RenderAt(0);
                var red=Read(m);Require(red[0]==255&&red[1]==0&&red[2]==0&&red[3]==255,"red channel/alpha");
                m.pattern=MatrixVolumeRenderer.TestPattern.Gray;m.RenderAt(0);
                var gray=Read(m);Require(Math.Abs(gray[0]-188)<=1,"linear 0.5 must encode to sRGB 188");
                m.pattern=MatrixVolumeRenderer.TestPattern.ScanZ;
                for(int layer=0;layer<16;layer++)
                {
                    m.RenderAt(layer/8f);var pixels=Read(m);int w=m.AtlasTexture.width;
                    for(int tile=0;tile<16;tile++)
                    {
                        int x=(tile%4)*m.TileWidth+m.TileWidth/2,y=(3-tile/4)*m.TileHeight+m.TileHeight/2;
                        int p=(y*w+x)*4;bool lit=pixels[p]+pixels[p+1]+pixels[p+2]>0;
                        Require(lit==(tile==layer),"layer order "+layer+" at tile "+tile);
                    }
                }
                m.pattern=MatrixVolumeRenderer.TestPattern.Content;m.RenderAt(0.5f);var a=Read(m);
                Require(Lit(a)>0,"FBX surface must produce colors");
                m.RenderAt(1);var b=Read(m);Require(!a.SequenceEqual(b),"FBX animation must change slices");
                m.RenderAt(0.5f);Require(a.SequenceEqual(Read(m)),"absolute animation time must repeat deterministically");
                m.fillInterior=true;m.RenderAt(0.5f);int filled=Lit(Read(m));Require(filled>Lit(a),"closed mesh interior must fill");
                m.contentRoot.localPosition=placement+Vector3.right*5;m.RenderAt(0.5f);Require(Lit(Read(m))==0,"clear vacated volume");
                m.contentRoot.localPosition=placement;m.fillInterior=false;m.RenderAt(0.5f);
                Directory.CreateDirectory("Verification");
                File.WriteAllBytes("Verification/atlas.png",ImageConversion.EncodeArrayToPNG(a,UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,(uint)m.AtlasTexture.width,(uint)m.AtlasTexture.height));
                string result=$"PASS: RGB/alpha, sRGB gray, 16 depth layers, FBX animation, deterministic evaluation, closed-mesh fill, vacated-volume clearing. Surface pixels={Lit(a)}; filled pixels={filled}.";
                File.WriteAllText("Verification/checks.txt",result); return result;
            }
            finally {m.contentRoot.localPosition=placement;m.pattern=pattern;m.fillInterior=fill;m.playing=playing;m.RenderAt(time);}
        }
        public static string VerifyBake(string directory)
        {
            var manifest=JsonUtility.FromJson<MatrixBaker.Manifest>(File.ReadAllText(Path.Combine(directory,"manifest.json")));
            Require(manifest.complete&&manifest.completedFrames==manifest.expectedFrames,"completed bake manifest");
            Require(Directory.GetFiles(directory,"frame_*.png").Length==manifest.expectedFrames,"sequence count");
            var m=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
            m.playing=false;m.RenderAt(15/manifest.fps);
            var png=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                png.LoadImage(File.ReadAllBytes(Path.Combine(directory,"frame_000015.png")));
                var live=Read(m);var pixels=png.GetPixels32();
                for(int i=0;i<pixels.Length;i++)
                {
                    var p=pixels[i];
                    Require(live[i*4]==p.r&&live[i*4+1]==p.g&&live[i*4+2]==p.b&&live[i*4+3]==p.a,"baked frame must equal live atlas byte-for-byte");
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(png);}
            return "PASS: sequence count, manifest completion, live/baked frame byte equality";
        }
    }
}
