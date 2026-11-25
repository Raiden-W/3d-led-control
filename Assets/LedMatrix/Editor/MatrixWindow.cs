using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.ShortcutManagement;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace LedMatrix.Editor
{
    public sealed class MatrixWindow : EditorWindow
    {
        [SerializeField] MatrixVolumeRenderer target;
        Image atlas;
        Label status,range;
        ObjectField modelField,sceneContentField,animationField;
        PopupField<string> contentField;
        Button removeContentButton,bakeButton;
        IntegerField targetFpsField;
        double last;
        double nextRender;
        int scheduledFps;
        double fpsTime;
        int fpsFrames;
        float measuredFps;
        Toggle sourceToggle;
        Toggle playToggle;
        Label dimensions;
        Vector3IntField countField;
        Vector3Field spacingField;
        EnumField patternField;
        IntegerField scaleField;
        Slider thicknessField,brightnessField;
        Toggle fillField,previewField,glowField;
        ColorField colorField;
        [SerializeField] MatrixVolumeRenderer draftTarget;
        [SerializeField] Vector3Int draftCounts;
        [SerializeField] Vector3 draftSpacingMm;
        [SerializeField] int draftResolution;
        [SerializeField] bool draftDirty;
        Label draftStatus;
        [MenuItem("Tools/LED Matrix/Control Panel")]
        public static void Open() { GetWindow<MatrixWindow>("LED Matrix").Show(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            if(target==null)target=FindFirstObjectByType<MatrixVolumeRenderer>();
            if(target==null) { atlas=null;rootVisualElement.Add(new Button(MatrixDemoSetup.Create){text="Create demo in current scene"}); return; }
            if(!Application.isPlaying && target.Contents.Count==0) DiscoverExistingContents();
            if(draftTarget!=target || !draftDirty)ResetDraft();
            var scroll=new ScrollView(); rootVisualElement.Add(scroll);
            dimensions=new Label { style={unityFontStyleAndWeight=FontStyle.Bold,marginBottom=8,whiteSpace=WhiteSpace.Normal} };scroll.Add(dimensions);
            var names=ContentNames();
            contentField=new PopupField<string>("当前内容",names,Mathf.Max(0,target.ActiveContentIndex));
            contentField.name="matrix-content-select";
            contentField.SetEnabled(target.Contents.Count>0);
            contentField.RegisterValueChangedCallback(e=>SwitchFromPanel(target.Contents.ToList().FindIndex(c=>c!=null && c.name==e.newValue)));
            scroll.Add(contentField);
            var input=new ObjectField("添加 FBX 模型") {objectType=typeof(GameObject),allowSceneObjects=false}; modelField=input;
            input.tooltip="添加新的场景实例，不替换已登记的内容。";
            input.RegisterValueChangedCallback(e=>{ if(e.newValue!=null) { AddModel((GameObject)e.newValue); CreateGUI(); } }); scroll.Add(input);
            var existing=new ObjectField("添加已有场景内容") {objectType=typeof(GameObject),allowSceneObjects=true};sceneContentField=existing;
            existing.tooltip="拖入场景中的摆放根对象；支持包含 VolumeSource 的自定义内容。";
            existing.RegisterValueChangedCallback(e=>{if(e.newValue!=null){AddSceneContent((GameObject)e.newValue);CreateGUI();}});scroll.Add(existing);
            removeContentButton=new Button(RemoveSelectedContent){text="从列表移除当前内容（保留场景对象）"};
            scroll.Add(removeContentButton);
            if(!Application.isPlaying && target.Contents.Any(c=>c==null || c.contentRoot==null))
                scroll.Add(new Button(RemoveMissingContents){text="清理已删除的内容引用"});
            var clipField=new ObjectField("Animation clip") {objectType=typeof(AnimationClip),value=target.clip}; animationField=clipField;
            clipField.RegisterValueChangedCallback(e=>{Undo.RecordObject(target,"Change clip");if(target.SetActiveContentClip((AnimationClip)e.newValue)){MatrixAnimationSetup.Assign(target);EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);}CreateGUI();}); scroll.Add(clipField);
            var controllerClip=DefaultControllerClip(target.animationRoot);
            if(controllerClip!=null && target.clip!=controllerClip)
            {
                scroll.Add(new HelpBox("预览 / 烘焙片段与 Animator 默认播放片段不一致；运行时播放："+controllerClip.name,HelpBoxMessageType.Warning));
                if(!Application.isPlaying)
                    scroll.Add(new Button(()=>{Undo.RecordObject(target,"同步动画片段");target.SetActiveContentClip(controllerClip);EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);CreateGUI();}){text="使用 Animator 默认动画片段"});
            }
            range=new Label(); scroll.Add(range); UpdateRange();
            playToggle=new Toggle("Playing（动画播放）") {value=target.playing,tooltip="编辑模式：控制 Unity Animation 预览；运行模式：暂停/恢复 Animator。"};
            playToggle.RegisterValueChangedCallback(e=>SetPlaying(e.newValue));scroll.Add(playToggle);
            scroll.Add(new Label("编辑：选择模型并使用 Unity Animation 窗口预览。运行：使用 Unity 顶部 Play / Pause。") {style={whiteSpace=WhiteSpace.Normal,marginTop=6,marginBottom=6}});
            scroll.Add(new Button(()=>{if(target.contentRoot!=null){MatrixAnimationWindowPin.PinBeforePlacement();Selection.activeGameObject=target.contentRoot.gameObject;}}){text="选择模型摆放（移动 / 旋转 / 缩放）"});
            scroll.Add(new Button(()=>{if(target.animationRoot!=null)Selection.activeGameObject=target.animationRoot.gameObject;EditorApplication.ExecuteMenuItem("Window/Animation/Animation");}){text="打开 Unity 动画时间轴"});
            if(MatrixAnimationWindowPin.Supported)
                scroll.Add(new Button(MatrixAnimationWindowPin.ReleaseByUser){text="解除动画时间轴自动锁定"});
            else
                scroll.Add(new HelpBox("选择模型摆放前，请在 Unity Animation 窗口右上角点击锁定图标，以保持当前动画时间。",HelpBoxMessageType.Info));
            sourceToggle=new Toggle("显示原始 FBX") {value=target.contentRoot!=null&&target.contentRoot.GetComponentInChildren<FbxVolumeSource>()?.showSourceMesh==true};
            sourceToggle.RegisterValueChangedCallback(e=>{var source=target.contentRoot!=null?target.contentRoot.GetComponentInChildren<FbxVolumeSource>():null;if(source!=null){Undo.RecordObject(source,"Toggle source mesh");source.showSourceMesh=e.newValue;EditorUtility.SetDirty(source);Render();}});scroll.Add(sourceToggle);
            countField=new Vector3IntField("灯点数量 X长 / Y高 / Z宽") {value=draftCounts};countField.RegisterValueChangedCallback(e=>{draftCounts=e.newValue;UpdateDraftState();});scroll.Add(countField);
            spacingField=new Vector3Field("灯点间距 X / Y / Z（mm）") {value=draftSpacingMm};spacingField.RegisterValueChangedCallback(e=>{draftSpacingMm=e.newValue;UpdateDraftState();});scroll.Add(spacingField);
            scaleField=new IntegerField("Resolution multiplier (1–8)"){value=draftResolution};scaleField.RegisterValueChangedCallback(e=>{draftResolution=e.newValue;UpdateDraftState();});scroll.Add(scaleField);
            scroll.Add(new Button(ApplyMatrixSettings){text="重新生成矩阵",name="regenerate-matrix"});
            draftStatus=new Label();draftStatus.style.whiteSpace=WhiteSpace.Normal;scroll.Add(draftStatus);UpdateDraftState();
            var fps=new IntegerField("Target output fps (1–120)"){value=target.outputFps}; targetFpsField=fps; fps.RegisterValueChangedCallback(e=>Change("输出帧率",()=>target.outputFps=Mathf.Clamp(e.newValue,1,120)));scroll.Add(fps);
            patternField=new EnumField("Input",target.pattern); patternField.RegisterValueChangedCallback(e=>Change("切换输入",()=>target.pattern=(MatrixVolumeRenderer.TestPattern)e.newValue)); scroll.Add(patternField);
            thicknessField=new Slider("Surface thickness (m)",0.005f,0.1f){value=target.surfaceThickness,showInputField=true}; thicknessField.RegisterValueChangedCallback(e=>Change("表面厚度",()=>target.surfaceThickness=e.newValue)); scroll.Add(thicknessField);
            fillField=new Toggle("Fill closed meshes"){value=target.fillInterior}; fillField.RegisterValueChangedCallback(e=>Change("内部填充",()=>target.fillInterior=e.newValue)); scroll.Add(fillField);
            colorField=new ColorField("Interior color"){value=target.interiorColor}; colorField.RegisterValueChangedCallback(e=>Change("填充颜色",()=>target.interiorColor=e.newValue)); scroll.Add(colorField);
            brightnessField=new Slider("Content brightness",0,1){value=target.brightness}; brightnessField.RegisterValueChangedCallback(e=>Change("内容亮度",()=>target.brightness=e.newValue)); scroll.Add(brightnessField);
            previewField=new Toggle("3D LED preview"){value=target.previewPoints}; previewField.RegisterValueChangedCallback(e=>Change("点阵预览",()=>target.previewPoints=e.newValue)); scroll.Add(previewField);
            glowField=new Toggle("辉光预览"){value=target.glowPreview,tooltip="只控制当前 Global Volume 的 Bloom 开关；强度、阈值和散射请在 Global Volume 中调整。"};glowField.name="glow-preview";
            glowField.RegisterValueChangedCallback(e=>
            {
                if(target==null || MatrixBaker.IsBaking)return;
                Undo.RecordObject(target,"辉光预览");
                if(!target.SetGlowPreview(e.newValue))glowField.SetValueWithoutNotify(target.glowPreview);
                else {EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);Render();}
            });
            scroll.Add(glowField);
            if(!target.GlowAvailable)scroll.Add(new HelpBox("当前场景没有可用的 Global Volume Bloom override。",HelpBoxMessageType.Warning));
            bakeButton=new Button(()=>{string folder=EditorUtility.OpenFolderPanel("Bake PNG sequence",Application.dataPath+"/..",""); if(folder.Length>0) {try{MatrixBaker.Begin(target,folder);}catch(Exception ex){Debug.LogException(ex);}}}){text="Bake entire FBX clip to PNG sequence"};scroll.Add(bakeButton);
            scroll.Add(new Button(MatrixBaker.Cancel){text="Cancel bake",name="cancelBake"});
            status=new Label(); scroll.Add(status);
            atlas=new Image {scaleMode=ScaleMode.ScaleToFit}; atlas.style.height=420; scroll.Add(atlas);
            last=EditorApplication.timeSinceStartup;
        }
        void OnEnable() { EditorApplication.update+=Tick; EditorApplication.playModeStateChanged+=OnPlayModeChanged; Undo.undoRedoPerformed+=RefreshAfterUndo; }
        void OnDisable() { EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=OnPlayModeChanged; Undo.undoRedoPerformed-=RefreshAfterUndo; }
        [Shortcut("LED Matrix/重做", typeof(MatrixWindow), KeyCode.Z, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void RedoShortcut(ShortcutArguments args)
        {
            var window=args.context as MatrixWindow;
            var focus=window?.rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
            for(var node=focus;node!=null;node=node.parent)
                if(node is TextField || node.ClassListContains("unity-base-text-field__input"))return;
            Undo.PerformRedo();
        }
        void RefreshAfterUndo() {if(target!=null){if(!draftDirty)ResetDraft();Render();SceneView.RepaintAll();}}
        void Change(string name,Action edit)
        {
            if(target==null || MatrixBaker.IsBaking)return;
            Undo.RecordObject(target,name);edit();EditorUtility.SetDirty(target);Render();
        }
        bool ValidSize(Vector3Int c,int resolution)
        {
            if(c.x<1||c.y<1||c.z<1||c.x>128||c.y>128||c.z>128)return false;
            int cols=Mathf.CeilToInt(Mathf.Sqrt(c.z)),rows=Mathf.CeilToInt((float)c.z/cols);
            long points=(long)c.x*c.y*c.z,voxels=points*resolution*resolution*resolution;
            return points<=524288 && voxels<=32000000 && (long)c.x*resolution*cols<=SystemInfo.maxTextureSize && (long)c.y*resolution*rows<=SystemInfo.maxTextureSize;
        }
        void ResetDraft()
        {
            draftTarget=target;draftCounts=target.Counts;draftSpacingMm=target.Spacing*1000;
            draftResolution=target.resolutionScale;draftDirty=false;
        }
        void UpdateDraftState()
        {
            if(target==null)return;
            draftDirty=draftCounts!=target.Counts || Vector3.Distance(draftSpacingMm,target.Spacing*1000)>0.0001f || draftResolution!=target.resolutionScale;
            if(draftStatus!=null)draftStatus.text=draftDirty?"参数已修改，点击“重新生成矩阵”后生效。":"当前参数已应用。";
        }
        void ApplyMatrixSettings()
        {
            if(target==null || MatrixBaker.IsBaking)return;
            var value=draftSpacingMm;
            if(float.IsNaN(value.x)||float.IsNaN(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.x)||float.IsInfinity(value.y)||float.IsInfinity(value.z)||value.x<1||value.y<1||value.z<1)
            {draftStatus.text="间距无效：各方向须为有限数值且不小于 1 mm。";return;}
            if(draftResolution<1||draftResolution>8||!ValidSize(draftCounts,draftResolution))
            {draftStatus.text="矩阵规模无效：请检查灯点数量、分辨率倍率和显卡纹理尺寸限制。";return;}
            bool changed=draftDirty;
            if(changed)Undo.RecordObject(target,"重新生成矩阵");
            target.pointCounts=draftCounts;target.spacing=value*0.001f;target.resolutionScale=draftResolution;
            if(changed)EditorUtility.SetDirty(target);
            target.Release();Render();target.DrawPreview();
            ResetDraft();UpdateDraftState();SceneView.RepaintAll();
        }
        void SetPlaying(bool value)
        {
            if(!Application.isPlaying)
            {
                var window=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault(w=>w.previewing && w.animationClip==target.clip);
                if(window==null && value && target.animationRoot!=null && target.clip!=null)
                {
                    Selection.activeGameObject=target.animationRoot.gameObject;
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                    EditorApplication.delayCall+=StartEditAnimation;
                    return;
                }
                if(window==null){playToggle.SetValueWithoutNotify(false);return;}
                window.playing=value;return;
            }
            Change("动画播放/暂停",()=>target.playing=value);
        }
        void StartEditAnimation()
        {
            if(this==null || target==null || Application.isPlaying)return;
            var window=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault(w=>w.canPreview && w.animationClip==target.clip);
            if(window==null){ShowNotification(new GUIContent("请先在 Animation 窗口选择当前模型动画"));return;}
            window.previewing=true;window.playing=true;
        }
        void OnPlayModeChanged(PlayModeStateChange state) { EditorApplication.delayCall+=Rebind; }
        void Rebind() { if(this!=null) CreateGUI(); }
        List<string> ContentNames()
        {
            var names=target.Contents.Select(c=>c!=null?c.name:"<缺失内容>").ToList();
            if(names.Count==0)names.Add("暂无内容");
            return names;
        }
        string UniqueName(string baseName)
        {
            if(string.IsNullOrWhiteSpace(baseName))baseName="内容";
            string name=baseName;int n=2;
            while(target.Contents.Any(c=>c!=null && c.name==name))name=baseName+" ("+n+++ ")";
            return name;
        }
        static AnimationClip FindClip(Transform root)
        {
            var animator=root!=null?root.GetComponentInChildren<Animator>(true):null;
            var defaultClip=DefaultControllerClip(animator!=null?animator.transform:null);
            if(defaultClip!=null)return defaultClip;
            if(animator!=null && animator.runtimeAnimatorController!=null)
            {
                var existing=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c!=null);
                if(existing!=null)return existing;
            }
            var assetRoot=animator!=null?animator.gameObject:root!=null?root.gameObject:null;
            string path=assetRoot!=null?PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(assetRoot):null;
            return string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
        }
        static AnimationClip DefaultControllerClip(Transform root)
        {
            var animator=root!=null?root.GetComponent<Animator>():null;
            if(animator!=null && animator.runtimeAnimatorController is AnimatorController controller && controller.layers.Length>0)
            {
                var state=controller.layers[0].stateMachine.defaultState;
                if(state!=null)return state.motion as AnimationClip;
            }
            return null;
        }
        static GameObject FindModelInstance(Transform root)
        {
            if(root==null)return null;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var instance=PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
                if(instance==null || (instance.transform!=root && !instance.transform.IsChildOf(root)))continue;
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                if(AssetImporter.GetAtPath(path) is ModelImporter)return instance;
            }
            return null;
        }
        static void EnsureReadable(Transform root)
        {
            if(root==null)return;
            var paths=new HashSet<string>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
                if(string.IsNullOrEmpty(path))continue;
                paths.Add(path);
            }
            foreach(string path in paths)
                if(AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
                {importer.isReadable=true;importer.SaveAndReimport();}
            foreach(var source in root.GetComponentsInChildren<FbxVolumeSource>(true))source.Rebuild();
        }
        void RecordContentUndo(string label)
        {
            var objects=new List<UnityEngine.Object>{target};
            foreach(var entry in target.Contents)
                if(entry?.contentRoot!=null)objects.Add(entry.contentRoot.gameObject);
            Undo.RecordObjects(objects.ToArray(),label);
        }
        void StopCurrentPreview()
        {
            if(Application.isPlaying)return;
            foreach(var window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
                if(window.previewing && window.animationClip==target.clip) {window.playing=false;window.previewing=false;}
            MatrixAnimationWindowPin.ReleaseByUser();
        }
        void SwitchFromPanel(int index)
        {
            if(MatrixBaker.IsBaking || index<0)return;
            if(!Application.isPlaying) {StopCurrentPreview();RecordContentUndo("切换点阵内容");}
            if(!target.SwitchContent(index)) {ShowNotification(new GUIContent("内容无效或正在烘焙，无法切换"));CreateGUI();return;}
            if(!Application.isPlaying)
            {
                EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                if(target.animationRoot!=null)Selection.activeGameObject=target.animationRoot.gameObject;
            }
            Render();CreateGUI();SceneView.RepaintAll();
        }
        void DiscoverExistingContents()
        {
            if(target==null || target.Contents.Count>0)return;
            var legacyRoot=target.contentRoot;
            var legacyAnimation=target.animationRoot;
            var legacyClip=target.clip;
            var roots=new List<Transform>();
            if(legacyRoot!=null && legacyRoot!=target.transform)roots.Add(legacyRoot);
            foreach(var source in target.GetComponentsInChildren<VolumeSource>(true))
            {
                var root=source.transform;
                while(root.parent!=null && root.parent!=target.transform)root=root.parent;
                if(root.parent!=target.transform || roots.Any(r=>r==root || r.IsChildOf(root) || root.IsChildOf(r)))continue;
                roots.Add(root);
            }
            if(roots.Count==0)return;
            foreach(var root in roots)EnsureReadable(root);
            var recorded=new List<UnityEngine.Object>{target};
            recorded.AddRange(roots.Select(r=>(UnityEngine.Object)r.gameObject));
            Undo.RecordObjects(recorded.ToArray(),"收集已有点阵内容");
            int selected=0;
            foreach(var root in roots)
            {
                var source=root.GetComponentInChildren<VolumeSource>(true);
                var animator=root.GetComponentInChildren<Animator>(true);
                var animRoot=root==legacyRoot && legacyAnimation!=null?legacyAnimation:animator!=null?animator.transform:source!=null?source.transform:root;
                var imported=PrefabUtility.GetCorrespondingObjectFromSource(animRoot.gameObject);
                var clip=root==legacyRoot && legacyClip!=null?legacyClip:FindClip(root);
                if(target.AddContent(UniqueName(imported!=null?imported.name:root.name),root,animRoot,clip) && (root==legacyRoot || (legacyRoot==null && root.gameObject.activeSelf)))
                    selected=target.Contents.Count-1;
            }
            if(target.Contents.Count==0)return;
            target.SwitchContent(selected);
            EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        }
        void AddModel(GameObject asset)
        {
            if(Application.isPlaying || MatrixBaker.IsBaking)return;
            string path=AssetDatabase.GetAssetPath(asset);
            if(!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {ShowNotification(new GUIContent("请选择导入的 FBX 模型资产"));return;}
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("添加点阵 FBX");
            var pivot=new GameObject("Content Placement · "+asset.name);pivot.transform.SetParent(target.transform,false);Undo.RegisterCreatedObjectUndo(pivot,"添加点阵 FBX");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            if(go==null){Undo.DestroyObjectImmediate(pivot);ShowNotification(new GUIContent("模型实例化失败"));return;}
            Undo.RegisterCreatedObjectUndo(go,"添加点阵 FBX");go.transform.SetParent(pivot.transform,false);
            Undo.AddComponent<FbxVolumeSource>(go);
            var modelClip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
            RecordContentUndo("添加点阵 FBX");
            if(!target.AddContent(UniqueName(asset.name),pivot.transform,go.transform,modelClip))
            {Undo.DestroyObjectImmediate(pivot);ShowNotification(new GUIContent("无法登记该模型"));return;}
            StopCurrentPreview();target.SwitchContent(target.Contents.Count-1);MatrixAnimationSetup.Assign(target);
            EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);Render();SceneView.RepaintAll();
        }
        void AddSceneContent(GameObject root)
        {
            if(Application.isPlaying || MatrixBaker.IsBaking)return;
            if(!root.scene.IsValid() || root.scene!=target.gameObject.scene)
            {ShowNotification(new GUIContent("请拖入当前场景中的对象"));return;}
            if(target.Contents.Any(c=>c!=null && c.contentRoot!=null && (root.transform==c.contentRoot || root.transform.IsChildOf(c.contentRoot) || c.contentRoot.IsChildOf(root.transform))))
            {ShowNotification(new GUIContent("该对象与已有内容重叠"));return;}
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("添加场景内容");
            var source=root.GetComponentInChildren<VolumeSource>(true);
            var animator=root.GetComponentInChildren<Animator>(true);
            var modelInstance=FindModelInstance(root.transform);
            if(source==null && root.GetComponentsInChildren<Renderer>(true).Any(r=>r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>()!=null))
                source=Undo.AddComponent<FbxVolumeSource>(animator!=null?animator.gameObject:modelInstance!=null?modelInstance:root);
            var animRoot=animator!=null?animator.transform:modelInstance!=null?modelInstance.transform:source!=null?source.transform:root.transform;
            var modelClip=FindClip(animRoot);
            var imported=PrefabUtility.GetCorrespondingObjectFromSource(animRoot.gameObject);
            EnsureReadable(root.transform);
            RecordContentUndo("添加场景内容");Undo.RecordObject(root,"添加场景内容");
            if(!target.AddContent(UniqueName(imported!=null?imported.name:root.name),root.transform,animRoot,modelClip))
            {ShowNotification(new GUIContent("无法登记此场景对象，请检查层级"));Undo.RevertAllDownToGroup(group);return;}
            StopCurrentPreview();target.SwitchContent(target.Contents.Count-1);
            if(modelClip!=null)MatrixAnimationSetup.Assign(target);
            EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);Render();SceneView.RepaintAll();
            if(source==null)ShowNotification(new GUIContent("已登记；需要 VolumeSource 适配器才能输出"));
        }
        void RemoveSelectedContent()
        {
            if(Application.isPlaying || MatrixBaker.IsBaking || target.ActiveContentIndex<0)return;
            StopCurrentPreview();RecordContentUndo("移除点阵内容");
            if(target.RemoveContent(target.ActiveContentIndex))
            {EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);Render();CreateGUI();SceneView.RepaintAll();}
        }
        void RemoveMissingContents()
        {
            if(Application.isPlaying || MatrixBaker.IsBaking)return;
            RecordContentUndo("清理已删除的内容引用");
            for(int i=target.Contents.Count-1;i>=0;i--)
                if(target.Contents[i]==null || target.Contents[i].contentRoot==null)target.RemoveContent(i);
            EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Render();CreateGUI();SceneView.RepaintAll();
        }
        void UpdateRange()
        {
            if(range==null) return;
            if(target.clip==null) {range.text="Choose a clip to enable baking";return;}
            range.text=$"动画时长 {target.clip.length:0.###} 秒 · {target.clip.frameRate:0.###} fps";
        }
        void Render() { if(target!=null && !MatrixBaker.IsBaking) {target.RenderCurrentPose(); target.GetComponent<MatrixSpoutOutput>()?.Sync();} }
        void Tick()
        {
            if(target==null) {if(atlas!=null)CreateGUI();return;}
            if(atlas==null)return;
            foreach(var control in rootVisualElement.Q<ScrollView>().contentContainer.Children())
                control.SetEnabled(!MatrixBaker.IsBaking || control.name=="cancelBake" || control==status || control==atlas);
            double now=EditorApplication.timeSinceStartup;
            if(scheduledFps!=target.outputFps) {nextRender=now;scheduledFps=target.outputFps;}
            if(now-fpsTime>=0.5)
            {
                measuredFps=fpsTime>0?(float)((target.FrameCount-fpsFrames)/(now-fpsTime)):0;
                fpsFrames=target.FrameCount;fpsTime=now;
            }
            if(Application.isPlaying || MatrixBaker.IsBaking) {last=now;nextRender=now;}
            atlas.image=target.GetComponent<MatrixSpoutOutput>()?.DisplayTexture;
            var names=ContentNames();
            if(!contentField.choices.SequenceEqual(names))contentField.choices=names;
            contentField.SetValueWithoutNotify(target.ActiveContent!=null?target.ActiveContent.name:names[0]);
            contentField.SetEnabled(!MatrixBaker.IsBaking && target.Contents.Count>0);
            modelField.SetEnabled(!MatrixBaker.IsBaking && !Application.isPlaying);
            sceneContentField.SetEnabled(!MatrixBaker.IsBaking && !Application.isPlaying);
            removeContentButton.SetEnabled(!MatrixBaker.IsBaking && !Application.isPlaying && target.ActiveContent!=null);
            animationField.SetEnabled(!MatrixBaker.IsBaking && !Application.isPlaying && target.ActiveContent!=null);
            bakeButton.SetEnabled(!MatrixBaker.IsBaking && !Application.isPlaying && target.clip!=null && target.contentRoot!=null);
            playToggle.SetEnabled(!MatrixBaker.IsBaking && target.clip!=null);
            animationField.SetValueWithoutNotify(target.clip);
            var animationWindow=Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault(w=>w.previewing && w.animationClip==target.clip);
            playToggle.SetValueWithoutNotify(Application.isPlaying?target.playing:animationWindow!=null&&animationWindow.playing);
            if(!draftDirty)
            {
                ResetDraft();countField.SetValueWithoutNotify(draftCounts);
                spacingField.SetValueWithoutNotify(draftSpacingMm);scaleField.SetValueWithoutNotify(draftResolution);
            }
            patternField.SetValueWithoutNotify(target.pattern);
            thicknessField.SetValueWithoutNotify(target.surfaceThickness);fillField.SetValueWithoutNotify(target.fillInterior);
            colorField.SetValueWithoutNotify(target.interiorColor);brightnessField.SetValueWithoutNotify(target.brightness);previewField.SetValueWithoutNotify(target.previewPoints);glowField.SetValueWithoutNotify(target.glowPreview);glowField.SetEnabled(!MatrixBaker.IsBaking && target.GlowAvailable);
            dimensions.text=$"{target.Counts.x} × {target.Counts.z} × {target.Counts.y}（长 × 宽 × 高）· {target.PointCount:N0} 点\n图集 {target.AtlasColumns} 列 × {target.AtlasRows} 行 · 每格 {target.TileWidth} × {target.TileHeight} 像素";
            if(sourceToggle!=null)sourceToggle.SetValueWithoutNotify(target.contentRoot!=null&&target.contentRoot.GetComponentInChildren<FbxVolumeSource>()?.showSourceMesh==true);
            targetFpsField.SetValueWithoutNotify(target.outputFps);
            UpdateRange();
            status.text=MatrixBaker.IsBaking?MatrixBaker.Status:$"{target.TileWidth*target.AtlasColumns} × {target.TileHeight*target.AtlasRows} · {(Application.isPlaying?"运行":"编辑预览")} {measuredFps:0.0} fps";
        }
    }
}
