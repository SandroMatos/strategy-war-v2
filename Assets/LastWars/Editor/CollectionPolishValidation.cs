using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LastWars.Client.Editor
{
    // Isolated visual fixture: does not connect to the API or use/change the player's session.
    [InitializeOnLoad]
    public static class CollectionPolishValidation
    {
        const string Active = "CollectionPolish.Active", Output = "Library/CollectionPolishValidation";
        static double next;
        static int phase;
        static bool hadError;
        static BaseWorld world;
        static BuildingProductionView production;
        static ProductionStorageDto snapshot;
        static AnimatedResourceValue counter;
        static CollectionPolishValidation()
        {
            if (SessionState.GetBool(Active, false))
            {
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += Changed;
                Application.logMessageReceived += Log;
            }
        }
        [MenuItem("LastWars/Validar barras e bursts (sem alterar conta)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Saia do Play Mode e salve a cena antes da validação.");
            // Use the existing prefabs; this fixture does not modify assets.
            SessionState.SetString("CollectionPolish.Scene", EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Active, true);
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (phase == 0)
                {
                    var types = new[] { "food_farm", "iron_mine", "gold_refinery", "oil_pump" };
                    var data = new BaseDto { resources = new ResourcesDto { food=1250, iron=1100, gold=550, oil=700 },
                        available_builders=1, total_builders=2, grid_bounds=new GridDto { min_x=6,min_y=6,max_x=16,max_y=16 },
                        buildings = types.Select((type,i) => new BuildingDto { id="fixture-"+i,type=type,level=1,status="completed",
                            grid_x=8+(i%2)*4,grid_y=8+(i/2)*4,width=2,height=2 }).ToArray() };
                    world = new GameObject("Fixture world").AddComponent<BaseWorld>(); world.Initialize(); world.Render(data,null,true);
                    var ui = new GameObject("Fixture UI",typeof(RectTransform)).AddComponent<FrontierView>(); ui.Initialize(); ui.Hud(data,"Prévia dos quatro recursos"); ui.StorageCapacity(5000);
                    production=ui.gameObject.AddComponent<BuildingProductionView>(); production.Initialize(world); production.Render(data);
                    snapshot=new ProductionStorageDto { local_production=data.buildings.Select((b,i)=>new ProductionDto {
                        building_id=b.id,building_type=b.type,local_capacity=1000,
                        stored_resources=new ResourcesDto { food=50,iron=50,gold=50,oil=50 },
                        production_per_hour=new ResourcesDto { food=100,iron=100,gold=100,oil=100 } }).ToArray() };
                    production.Snapshot(snapshot);
                    var counters=ui.GetComponentsInChildren<AnimatedResourceValue>();
                    Require(counters.Length==5,"Expected four resources and builders");
                    counter=counters.First(c=>c.gameObject.name=="Comida");
                    counter.SetValue(2250,5000);
                    Require(counter.DisplayedValue==1250 && counter.IsAnimating,"Counter jumped instead of animating");
                    next=EditorApplication.timeSinceStartup+2; phase=1;return;
                }
                if (phase == 1)
                {
                    foreach(var track in UnityEngine.Object.FindObjectsOfType<RectTransform>().Where(t=>t.name=="Track"))
                        Require(track.anchorMin==Vector2.zero && track.anchorMax==Vector2.one && track.offsetMin==Vector2.zero && track.offsetMax==Vector2.zero,"Bar does not fill panel");
                    Require(counter.DisplayedValue==2250 && !counter.IsAnimating,"Counter missed first target");
                    counter.SetValue(3250,5000);
                    ScreenCapture.CaptureScreenshot(Output+"/full-panel-bars.png");
                    foreach(var item in snapshot.local_production) item.stored_resources=new ResourcesDto { food=100,iron=100,gold=100,oil=100 };
                    production.Snapshot(snapshot);
                    next=EditorApplication.timeSinceStartup+.2; phase=15;return;
                }
                if (phase == 15)
                {
                    Require(counter.DisplayedValue>2250 && counter.DisplayedValue<3250,"Counter did not interpolate");
                    long shown=counter.DisplayedValue;
                    counter.SetValue(500,5000);
                    Require(counter.DisplayedValue==shown,"Retarget jumped away from displayed value");
                    counter.SetValue(500,5000); // Duplicate snapshots must not create competing animations.
                    next=EditorApplication.timeSinceStartup+1; phase=2;return;
                }
                if (phase == 2)
                {
                    Require(counter.DisplayedValue==500 && !counter.IsAnimating,"Old tween survived replacement");
                    ScreenCapture.CaptureScreenshot(Output+"/four-resources-ready.png");
                    CollectionFeedback.Play(new ResourcesDto(),Vector3.zero,world.transform);
                    Require(UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Length==0,"Zero collection emitted particles");
                    foreach(var item in snapshot.local_production)
                    {
                        var amount=new ResourcesDto();
                        switch(ProductionRules.Kind(item.building_type)) { case ResourceKind.Food:amount.food=100;break;case ResourceKind.Iron:amount.iron=100;break;case ResourceKind.Gold:amount.gold=100;break;default:amount.oil=100;break; }
                        CollectionFeedback.Play(amount,world.BuildingModel(item.building_id).transform.position+Vector3.up*1.6f,world.transform);
                    }
                    production.Hidden=false;
                    next=EditorApplication.timeSinceStartup+.45; phase=3;return;
                }
                if (phase == 3)
                {
                    var effects=UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
                    Require(effects.Length==4,"Expected four resource prefabs");
                    foreach(var effect in effects)
                        Require(!effect.main.loop && effect.particleCount>0 && effect.main.stopAction==ParticleSystemStopAction.Destroy,"Burst lifecycle invalid");
                    var overlays=UnityEngine.Object.FindObjectsOfType<ResourceParticleOverlay>();
                    Require(overlays.Length==4,"Expected four UI particle renderers");
                    foreach(var overlay in overlays)
                        Require(overlay.canvas.sortingOrder>0 && !overlay.raycastTarget && overlay.canvasRenderer.materialCount>0,"Overlay ordering/material invalid");
                    ScreenCapture.CaptureScreenshot(Output+"/collection-bursts.png");
                    next=EditorApplication.timeSinceStartup+3;phase=4;return;
                }
                if(phase==4)
                {
                    Require(UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Length==0,"Burst did not self-destroy");
                    Require(UnityEngine.Object.FindObjectsOfType<ResourceParticleOverlay>().Length==0,"Overlay did not self-destroy");
                    Finish(!hadError,"Five HUD bars; interrupted counter tween reaches latest target; four overlay bursts; automatic cleanup.");
                }
            }
            catch(Exception error) { Finish(false,error.Message); }
        }
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static void Log(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) hadError=true; }
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Active,false)) Finish(false,"Interrupted");
        }
        static void Finish(bool success,string message)
        {
            File.WriteAllText(Output+"/result.txt",(success?"PASS\n":"FAIL\n")+message);
            SessionState.SetBool(Active,false);EditorApplication.update-=Tick;
            EditorApplication.playModeStateChanged-=Changed;Application.logMessageReceived-=Log;
            string scene=SessionState.GetString("CollectionPolish.Scene","");
            EditorApplication.playModeStateChanged+=Restore;
            EditorApplication.isPlaying=false;
            void Restore(PlayModeStateChange state)
            {
                if(state!=PlayModeStateChange.EnteredEditMode) return;
                EditorApplication.playModeStateChanged-=Restore;
                if(!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
            }
            Debug.Log("Collection polish "+(success?"PASS: ":"FAIL: ")+message);
        }
    }
}
