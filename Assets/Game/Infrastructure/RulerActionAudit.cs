using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Session JSONL audit: provider plans, executed strategic decisions and resident transitions.</summary>
    public sealed class RulerActionAudit : MonoBehaviour
    {
        [Serializable] private sealed class Entry
        {
            public long gameTick, planSequence;
            public string faction, resident, kind, detail;
        }
        private LocalSceneCompositionRoot root;
        private StreamWriter writer;
        private readonly HashSet<Tier1AiAgentState> observed = new();
        private readonly Dictionary<string,long> decisions = new();
        private float nextFlush;
        public string LogPath { get; private set; }
        public void Initialize(LocalSceneCompositionRoot scene)
        {
            root=scene;
            try
            {
                var directory=Path.Combine(Application.persistentDataPath,"AiAudit");
                Directory.CreateDirectory(directory);
                LogPath=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".jsonl");
                writer=new StreamWriter(LogPath,false,new System.Text.UTF8Encoding(false));
                root.RulerStrategies.Audit=(faction,sequence,kind,detail)=>Write(faction,sequence,null,kind,detail);
                Debug.Log("AI audit: "+LogPath);
            }
            catch(IOException exception){Debug.LogWarning("Cannot open AI audit: "+exception.Message);}
            catch(UnauthorizedAccessException exception){Debug.LogWarning("Cannot open AI audit: "+exception.Message);}
        }
        private void Update()
        {
            if(root==null||writer==null)return;
            foreach(var agent in root.AiAgents.All)
                if(observed.Add(agent)){agent.ActionChanged+=OnAction;OnAction(agent);}
            foreach(var faction in root.StrategicAi.Factions)
            {
                var key=faction.Strategic.Id.ToString();
                decisions.TryGetValue(key,out var previous);
                foreach(var decision in faction.Strategic.RecentDecisions.Where(x=>x.Sequence>previous))
                    Write(key,faction.RulerStrategy.PlanAt(decision.Tick)?.RequestSequence??0,null,
                        "strategic_execution",decision.Kind+"; "+decision.Reason);
                decisions[key]=faction.Strategic.DecisionSequence;
            }
            if(writer!=null&&Time.unscaledTime>=nextFlush)
            {
                nextFlush=Time.unscaledTime+1;
                try{writer.Flush();}catch(IOException){writer.Dispose();writer=null;}
            }
        }
        private void OnAction(Tier1AiAgentState agent)
        {
            var faction=root.StrategicAi.Factions.FirstOrDefault(x=>x.Residents.Contains(agent.CharacterId));
            var job=root.Work.GetQueue(agent.CharacterId).FirstOrDefault();
            var production=faction?.Production??root.Production;
            var productionDetail=production.TryGetWorkerTask(agent.CharacterId,out var task)
                ? "; production="+task.RecipeId+"; site="+task.SiteId+"; task="+task.Id : "";
            Write(faction?.Strategic.Id.ToString()??"player",faction?.RulerStrategy.PlanAt(root.Clock.State.CalendarTicks)?.RequestSequence??0,
                agent.CharacterId.ToString(),"resident_action",agent.CurrentAction+"/"+agent.Phase+
                (job==null?"":"; job="+job.Id+"; type="+job.Type+"; target="+job.Target.EntityId)+productionDetail);
        }
        private void Write(string faction,long sequence,string resident,string kind,string detail)
        {
            if(writer==null)return;
            try{writer.WriteLine(JsonUtility.ToJson(new Entry{gameTick=root.Clock.State.CalendarTicks,
                faction=faction,planSequence=sequence,resident=resident,kind=kind,detail=detail}));}
            catch(IOException){writer.Dispose();writer=null;Debug.LogWarning("AI audit disabled: file write failed.");}
        }
        public void FlushNow()
        {
            try { writer?.Flush(); }
            catch (IOException) { writer?.Dispose(); writer = null; }
        }
        private void OnDestroy()
        {
            foreach(var agent in observed)agent.ActionChanged-=OnAction;
            writer?.Dispose();writer=null;
        }
    }
}
