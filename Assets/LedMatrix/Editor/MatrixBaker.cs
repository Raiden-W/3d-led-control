using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LedMatrix.Editor
{
    public static class MatrixBaker
    {
        [Serializable] public sealed class Manifest
        {
            public string source,clip,colorEncoding="sRGB encoded RGB8; alpha=255",layout="4x4; top-left to bottom-right = Z 0..15";
            public string pixelDirection="X increases right; Y increases up";
            public int firstSourceFrame,lastSourceFrame,expectedFrames,completedFrames,width,height,resolutionScale,atlasColumns,atlasRows;
            public float fps,surfaceThickness,brightness;
            public Vector3Int pointCounts;
            public Vector3 spacingMetres;
            public bool complete,fillInterior;
            public Vector3 volumeMin,volumeSize;
            public Matrix4x4 contentLocalToMatrix;
        }
        static MatrixVolumeRenderer target;
        static AsyncGPUReadbackRequest request;
        static bool waiting,cancel;
        static int index;
        static string directory;
        static Manifest manifest;
        static float originalTime;
        static bool originalPlaying;
        static MatrixVolumeRenderer.TestPattern originalPattern;
        sealed class WindowPreview
        {
            public AnimationWindow window;
            public AnimationClip clip;
            public float time;
            public bool playing;
        }
        static WindowPreview[] previousPreviews;
        static UnityEngine.Object originalSelection;
        static bool ownsAnimationMode;
        public static bool IsBaking => target!=null;
        public static string Status { get; private set; }="Ready";
        public static event Action Finished;

        public static Vector2Int SourceRange(AnimationClip clip)
        {
            if(clip==null) return Vector2Int.zero;
            var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as ModelImporter;
            if(importer!=null)
            {
                var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
                foreach(var c in clips) if(c.name==clip.name)
                {
                    int start=Mathf.RoundToInt(c.firstFrame);
                    int available=Mathf.Max(0,Mathf.RoundToInt(clip.length*clip.frameRate));
                    return new Vector2Int(start,Mathf.Min(Mathf.RoundToInt(c.lastFrame),start+available));
                }
            }
            return new Vector2Int(0,Mathf.RoundToInt(clip.length*clip.frameRate));
        }
        public static void Begin(MatrixVolumeRenderer renderer,string folder)
        {
            if(IsBaking) throw new InvalidOperationException("A bake is already active.");
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Bake in Edit Mode.");
            if(renderer.clip==null || renderer.contentRoot==null) throw new InvalidOperationException("Select an FBX and animation clip.");
            if(renderer.clip.frameRate<=0)throw new InvalidOperationException("Invalid source frame rate.");
            var range=SourceRange(renderer.clip);
            // A separate run folder prevents old frames from contaminating a shorter bake.
            directory=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")); Directory.CreateDirectory(directory);
            originalSelection=Selection.activeObject;
            previousPreviews=Resources.FindObjectsOfTypeAll<AnimationWindow>().Where(w=>w.previewing)
                .Select(w=>new WindowPreview {window=w,clip=w.animationClip,time=w.time,playing=w.playing}).ToArray();
            foreach(var p in previousPreviews){p.window.playing=false;p.window.previewing=false;}
            if(AnimationMode.InAnimationMode())
            {
                RestoreWindows(renderer);
                throw new InvalidOperationException("其他编辑器工具正在预览动画，请先结束该预览再烘焙。");
            }
            try { AnimationMode.StartAnimationMode();ownsAnimationMode=true; }
            catch { RestoreWindows(renderer);throw; }
            target=renderer; originalTime=renderer.time; originalPlaying=renderer.playing; originalPattern=renderer.pattern;
            renderer.playing=false; renderer.pattern=MatrixVolumeRenderer.TestPattern.Content;
            manifest=new Manifest {source=AssetDatabase.GetAssetPath(renderer.clip),clip=renderer.clip.name,
                firstSourceFrame=range.x,lastSourceFrame=range.y,expectedFrames=range.y-range.x+1,
                fps=renderer.clip.frameRate,width=renderer.TileWidth*renderer.AtlasColumns,height=renderer.TileHeight*renderer.AtlasRows,
                pointCounts=renderer.Counts,spacingMetres=renderer.Spacing,atlasColumns=renderer.AtlasColumns,atlasRows=renderer.AtlasRows,
                layout=$"{renderer.AtlasColumns}x{renderer.AtlasRows}; top-left to bottom-right = Z 0..{renderer.Counts.z-1}",
                resolutionScale=renderer.resolutionScale,surfaceThickness=renderer.surfaceThickness,
                brightness=renderer.brightness,fillInterior=renderer.fillInterior,volumeMin=renderer.VolumeMin,
                volumeSize=renderer.VolumeSize,contentLocalToMatrix=renderer.transform.worldToLocalMatrix*renderer.contentRoot.localToWorldMatrix};
            if(manifest.fps<=0 || manifest.expectedFrames<=0) { EndAnimationSampling();target=null;throw new InvalidOperationException("Invalid source frame range."); }
            index=0; waiting=false; cancel=false;
            try { SaveManifest(); }
            catch
            {
                EndAnimationSampling();renderer.playing=originalPlaying;renderer.pattern=originalPattern;
                target=null;throw;
            }
            renderer.ContentSwitchLocked=true;
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
            EditorApplication.update+=Tick;
        }
        public static void Cancel() { cancel=true; }
        static void SaveManifest() { File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(manifest,true)); }
        static void Tick()
        {
            if(target==null) { Stop(false,"Target was removed"); return; }
            try
            {
                if(waiting)
                {
                    if(!request.done) return;
                    waiting=false;
                    if(request.hasError) throw new IOException("GPU readback failed at frame "+index);
                    byte[] png=ImageConversion.EncodeArrayToPNG(request.GetData<byte>().ToArray(),UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,(uint)manifest.width,(uint)manifest.height);
                    File.WriteAllBytes(Path.Combine(directory,$"frame_{index:D6}.png"),png);
                    index++; manifest.completedFrames=index;
                }
                if(cancel) { Stop(false,"Cancelled"); return; }
                if(index>=manifest.expectedFrames) { Stop(true,"Complete"); return; }
                Status=$"烘焙 · 已保存 {index}/{manifest.expectedFrames} 帧";
                cancel=EditorUtility.DisplayCancelableProgressBar("LED Matrix Bake",Status,(float)index/manifest.expectedFrames);
                if(cancel) return;
                if(!AnimationMode.InAnimationMode())throw new InvalidOperationException("烘焙期间动画预览被其他操作中断，请重新开始烘焙。");
                AnimationMode.BeginSampling();
                try { AnimationMode.SampleAnimationClip((target.animationRoot!=null?target.animationRoot:target.contentRoot).gameObject,target.clip,index/manifest.fps); }
                finally { AnimationMode.EndSampling(); }
                target.RenderCurrentPose();
                target.DrawPreview();SceneView.RepaintAll();
                request=AsyncGPUReadback.Request(target.AtlasTexture,0,TextureFormat.RGBA32); waiting=true;
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch(Exception e) { Debug.LogException(e); Stop(false,e.Message); }
        }
        static void BeforeReload()
        {
            if(waiting) { request.WaitForCompletion(); }
            Stop(false,"Interrupted by script reload");
        }
        static void Stop(bool success,string message)
        {
            EditorApplication.update-=Tick; AssemblyReloadEvents.beforeAssemblyReload-=BeforeReload;
            EditorUtility.ClearProgressBar();
            var finishedTarget=target;
            try
            {
                if(manifest!=null) { manifest.complete=success; SaveManifest(); }
            }
            catch(Exception e) { success=false;message="无法保存烘焙状态："+e.Message;Debug.LogException(e); }
            try { EndAnimationSampling(); }
            finally
            {
                if(finishedTarget!=null)
                {
                    finishedTarget.ContentSwitchLocked=false;
                    finishedTarget.playing=originalPlaying; finishedTarget.pattern=originalPattern; finishedTarget.time=originalTime;
                }
                target=null;waiting=false;
            }
            if(finishedTarget!=null)finishedTarget.RenderCurrentPose();
            Status=message+": "+directory;
            Debug.Log("[LED Bake] "+Status); Finished?.Invoke();
        }
        static void EndAnimationSampling()
        {
            if(ownsAnimationMode && AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
            ownsAnimationMode=false;
            RestoreWindows(target);
        }
        static void RestoreWindows(MatrixVolumeRenderer renderer)
        {
            if(previousPreviews==null)return;
            foreach(var p in previousPreviews)
            {
                if(p.window==null || p.clip==null)continue;
                if(!p.window.canPreview && renderer!=null && renderer.animationRoot!=null)
                {
                    Selection.activeGameObject=renderer.animationRoot.gameObject;
                    var method=typeof(AnimationWindow).GetMethod("OnSelectionChange",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                    method?.Invoke(p.window,null);
                }
                if(p.window.canPreview)
                {
                    p.window.animationClip=p.clip;p.window.time=p.time;p.window.previewing=true;p.window.playing=p.playing;
                    MatrixAnimationWindowPin.PinBeforePlacement();
                }
            }
            if(originalSelection!=null)Selection.activeObject=originalSelection;
            previousPreviews=null;originalSelection=null;
        }
    }
}
