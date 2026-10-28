#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Domain.AI;

namespace Game.Infrastructure.Editor
{
    /// <summary>One-shot ordinary-lake and marsh art review. It does not execute tests or build a Player.</summary>
    [InitializeOnLoad]
    public static class WM15EnvironmentReviewCapture
    {
        private const string ActiveKey="WM15.EnvironmentReview.Active";
        private const string StartedKey="WM15.EnvironmentReview.Started";
        private const string OutputKey="WM15.EnvironmentReview.Output";
        private static int readyFrames;
        private static bool terrainReady;
        static WM15EnvironmentReviewCapture()
        {
            EditorApplication.update+=Tick;
            TiledTerrainWorld.EditorTerrainReady+=OnTerrainReady;
        }

        public static void Begin()
        {
            var args=Environment.GetCommandLineArgs();
            var index=Array.IndexOf(args,"-wm15EnvironmentPreview");
            if(index<0||index+1>=args.Length)
                throw new ArgumentException("-wm15EnvironmentPreview requires an output directory.");
            SessionState.SetString(OutputKey,Path.GetFullPath(args[index+1]));
            SessionState.SetBool(ActiveKey,true);SessionState.SetBool(StartedKey,false);
            terrainReady=false;readyFrames=0;TiledTerrainWorld.EditorReviewCapturePending=true;
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
            EditorApplication.isPlaying=true;
        }

        private static void Tick()
        {
            if(!SessionState.GetBool(ActiveKey,false)||!EditorApplication.isPlaying)return;
            if(!SessionState.GetBool(StartedKey,false))
            {
                var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                if(menu==null)return;
                SessionState.SetBool(StartedKey,true);menu.StartNewGame(1380062136);return;
            }
            if(!terrainReady)return;
            var root=UnityEngine.Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            // Environment review only needs the completed tiled-world projection.  Do not make art
            // capture depend on later gameplay catalogs that are outside this visual task.
            if(root==null||root.TiledWorld==null||root.WorldHeight==null)return;
            if(++readyFrames<60)return;
            try{RenderAll(root,SessionState.GetString(OutputKey,string.Empty));}
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(2);}
        }

        private static void OnTerrainReady(TiledTerrainWorld world)
        {
            if(!SessionState.GetBool(ActiveKey,false)||!EditorApplication.isPlaying||world==null)return;
            terrainReady=true;
            readyFrames=0;
        }

        private static void RenderAll(LocalSceneCompositionRoot root,string outputDirectory)
        {
            SessionState.SetBool(ActiveKey,false);
            TiledTerrainWorld.EditorReviewCapturePending=false;
            root.GameplayUi?.SetOnboardingVisible(false);
            if(root.CameraController!=null)root.CameraController.enabled=false;
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                        FindObjectsSortMode.None))canvas.enabled=false;
            Directory.CreateDirectory(outputDirectory);
            var camera=Camera.main??root.CameraController?.GetComponentInChildren<Camera>(true);
            if(camera==null)throw new InvalidOperationException("Environment review camera is unavailable.");
            camera.fieldOfView=42f;camera.nearClipPlane=.3f;camera.farClipPlane=1800f;

            Render(root,camera,outputDirectory,"lake.png",new Vector2(165f,455f),
                new Vector3(-72f,112f,-84f),45f);
            Render(root,camera,outputDirectory,"marsh.png",new Vector2(815f,218f),
                new Vector3(-66f,96f,-78f),45f);
            Debug.Log("WM15_ENVIRONMENT_REVIEW_CAPTURE "+outputDirectory);
            EditorApplication.Exit(0);
        }

        private static void Render(LocalSceneCompositionRoot root,Camera camera,string directory,string file,
            Vector2 designTarget,Vector3 cameraOffset,float fieldOfView)
        {
            var map=root.TiledWorld.ArtWorldToDesign;
            var target=new Vector3((designTarget.x-map.x)/map.z,0f,(designTarget.y-map.y)/map.w);
            if(!root.WorldHeight.TrySampleWaterSurface(target.x,target.z,out var water))
                target.y=root.WorldHeight.SampleHeight(target.x,target.z)+2f;
            else target.y=water+2.2f;
            var position=target+cameraOffset;
            position.y=Mathf.Max(position.y,root.WorldHeight.SampleHeight(position.x,position.z)+30f);
            root.TiledWorld.RefreshNow(new WorldPosition(position.x,0f,position.z),
                Array.Empty<WorldPosition>(),Array.Empty<Tuple<WorldPosition,WorldPosition>>());
            camera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(target-position));
            camera.fieldOfView=fieldOfView;

            Write(camera,Path.Combine(directory,file));
        }

        private static void RenderAbsolute(LocalSceneCompositionRoot root,Camera camera,string directory,string file,
            Vector3 position,Vector3 target,float fieldOfView)
        {
            root.TiledWorld.RefreshNow(new WorldPosition(position.x,0f,position.z),
                Array.Empty<WorldPosition>(),Array.Empty<Tuple<WorldPosition,WorldPosition>>());
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            camera.fieldOfView=fieldOfView;
            Write(camera,Path.Combine(directory,file));
        }

        private static void Write(Camera camera,string output)
        {
            const int width=1920,height=1080;
            var render=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;camera.targetTexture=render;camera.Render();
            RenderTexture.active=render;image.ReadPixels(new Rect(0,0,width,height),0,0,false);image.Apply(false,false);
            File.WriteAllBytes(output,image.EncodeToPNG());
            camera.targetTexture=previous;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(render);
        }
    }
}
#endif
