using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Unity.Profiling;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    // Opt-in Player measurements; never installed in ordinary play.
    public sealed class WM15SnowPerformancePlayer : MonoBehaviour
    {
        private string output;
        private float deadline;
        private readonly List<string> errors = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-wm15SnowPerformance");
            if(index<0||index+1>=args.Length)return;
            var host=new GameObject("Snow performance acceptance");DontDestroyOnLoad(host);
            var test=host.AddComponent<WM15SnowPerformancePlayer>();test.output=Path.GetFullPath(args[index+1]);
            Directory.CreateDirectory(test.output);Application.runInBackground=true;
            test.deadline=Time.realtimeSinceStartup+900;
            Application.logMessageReceived+=test.OnLog;test.StartCoroutine(test.Run());
        }
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        private void Update(){if(output!=null&&Time.realtimeSinceStartup>deadline)Finish(2);}
        private void Finish(int code)
        {Application.logMessageReceived-=OnLog;File.WriteAllLines(output+"/errors.txt",errors);Application.Quit(code);enabled=false;}
        private IEnumerator Run()
        {
            MainMenuController menu;while((menu=FindAnyObjectByType<MainMenuController>())==null)yield return null;
            menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;while((root=FindAnyObjectByType<LocalSceneCompositionRoot>())==null||!root.IsInitialized)yield return null;
            root.Clock.Pause();root.GameplayUi.SetOnboardingVisible(false);
            var hut=root.Buildings.All.First(x=>x.IsOperational&&x.OwnerSideId.Value=="player"&&x.DefinitionId.FamilyId.ToString()=="stone_age_hut");
            root.BuildingPresentations.TryGet(hut.Id,out var view);root.CameraController.FocusPosition(view.transform.position,16);
            yield return new WaitForSecondsRealtime(2);Capture("hut");
            while(!root.TiledWorld.BackgroundWarmupComplete)yield return null;
            var snow=root.GetComponent<DeformableSnowPresentation>();var map=root.TiledWorld.ArtWorldToDesign;
            var point=Vector3.zero;var found=false;
            for(var z=700;z<=850&&!found;z+=5)for(var x=770;x<=950&&!found;x+=5)
            {var p=new Vector3((x-map.x)/map.z,0,(z-map.y)/map.w);p.y=root.WorldHeight.SampleHeight(p.x,p.z);
                if(!snow.IsTrackableSnow(p)||!snow.IsTrackableSnow(p+Vector3.forward*18))continue;point=p;found=true;}
            if(!found){errors.Add("No snow measurement point");Finish(2);yield break;}
            root.TiledWorld.RefreshNow(new WorldPosition(point.x,point.y,point.z),Array.Empty<WorldPosition>(),Array.Empty<Tuple<WorldPosition,WorldPosition>>());
            snow.QueueAround(point,2);root.CameraController.FocusPosition(point,16);
            yield return new WaitForSecondsRealtime(5);
            root.CameraController.enabled=false;var camera=Camera.main;
            foreach(var component in camera.GetComponents<Behaviour>())
                if(component.GetType().FullName=="Unity.Cinemachine.CinemachineBrain")component.enabled=false;
            camera.transform.SetPositionAndRotation(point+new Vector3(7,6,-6),Quaternion.LookRotation(point+Vector3.forward*5-(point+new Vector3(7,6,-6))));
            yield return new WaitForSecondsRealtime(3);Capture("snow-close");
            File.WriteAllText(output+"/frames.txt",SystemInfo.graphicsDeviceName+" "+Screen.width+"x"+Screen.height+" vsync="+QualitySettings.vSyncCount+" target="+Application.targetFrameRate+"\n");
            yield return Measure("close snow paused");root.Clock.Resume();yield return Measure("close snow running");root.Clock.Pause();
            var renderers=snow.GetComponentsInChildren<MeshRenderer>();foreach(var renderer in renderers)renderer.enabled=false;
            yield return Measure("diagnostic without snow overlay");foreach(var renderer in renderers)renderer.enabled=true;
            Finish(errors.Count==0?0:2);
        }
        private IEnumerator Measure(string label)
        {
            using var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
            using var render=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread",1);
            using var wait=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Gfx.WaitForPresentOnGfxThread",1);
            var frames=new List<float>();double mainMs=0,renderMs=0,waitMs=0;
            for(var i=0;i<180;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);mainMs+=main.LastValue/1e6;renderMs+=render.LastValue/1e6;waitMs+=wait.LastValue/1e6;}
            frames.Sort();File.AppendAllText(output+"/frames.txt",label+": mean="+frames.Average()+" p95="+frames[171]+" max="+frames.Last()+" main="+mainMs/180+" render="+renderMs/180+" presentWait="+waitMs/180+" ms\n");
        }
        private void Capture(string name)
        {
            var camera=Camera.main;var target=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(output+"/"+name+".png",texture.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;Destroy(texture);RenderTexture.ReleaseTemporary(target);}
        }
    }
}
