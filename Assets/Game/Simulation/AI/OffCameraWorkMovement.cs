using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;

namespace Game.Simulation.AI
{
    /// <summary>Movement for the same work executions without a GameObject. A route must be approved by the world.</summary>
    public sealed class OffCameraWorkMovement : ITier1MovementDriver
    {
        private readonly Func<WorldPosition,WorldPosition,IReadOnlyList<WorldPosition>> routeFinder;
        private readonly float speed;
        private IReadOnlyList<WorldPosition> route;
        private int next;
        private float stopping;
        public OffCameraWorkMovement(StableEntityId id, WorldPosition position, float metresPerSecond,
            Func<WorldPosition,WorldPosition,IReadOnlyList<WorldPosition>> findRoute)
        { CharacterId=id; CurrentPosition=position; speed=metresPerSecond; routeFinder=findRoute; }
        public StableEntityId CharacterId { get; }
        public WorldPosition CurrentPosition { get; private set; }
        public MovementState State { get; private set; }
        public bool IsAvailable => true;
        public void MoveTo(WorldPosition target,float stoppingDistance)
        {
            stopping=stoppingDistance; route=routeFinder(CurrentPosition,target); next=0;
            State=route==null || route.Count==0 ? MovementState.Failed : MovementState.Moving;
        }
        public void Stop() { route=null; State=MovementState.Idle; }
        public void Advance(TimeSpan calendarDelta)
        {
            var budget=(float)calendarDelta.TotalSeconds/60f*speed;
            while(State==MovementState.Moving && budget>0)
            {
                var destination=route[next];
                var distance=(float)Math.Sqrt(CurrentPosition.DistanceSquared(destination));
                var remaining=Math.Max(0,distance-(next==route.Count-1 ? stopping : 0));
                var step=Math.Min(budget,remaining);
                if(distance>.0001f)
                    CurrentPosition=new WorldPosition(CurrentPosition.X+(destination.X-CurrentPosition.X)*step/distance,
                        CurrentPosition.Y+(destination.Y-CurrentPosition.Y)*step/distance,
                        CurrentPosition.Z+(destination.Z-CurrentPosition.Z)*step/distance);
                budget-=step;
                if(step<remaining) break;
                if(++next>=route.Count) State=MovementState.Arrived;
            }
        }
    }
}
