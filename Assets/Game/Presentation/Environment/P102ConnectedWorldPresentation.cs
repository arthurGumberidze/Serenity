using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.World;
using UnityEngine;

namespace Game.Presentation.Environment
{
    [DisallowMultipleComponent]
    public sealed class P102ConnectedWorldPresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class District
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private string description;
            [SerializeField] private Transform anchor;
            public string Id => id;
            public string DisplayName => displayName;
            public string Description => description;
            public Transform Anchor => anchor;
            public District Configure(string value, string name, string details, Transform valueAnchor)
            { id = value; displayName = name; description = details; anchor = valueAnchor; return this; }
        }

        [SerializeField] private Terrain terrain;
        [SerializeField] private District[] districts = Array.Empty<District>();
        [SerializeField] private Transform[] routeMarkers = Array.Empty<Transform>();
        [SerializeField] private Renderer[] inlandWater = Array.Empty<Renderer>();
        [SerializeField] private Transform[] mountainLandmarks = Array.Empty<Transform>();
        [SerializeField] private int accessibleSamples;
        [SerializeField] private int totalSamples;
        [SerializeField] private int sampleSpacingMeters = ConnectedWorldContract.AuditSampleMeters;
        [SerializeField] private int canonicalResourceAnchorCount;

        public Terrain Terrain => terrain;
        public IReadOnlyList<District> Districts => districts;
        public int AccessibleSamples => accessibleSamples;
        public int TotalSamples => totalSamples;
        public int SampleSpacingMeters => sampleSpacingMeters;
        public int AccessibleAreaSquareMeters => accessibleSamples * sampleSpacingMeters * sampleSpacingMeters;
        public int CanonicalResourceAnchorCount => canonicalResourceAnchorCount;
        public IReadOnlyList<Transform> RouteMarkers => routeMarkers;
        public IReadOnlyList<Renderer> InlandWater => inlandWater;
        public IReadOnlyList<Transform> MountainLandmarks => mountainLandmarks;

        public void Configure(Terrain valueTerrain, IEnumerable<District> valueDistricts,
            IEnumerable<Transform> routes, IEnumerable<Renderer> waters, IEnumerable<Transform> mountains,
            int accessible, int total, int resourceCount)
        {
            terrain = valueTerrain;
            districts = (valueDistricts ?? Enumerable.Empty<District>()).Where(x => x != null && x.Anchor != null).ToArray();
            routeMarkers = (routes ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            inlandWater = (waters ?? Enumerable.Empty<Renderer>()).Where(x => x != null).ToArray();
            mountainLandmarks = (mountains ?? Enumerable.Empty<Transform>()).Where(x => x != null).ToArray();
            accessibleSamples = accessible;
            totalSamples = total;
            canonicalResourceAnchorCount = resourceCount;
        }

        public void ValidateConfiguration()
        {
            if (terrain == null || terrain.terrainData == null || terrain.terrainData.size.x < ConnectedWorldContract.SizeMeters ||
                terrain.terrainData.size.z < ConnectedWorldContract.SizeMeters)
                throw new InvalidOperationException("P10.2 requires one physical terrain of at least 512 x 512 metres.");
            if (sampleSpacingMeters != 2 || AccessibleAreaSquareMeters < ConnectedWorldContract.MinimumAccessibleAreaSquareMeters)
                throw new InvalidOperationException("P10.2 accessible-land audit is below 163,840 square metres on a 2 m grid.");
            if (ConnectedWorldContract.PreservedLocationIds.Any(required => districts.All(x => x.Id != required)) ||
                districts.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != districts.Length)
                throw new InvalidOperationException("P10.2 district identities are incomplete or duplicated.");
            if (canonicalResourceAnchorCount != 24)
                throw new InvalidOperationException("P10.2 must preserve exactly 24 canonical finite source anchors.");
            if (inlandWater.Length < 2 || mountainLandmarks.Length < 3 || routeMarkers.Length < 8)
                throw new InvalidOperationException("P10.2 geography lacks lakes, mountain boundary or readable physical routes.");
        }
    }
}
