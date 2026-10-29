using System.Collections.Generic;
using Game.Domain;
using Game.Domain.World;
using Game.Presentation.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure
{
    /// <summary>Bounded snow skin; never modifies collision, navigation, weather or saves.</summary>
    public sealed class DeformableSnowPresentation : MonoBehaviour
    {
        public const float CellSize=.28f;
        private const int Cells=48;
        private const float PatchSize=CellSize*Cells;
        private readonly Dictionary<StableEntityId,TrackState> states=new();
        private readonly Dictionary<Vector2Int,Patch> patches=new();
        private readonly Queue<Vector2Int> pending=new();
        private readonly HashSet<Vector2Int> queued=new();
        private readonly List<StableEntityId> stale=new();
        private TiledWorldHeightSampler heights;
        private CharacterPresentationRegistry presentations;
        private Vector4 map;
        private Material material;
        private float nextSample;
        private struct TrackState { public Vector3 Position; public bool Left; public float Seen; }
        private sealed class Patch
        {
            public Mesh Mesh; public Vector3[] Vertices; public float[] Ground,Depth;
            public Color[] Colors; public bool Dirty;
        }
        public int VisibleTrackCount {get;private set;}
        public int SnowPatchCount=>patches.Count;
        public float MaximumDepression {get;private set;}

        public void Initialize(TiledWorldHeightSampler sampler,CharacterPresentationRegistry registry,Vector4 worldMap)
        {
            heights=sampler;presentations=registry;map=worldMap;
            var shader=Shader.Find("Serenity/DeformableSnow");
            if(shader==null)throw new System.InvalidOperationException("Deformable snow shader is missing.");
            material=new Material(shader){name="Alpine deep snow",enableInstancing=true};
            var art=Resources.Load<Game.Presentation.Environment.LateLandscapeCatalog>("World/LateLandscape");
            if(art!=null){material.SetTexture("_BaseMap",art.SnowAlbedo);material.SetTexture("_NormalMap",art.SnowNormal);}
        }
        private void Update()
        {
            if(heights==null||presentations==null)return;
            if(Time.unscaledTime>=nextSample)
            {
                nextSample=Time.unscaledTime+.1f;
                var camera=Camera.main;
                if(camera!=null && Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,1500f))QueueAround(hit.point,2);
                SampleResidents();
            }
            if(pending.Count>0)BuildPatch(pending.Dequeue());
            foreach(var p in patches.Values)if(p.Dirty)
            {p.Mesh.vertices=p.Vertices;p.Mesh.colors=p.Colors;p.Mesh.RecalculateNormals();p.Mesh.RecalculateBounds();p.Dirty=false;}
        }
        public bool IsTrackableSnow(Vector3 p)=>SnowDepth(p.x,p.z)>.06f;
        private float SnowDepth(float x,float z)
        {
            if(!heights.Definition.ContainsClosed(x,z)||heights.IsWater(x,z))return 0;
            var dx=(x*map.z+map.x-855f)/154f;var dz=(z*map.w+map.y-792f)/124f;
            var edge=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1f,Mathf.Sqrt(dx*dx+dz*dz)));
            if(edge<=0)return 0;
            var h=Mathf.SmoothStep(0,1,Mathf.InverseLerp(7.5f,10.5f,heights.SampleHeight(x,z)));
            var slope=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(24f,36f,heights.SampleSlopeDegrees(x,z,1f)));
            return edge*h*slope*(.18f+.12f*Mathf.PerlinNoise(x*.19f,z*.19f));
        }
        public void QueueAround(Vector3 p,int radius=1)
        {
            if(!IsTrackableSnow(p))return;
            var centre=Key(p);
            for(var z=-radius;z<=radius;z++)for(var x=-radius;x<=radius;x++)
            {var key=centre+new Vector2Int(x,z);if(queued.Count<196&&queued.Add(key))pending.Enqueue(key);}
        }
        private static Vector2Int Key(Vector3 p)=>new(Mathf.FloorToInt(p.x/PatchSize),Mathf.FloorToInt(p.z/PatchSize));
        private void BuildPatch(Vector2Int key)
        {
            if(patches.ContainsKey(key))return;
            var count=(Cells+1)*(Cells+1);
            var p=new Patch{Vertices=new Vector3[count],Ground=new float[count],Depth=new float[count],Colors=new Color[count]};
            var triangles=new List<int>();
            for(var z=0;z<=Cells;z++)for(var x=0;x<=Cells;x++)
            {
                var i=z*(Cells+1)+x;var wx=key.x*PatchSize+x*CellSize;var wz=key.y*PatchSize+z*CellSize;
                if(!heights.Definition.ContainsClosed(wx,wz))continue;
                p.Ground[i]=heights.SampleHeight(wx,wz);p.Depth[i]=SnowDepth(wx,wz);
                p.Vertices[i]=new Vector3(wx,p.Ground[i]+p.Depth[i]+.008f,wz);p.Colors[i]=new Color(0,0,0,1);
            }
            for(var z=0;z<Cells;z++)for(var x=0;x<Cells;x++)
            {
                var a=z*(Cells+1)+x;var b=a+1;var c=a+Cells+1;var d=c+1;
                if(p.Depth[a]<.012f||p.Depth[b]<.012f||p.Depth[c]<.012f||p.Depth[d]<.012f)continue;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            if(triangles.Count==0)return;
            p.Mesh=new Mesh{name="Deformable alpine snow "+key};p.Mesh.MarkDynamic();
            p.Mesh.vertices=p.Vertices;p.Mesh.colors=p.Colors;p.Mesh.SetTriangles(triangles,0);p.Mesh.RecalculateNormals();p.Mesh.RecalculateBounds();
            var view=new GameObject(p.Mesh.name);view.transform.SetParent(transform,false);
            view.AddComponent<MeshFilter>().sharedMesh=p.Mesh;var renderer=view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            patches.Add(key,p);
        }
        private void SampleResidents()
        {
            foreach(var presenter in presentations.Active)
            {
                if(presenter==null||!presenter.IsBound)continue;
                var p=presenter.transform.position;var id=presenter.CharacterId;QueueAround(p);
                if(!states.TryGetValue(id,out var state))state=new TrackState{Position=p};
                state.Seen=Time.unscaledTime;var motion=p-state.Position;motion.y=0;
                if(motion.sqrMagnitude>=.32f*.32f)
                {
                    if(motion.sqrMagnitude<=4f&&IsTrackableSnow(p))
                    {
                        var steps=Mathf.Min(6,Mathf.FloorToInt(motion.magnitude/.32f));
                        for(var n=1;n<=steps;n++){state.Left=!state.Left;Stamp(Vector3.Lerp(state.Position,p,n/(float)steps),motion.normalized,state.Left);}
                    }
                    state.Position=p;
                }
                states[id]=state;
            }
            stale.Clear();foreach(var pair in states)if(Time.unscaledTime-pair.Value.Seen>2f)stale.Add(pair.Key);
            foreach(var id in stale)states.Remove(id);
        }
        public void Stamp(Vector3 position,Vector3 direction,bool left)
        {
            if(!IsTrackableSnow(position))return;
            direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
            var centre=position+side*(left?-.12f:.12f);var key=Key(centre);var affected=false;
            for(var dz=-1;dz<=1;dz++)for(var dx=-1;dx<=1;dx++)
            {
                var patchKey=key+new Vector2Int(dx,dz);
                if(!patches.TryGetValue(patchKey,out var p))continue;
                var minX=Mathf.Max(0,Mathf.FloorToInt((centre.x-.45f-patchKey.x*PatchSize)/CellSize));
                var maxX=Mathf.Min(Cells,Mathf.CeilToInt((centre.x+.45f-patchKey.x*PatchSize)/CellSize));
                var minZ=Mathf.Max(0,Mathf.FloorToInt((centre.z-.45f-patchKey.y*PatchSize)/CellSize));
                var maxZ=Mathf.Min(Cells,Mathf.CeilToInt((centre.z+.45f-patchKey.y*PatchSize)/CellSize));
                for(var z=minZ;z<=maxZ;z++)for(var x=minX;x<=maxX;x++)
                {
                    var i=z*(Cells+1)+x;
                    var delta=p.Vertices[i]-centre;delta.y=0;if(delta.sqrMagnitude>1f||p.Depth[i]<.04f)continue;
                    var across=Vector3.Dot(delta,side)/.24f;var along=Vector3.Dot(delta,direction)/.36f;
                    var radius=across*across+along*along;if(radius>=1f)continue;
                    var pressure=Mathf.SmoothStep(1,0,radius);var depression=Mathf.Min(p.Depth[i]*.8f,.21f)*pressure;
                    var target=p.Ground[i]+p.Depth[i]+.008f-depression;if(target>=p.Vertices[i].y)continue;
                    p.Vertices[i].y=target;p.Colors[i].r=Mathf.Max(p.Colors[i].r,pressure);
                    MaximumDepression=Mathf.Max(MaximumDepression,depression);p.Dirty=true;affected=true;
                }
            }
            if(affected)VisibleTrackCount++;
        }
        private void OnDestroy(){if(material!=null)Destroy(material);foreach(var p in patches.Values)if(p.Mesh!=null)Destroy(p.Mesh);}
    }
}
