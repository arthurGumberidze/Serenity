using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Infrastructure;
using Game.Infrastructure.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.Editor
{
    public sealed class WM15AssetExpansionRuntimeTests
    {
        [UnityTest,Timeout(600000)]
        public IEnumerator RenderedUpgradeSaveUniformSnowAndLakeRoute()
        {
            SessionState.SetString("WM15Expansion.PreviousSaveDirectory",Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY")??string.Empty);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY",Path.GetFullPath("Artifacts/WM15AssetExpansion/runtime-saves"));
            yield return new EnterPlayMode();
            Application.runInBackground=true;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            UnityEngine.Object.FindAnyObjectByType<MainMenuController>().StartNewGame(1380062136);
            LocalSceneCompositionRoot root;
            while((root=UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>())==null||!root.IsInitialized)yield return null;
            var checks=new List<string>();
            yield return WM15AssetExpansionPlayerSmoke.Exercise(root,"Artifacts/WM15AssetExpansion/runtime",(pass,label)=>checks.Add((pass?"PASS ":"FAIL ")+label));
            File.WriteAllLines("Artifacts/WM15AssetExpansion/runtime/checks.txt",checks);
            Assert.That(checks.Where(x=>x.StartsWith("FAIL ")),Is.Empty);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
            var previous=SessionState.GetString("WM15Expansion.PreviousSaveDirectory",string.Empty);
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY",string.IsNullOrEmpty(previous)?null:previous);
        }
    }
}
