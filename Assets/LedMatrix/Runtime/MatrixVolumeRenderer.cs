using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LedMatrix
{
    [ExecuteAlways]
    public sealed class MatrixVolumeRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class ContentEntry
        {
            public string name;
            public Transform contentRoot;
            public Transform animationRoot;
            public AnimationClip clip;
        }

        public enum TestPattern { Content, Coordinates, ScanX, ScanY, ScanZ, Red, Green, Blue, Gray }
        public ComputeShader voxelizer;
        public Shader pointShader;
        public Transform contentRoot;
        public Transform animationRoot;
        [Tooltip("灯点数量：X 长、Y 高、Z 宽")]
        public Vector3Int pointCounts = new Vector3Int(16,48,16);
        [Tooltip("X/Y/Z 灯点中心间距，单位米")]
        public Vector3 spacing = new Vector3(0.05f,0.05f,0.05f);
        [Range(1, 8)] public int resolutionScale = 4;
        [Range(0.005f, 0.1f)] public float surfaceThickness = 0.035f;
        public bool fillInterior;
        public Color interiorColor = new Color(0.1f, 0.3f, 1);
        [Range(0, 1)] public float brightness = 1;
        public bool previewPoints = true;
        [Tooltip("在 Unity 预览中启用当前 Global Volume 的 Bloom；Bloom 参数仍由 Volume Profile 控制")]
        public bool glowPreview;
        [SerializeField, HideInInspector] bool glowPreviewInitialized;
        [SerializeField, HideInInspector] bool glowOriginalVolumeActive = true;
        [SerializeField, HideInInspector] bool glowOriginalVolumeEnabled = true;
        [SerializeField, HideInInspector] Volume glowVolume;
        [Range(0.002f, 0.025f)] public float pointRadius = 0.007f;
        public TestPattern pattern;
        public bool playing = true;
        public AnimationClip clip;
        [SerializeField, HideInInspector] List<ContentEntry> contents = new List<ContentEntry>();
        [SerializeField, HideInInspector] int activeContentIndex = -1;
        public IReadOnlyList<ContentEntry> Contents => contents;
        public int ActiveContentIndex => activeContentIndex;
        public ContentEntry ActiveContent => activeContentIndex>=0 && activeContentIndex<contents.Count ? contents[activeContentIndex] : null;
        public bool ContentSwitchLocked { get; set; }
        public event Action<ContentEntry> ContentChanged;
        public float time;
        [Range(1, 120)] public int outputFps = 60;
        public RenderTexture AtlasTexture { get; private set; }
        public ComputeBuffer Volume { get; private set; }
        public Vector3Int Dimensions { get; private set; }
        public int FrameCount { get; private set; }
        public double LastRenderMs { get; private set; }
        public Vector3Int Counts => new Vector3Int(Mathf.Clamp(pointCounts.x,1,128),Mathf.Clamp(pointCounts.y,1,128),Mathf.Clamp(pointCounts.z,1,128));
        public Vector3 Spacing => new Vector3(Mathf.Max(0.001f,spacing.x),Mathf.Max(0.001f,spacing.y),Mathf.Max(0.001f,spacing.z));
        public Vector3 VolumeSize => Vector3.Scale((Vector3)Counts,Spacing);
        public Vector3 VolumeMin => new Vector3(-VolumeSize.x*0.5f,-Spacing.y*0.5f,-VolumeSize.z*0.5f);
        public int AtlasColumns => Mathf.CeilToInt(Mathf.Sqrt(Counts.z));
        public int AtlasRows => Mathf.CeilToInt((float)Counts.z/AtlasColumns);
        public int TileWidth => Counts.x * Mathf.Clamp(resolutionScale, 1, 8);
        public int TileHeight => Counts.y * Mathf.Clamp(resolutionScale, 1, 8);
        public int PointCount => Counts.x*Counts.y*Counts.z;
        public long VoxelCount => (long)PointCount*Mathf.Clamp(resolutionScale,1,8)*Mathf.Clamp(resolutionScale,1,8)*Mathf.Clamp(resolutionScale,1,8);
        Material points;
        GameObject previewObject;
        Mesh previewMesh;
        MeshRenderer previewRenderer;
        MaterialPropertyBlock properties;
        double lastTick;
        double nextOutput;
        int scheduledFps;
        int allocatedScale;
        Vector3Int allocatedCounts;
        Animator runtimeAnimator;
        float runtimeAnimatorSpeed;
        int clearKernel, surfaceKernel, fillKernel, atlasKernel, patternKernel;
        UnityEngine.Playables.PlayableGraph animationGraph;
        UnityEngine.Animations.AnimationClipPlayable clipPlayable;
        AnimationClip graphClip;
        Transform graphRoot;
        bool patternTimeOverride;
        public int SurfaceKernel => surfaceKernel;
        public int FillKernel => fillKernel;
        public bool GlowAvailable => TryFindGlowVolume(out var volume, out var bloom);

        // Content roots are independently owned scene objects. VolumeSource is the
        // common boundary for FBX and future non-mesh content adapters.
        public bool AddContent(string contentName, Transform root, Transform animatedRoot, AnimationClip animationClip)
        {
            if(ContentSwitchLocked || string.IsNullOrWhiteSpace(contentName) || root==null ||
               root==transform || transform.IsChildOf(root) || root.gameObject.scene!=gameObject.scene ||
               (animatedRoot!=null && animatedRoot!=root && !animatedRoot.IsChildOf(root))) return false;
            foreach(var item in contents)
                if(item==null || item.name==contentName || item.contentRoot==null || item.contentRoot==root ||
                   item.contentRoot.IsChildOf(root) || root.IsChildOf(item.contentRoot)) return false;
            contents.Add(new ContentEntry {name=contentName,contentRoot=root,animationRoot=animatedRoot,clip=animationClip});
            return true;
        }

        public bool SwitchContent(string contentName)
        {
            if(string.IsNullOrEmpty(contentName))return false;
            for(int i=0;i<contents.Count;i++)
                if(contents[i]!=null && string.Equals(contents[i].name,contentName,StringComparison.Ordinal)) return SwitchContent(i);
            return false;
        }

        public bool SwitchContent(int index)
        {
            if(ContentSwitchLocked || index<0 || index>=contents.Count) return false;
            var selected=contents[index];
            if(selected==null || selected.contentRoot==null || selected.contentRoot==transform ||
               transform.IsChildOf(selected.contentRoot) || selected.contentRoot.gameObject.scene!=gameObject.scene ||
               (selected.contentRoot.parent!=null && !selected.contentRoot.parent.gameObject.activeInHierarchy) ||
               (selected.animationRoot!=null && selected.animationRoot!=selected.contentRoot && !selected.animationRoot.IsChildOf(selected.contentRoot))) return false;
            // Reject broken or overlapping entries without changing the current selection.
            for(int i=0;i<contents.Count;i++)
            {
                var item=contents[i];
                if(item==null || item.contentRoot==null)continue;
                if(item.contentRoot==transform || transform.IsChildOf(item.contentRoot) ||
                   item.contentRoot.gameObject.scene!=gameObject.scene) return false;
                for(int j=0;j<i;j++)
                    if(contents[j]!=null && contents[j].contentRoot!=null &&
                       (item.contentRoot==contents[j].contentRoot || item.contentRoot.IsChildOf(contents[j].contentRoot) ||
                       contents[j].contentRoot.IsChildOf(item.contentRoot))) return false;
            }
            if(runtimeAnimator!=null) {runtimeAnimator.speed=runtimeAnimatorSpeed;runtimeAnimator=null;}
            if(animationGraph.IsValid())animationGraph.Destroy();
            graphClip=null;graphRoot=null;
            for(int i=0;i<contents.Count;i++)
                if(i!=index && contents[i]?.contentRoot!=null && contents[i].contentRoot.gameObject.activeSelf)contents[i].contentRoot.gameObject.SetActive(false);
            if(!selected.contentRoot.gameObject.activeSelf)selected.contentRoot.gameObject.SetActive(true);
            activeContentIndex=index;
            contentRoot=selected.contentRoot;animationRoot=selected.animationRoot;clip=selected.clip;
            time=0;pattern=TestPattern.Content;
            ContentChanged?.Invoke(selected);
            return true;
        }

        public bool SetActiveContentClip(AnimationClip animationClip)
        {
            if(ContentSwitchLocked || ActiveContent==null)return false;
            ActiveContent.clip=animationClip;clip=animationClip;return true;
        }

        public bool RemoveContent(int index)
        {
            if(ContentSwitchLocked || index<0 || index>=contents.Count)return false;
            if(index==activeContentIndex)
            {
                bool hasCandidate=false,switched=false;
                for(int i=0;i<contents.Count;i++)
                {
                    if(i==index || contents[i]?.contentRoot==null)continue;
                    hasCandidate=true;
                    if(SwitchContent(i)){switched=true;break;}
                }
                if(hasCandidate && !switched)return false;
                if(!switched)
                {
                    if(runtimeAnimator!=null){runtimeAnimator.speed=runtimeAnimatorSpeed;runtimeAnimator=null;}
                    if(animationGraph.IsValid())animationGraph.Destroy();
                    if(contents[index]?.contentRoot!=null)contents[index].contentRoot.gameObject.SetActive(false);
                    contentRoot=null;animationRoot=null;clip=null;activeContentIndex=-1;
                    ContentChanged?.Invoke(null);
                }
            }
            contents.RemoveAt(index);
            if(activeContentIndex>index)activeContentIndex--;
            return true;
        }

        void OnEnable()
        {
            lastTick = Clock;
            nextOutput=lastTick; scheduledFps=outputFps;
            if(glowPreviewInitialized)ApplyGlowPreview();
            if(Application.isPlaying && animationRoot!=null) {runtimeAnimator=animationRoot.GetComponent<Animator>();if(runtimeAnimator!=null)runtimeAnimatorSpeed=runtimeAnimator.speed;}
        }
        void OnDisable()
        {
            if(animationGraph.IsValid()) animationGraph.Destroy();
            if(runtimeAnimator!=null)runtimeAnimator.speed=runtimeAnimatorSpeed;
            Release();
        }
        double Clock => Application.isPlaying ? Time.realtimeSinceStartupAsDouble : EditorClock();
        static double EditorClock()
        {
            #if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
            #else
            return Time.realtimeSinceStartupAsDouble;
            #endif
        }
        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            var currentAnimator=animationRoot!=null?animationRoot.GetComponent<Animator>():null;
            if(currentAnimator!=runtimeAnimator)
            {
                if(runtimeAnimator!=null)runtimeAnimator.speed=runtimeAnimatorSpeed;
                runtimeAnimator=currentAnimator;
                if(runtimeAnimator!=null)runtimeAnimatorSpeed=runtimeAnimator.speed;
            }
            if(runtimeAnimator!=null)runtimeAnimator.speed=playing?runtimeAnimatorSpeed:0;
            double now = Clock;
            if(scheduledFps!=outputFps) {nextOutput=now;scheduledFps=outputFps;}
            if (now < nextOutput) { DrawPreview(); return; }
            lastTick = now;
            nextOutput+=1.0/Mathf.Clamp(outputFps,1,120);
            if(nextOutput<now-1.0/outputFps) nextOutput=now+1.0/outputFps;
            RenderCurrentPose();
            DrawPreview();
        }
        public void EnsureResources()
        {
            if (voxelizer == null) throw new InvalidOperationException("Assign MatrixVoxelize.compute first.");
            if (AtlasTexture != null && allocatedScale == resolutionScale && allocatedCounts==Counts) return;
            Release();
            if (!SystemInfo.supportsComputeShaders) throw new NotSupportedException("Compute shaders are required.");
            if(VoxelCount>32000000 || PointCount>524288 || TileWidth*AtlasColumns>SystemInfo.maxTextureSize || TileHeight*AtlasRows>SystemInfo.maxTextureSize)
                throw new InvalidOperationException("矩阵规模超过当前预览限制：最多 524288 点、3200 万体素，并受显卡纹理尺寸限制。请降低数量或分辨率倍率。");
            allocatedScale = resolutionScale;
            allocatedCounts=Counts;
            Dimensions = new Vector3Int(TileWidth, TileHeight, Counts.z * Mathf.Clamp(resolutionScale,1,8));
            Volume = new ComputeBuffer(Dimensions.x*Dimensions.y*Dimensions.z, sizeof(uint));
            AtlasTexture = new RenderTexture(TileWidth*AtlasColumns, TileHeight*AtlasRows, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name="3dLedMatrix_Atlas", enableRandomWrite=true, filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp, hideFlags=HideFlags.DontSave };
            AtlasTexture.Create();
            clearKernel=voxelizer.FindKernel("Clear"); surfaceKernel=voxelizer.FindKernel("Surface");
            fillKernel=voxelizer.FindKernel("Fill"); atlasKernel=voxelizer.FindKernel("Atlas"); patternKernel=voxelizer.FindKernel("Pattern");
            if (pointShader != null) points = new Material(pointShader) { hideFlags=HideFlags.DontSave };
            properties = new MaterialPropertyBlock();
            if(points!=null)
            {
                previewMesh=new Mesh {name="LED Matrix Preview",hideFlags=HideFlags.DontSave,indexFormat=IndexFormat.UInt32};
                int count=PointCount*6;
                var vertices=new Vector3[count];var indices=new int[count];
                for(int i=0;i<count;i++)indices[i]=i;
                previewMesh.vertices=vertices;previewMesh.SetIndices(indices,MeshTopology.Triangles,0,false);
                previewMesh.bounds=new Bounds(Vector3.zero,Vector3.one*10000);
                previewObject=new GameObject("LED Matrix Preview") {hideFlags=HideFlags.HideAndDontSave};
                previewObject.AddComponent<MeshFilter>().sharedMesh=previewMesh;
                previewRenderer=previewObject.AddComponent<MeshRenderer>();previewRenderer.sharedMaterial=points;
                previewRenderer.shadowCastingMode=ShadowCastingMode.Off;previewRenderer.receiveShadows=false;
            }
        }
        public void RenderAt(float seconds)
        {
            time=seconds;
            if (clip != null && contentRoot != null) SampleClip(seconds);
            patternTimeOverride=true;
            try { RenderCurrentPose(); }
            finally { patternTimeOverride=false; }
        }
        public void RenderCurrentPose()
        {
            double start=Clock;
            EnsureResources();
            voxelizer.SetInts("_Dims",Dimensions.x,Dimensions.y,Dimensions.z);
            voxelizer.SetInts("_PointCounts",Counts.x,Counts.y,Counts.z);
            voxelizer.SetInts("_AtlasGrid",AtlasColumns,AtlasRows);
            voxelizer.SetVector("_Min",VolumeMin); voxelizer.SetVector("_Size",VolumeSize);
            voxelizer.SetFloat("_Thickness",surfaceThickness);
            voxelizer.SetFloat("_Brightness",brightness);
            voxelizer.SetVector("_FillColor",interiorColor.linear);
            foreach (int kernel in new[]{clearKernel,surfaceKernel,fillKernel,atlasKernel,patternKernel}) voxelizer.SetBuffer(kernel,"_Volume",Volume);
            voxelizer.Dispatch(clearKernel,(Volume.count+255)/256,1,1);
            if (pattern == TestPattern.Content)
            {
                if (contentRoot != null)
                    foreach (VolumeSource source in contentRoot.GetComponentsInChildren<VolumeSource>())
                        if (source.isActiveAndEnabled) source.WriteVolume(this);
            }
            else
            {
                voxelizer.SetInt("_Pattern",(int)pattern); voxelizer.SetInt("_Frame",Mathf.FloorToInt((patternTimeOverride?time:(float)Clock)*8));
                voxelizer.Dispatch(patternKernel,(Dimensions.x+7)/8,(Dimensions.y+7)/8,Dimensions.z);
            }
            voxelizer.SetInts("_TileSize",TileWidth,TileHeight);
            voxelizer.SetTexture(atlasKernel,"_Atlas",AtlasTexture);
            voxelizer.Dispatch(atlasKernel,(AtlasTexture.width+7)/8,(AtlasTexture.height+7)/8,1);
            FrameCount++; LastRenderMs=(Clock-start)*1000;
        }
        void SampleClip(float seconds)
        {
            // A manual Playable evaluates generic and humanoid clips, including skinned meshes.
            var root=animationRoot!=null?animationRoot:contentRoot;
            if(!animationGraph.IsValid() || graphClip!=clip || graphRoot!=root)
            {
                if(animationGraph.IsValid()) animationGraph.Destroy();
                animationGraph=UnityEngine.Playables.PlayableGraph.Create("MatrixFrame");
                animationGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                var animator=root.GetComponent<Animator>();
                if (animator == null) animator=root.gameObject.AddComponent<Animator>();
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion=false;
                var output=UnityEngine.Animations.AnimationPlayableOutput.Create(animationGraph,"Animation",animator);
                clipPlayable=UnityEngine.Animations.AnimationClipPlayable.Create(animationGraph,clip);
                clipPlayable.SetApplyFootIK(false);
                UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,clipPlayable);
                animationGraph.Play(); graphClip=clip; graphRoot=root;
            }
            UnityEngine.Playables.PlayableExtensions.SetTime(clipPlayable,Math.Min(seconds,clip.length));
            animationGraph.Evaluate(0);
        }
        public void DrawPreview()
        {
            if (previewRenderer == null || points == null || AtlasTexture == null) return;
            previewRenderer.enabled=previewPoints;
            if(!previewPoints)return;
            properties.SetTexture("_Atlas",AtlasTexture);
            properties.SetMatrix("_MatrixToWorld",transform.localToWorldMatrix);
            properties.SetFloat("_PointSize",pointRadius*transform.lossyScale.x);
            properties.SetVector("_PointCounts",new Vector4(Counts.x,Counts.y,Counts.z,0));
            properties.SetVector("_Spacing",Spacing);
            properties.SetVector("_AtlasGrid",new Vector4(AtlasColumns,AtlasRows,0,0));
            properties.SetFloat("_GlowMultiplier",glowPreview?4f:1f);
            previewRenderer.SetPropertyBlock(properties);
        }
        public void Release()
        {
            Volume?.Release(); Volume=null;
            if(previewObject!=null){DestroyImmediate(previewObject);previewObject=null;previewRenderer=null;}
            if(previewMesh!=null){DestroyImmediate(previewMesh);previewMesh=null;}
            if (AtlasTexture != null) { AtlasTexture.Release(); DestroyImmediate(AtlasTexture); AtlasTexture=null; }
            if (points != null) { DestroyImmediate(points); points=null; }
        }

        bool TryFindGlowVolume(out Volume volume, out Bloom bloom)
        {
            volume=glowVolume;bloom=null;
            if(volume==null || volume.gameObject.scene!=gameObject.scene)
            {
                volume=null;
                var candidates=FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                foreach(var candidate in candidates)
                {
                    if(candidate.gameObject.scene!=gameObject.scene || !candidate.isGlobal)continue;
                    if(candidate.name.IndexOf("Global Volume",StringComparison.OrdinalIgnoreCase)>=0){volume=candidate;break;}
                    if(volume==null)volume=candidate;
                }
                glowVolume=volume;
            }
            if(volume==null)return false;
            var profile=Application.isPlaying?volume.profile:volume.sharedProfile;
            return profile!=null && profile.TryGet(out bloom);
        }

        public bool SetGlowPreview(bool enabled)
        {
            if(!TryFindGlowVolume(out var volume,out var bloom))return false;
            if(!glowPreviewInitialized)
            {
                glowOriginalVolumeActive=volume.gameObject.activeSelf;
                glowOriginalVolumeEnabled=volume.enabled;
            }
            glowPreview=enabled;glowPreviewInitialized=true;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(volume.gameObject,"LED Matrix Glow Preview");
            if(!Application.isPlaying && volume.sharedProfile!=null)UnityEditor.Undo.RecordObject(volume.sharedProfile,"LED Matrix Glow Preview Bloom");
#endif
            ApplyGlowPreview(volume,bloom);
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(volume.gameObject);
                if(volume.sharedProfile!=null)UnityEditor.EditorUtility.SetDirty(volume.sharedProfile);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
            }
#endif
            return true;
        }

        void ApplyGlowPreview()
        {
            if(TryFindGlowVolume(out var volume,out var bloom))ApplyGlowPreview(volume,bloom);
        }

        void ApplyGlowPreview(Volume volume,Bloom bloom)
        {
            bloom.active=glowPreview;
            if(glowPreview)
            {
                volume.gameObject.SetActive(true);volume.enabled=true;
            }
            else
            {
                volume.enabled=glowOriginalVolumeEnabled;
                volume.gameObject.SetActive(glowOriginalVolumeActive);
            }
        }
    }
}
