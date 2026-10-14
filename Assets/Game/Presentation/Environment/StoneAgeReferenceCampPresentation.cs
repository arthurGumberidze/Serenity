using System;
using System.Collections.Generic;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>
    /// Disposable presentation for the P3 reference camp. Canonical characters and buildings remain
    /// owned by Domain/Simulation; this component only supplies equipment, fire, ambience and art metrics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoneAgeReferenceCampPresentation : MonoBehaviour
    {
        private const float FireLightRefreshIntervalSeconds = 1f / 15f;
        [SerializeField] private Transform campfireAnchor;
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private GameObject stoneAxePrefab;
        [SerializeField] private GameObject stoneSpearPrefab;
        [SerializeField] private Material flameParticleMaterial;
        [SerializeField] private Material smokeParticleMaterial;
        [SerializeField] private Material rainParticleMaterial;
        [SerializeField] private Material shelterSurfaceMaterial;
        [SerializeField] private Material storageSurfaceMaterial;
        [SerializeField, Min(0)] private int authoredTreeCount;
        [SerializeField, Min(0)] private int authoredRockCount;
        [SerializeField, Min(0)] private int authoredGrassClusterCount;

        private readonly List<GameObject> equippedTools = new List<GameObject>();
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
        private Light fireLight;
        private AudioSource fireAudio;
        private AudioSource shoreAudio;
        private ParticleSystem flameParticles;
        private ParticleSystem smokeParticles;
        private bool initialized;
        private float nextFireLightRefresh;

        public bool IsInitialized => initialized;
        public Transform CampfireAnchor => campfireAnchor;
        public Renderer WaterRenderer => waterRenderer;
        public Material RainParticleMaterial => rainParticleMaterial;
        public Material ShelterSurfaceMaterial => shelterSurfaceMaterial;
        public Material StorageSurfaceMaterial => storageSurfaceMaterial;
        public int TreeCount => authoredTreeCount;
        public int RockCount => authoredRockCount;
        public int GrassClusterCount => authoredGrassClusterCount;
        public int EquippedToolCount => equippedTools.Count;
        public int SpatialAudioSourceCount => (fireAudio != null ? 1 : 0) + (shoreAudio != null ? 1 : 0);
        public bool CampfireEffectsActive => fireLight != null && flameParticles != null &&
            smokeParticles != null && fireAudio != null;

        public void Configure(Transform fireAnchor, Renderer water, GameObject axePrefab, GameObject spearPrefab,
            Material flameMaterial, Material smokeMaterial,
            Material rainMaterial, Material shelterMaterial, Material storageMaterial,
            int treeCount, int rockCount, int grassClusterCount)
        {
            campfireAnchor = fireAnchor;
            waterRenderer = water;
            stoneAxePrefab = axePrefab;
            stoneSpearPrefab = spearPrefab;
            flameParticleMaterial = flameMaterial;
            smokeParticleMaterial = smokeMaterial;
            rainParticleMaterial = rainMaterial;
            shelterSurfaceMaterial = shelterMaterial;
            storageSurfaceMaterial = storageMaterial;
            authoredTreeCount = Mathf.Max(0, treeCount);
            authoredRockCount = Mathf.Max(0, rockCount);
            authoredGrassClusterCount = Mathf.Max(0, grassClusterCount);
        }

        public void Initialize(CharacterPresenter firstCharacter, CharacterPresenter secondCharacter,
            BuildingPresenter shelter, BuildingPresenter storage)
        {
            if (initialized) return;
            if (campfireAnchor == null || waterRenderer == null || stoneAxePrefab == null || stoneSpearPrefab == null ||
                flameParticleMaterial == null || smokeParticleMaterial == null || rainParticleMaterial == null ||
                shelterSurfaceMaterial == null || storageSurfaceMaterial == null)
                throw new InvalidOperationException("P3 reference camp presentation is not fully configured.");
            if (firstCharacter == null || !firstCharacter.IsBound || secondCharacter == null || !secondCharacter.IsBound)
                throw new ArgumentException("P3 equipment requires two bound canonical character presenters.");

            AttachTool(stoneAxePrefab, firstCharacter.RightHandSocket,
                new Vector3(0.01f, 0.08f, 0.02f), Quaternion.Euler(15f, 8f, 92f));
            AttachTool(stoneSpearPrefab, secondCharacter.RightHandSocket,
                new Vector3(0.01f, 0.18f, 0.02f), Quaternion.Euler(-8f, 2f, 88f));
            ApplySurfaceMaterial(shelter, shelterSurfaceMaterial, "shelter");
            ApplySurfaceMaterial(storage, storageSurfaceMaterial, "storage");
            CreateCampfireEffects();
            CreateAmbientAudio();
            initialized = true;
        }

        public void RebindCharacterTools(CharacterPresenter firstCharacter, CharacterPresenter secondCharacter)
        {
            if (!initialized) throw new InvalidOperationException("P3 reference camp is not initialized.");
            if (firstCharacter == null || !firstCharacter.IsBound || secondCharacter == null || !secondCharacter.IsBound)
                throw new ArgumentException("P3 equipment requires two bound canonical character presenters.");
            for (var i = 0; i < equippedTools.Count; i++)
                if (equippedTools[i] != null) Destroy(equippedTools[i]);
            equippedTools.Clear();
            AttachTool(stoneAxePrefab, firstCharacter.RightHandSocket,
                new Vector3(0.01f, 0.08f, 0.02f), Quaternion.Euler(15f, 8f, 92f));
            AttachTool(stoneSpearPrefab, secondCharacter.RightHandSocket,
                new Vector3(0.01f, 0.18f, 0.02f), Quaternion.Euler(-8f, 2f, 88f));
        }

        private static void ApplySurfaceMaterial(BuildingPresenter presenter, Material material, string label)
        {
            if (presenter == null || !presenter.IsBound)
                throw new InvalidOperationException("P3 " + label + " presenter is not bound.");
            if (material == null) throw new InvalidOperationException("P3 " + label + " material is missing.");
            var renderers = presenter.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("P3 " + label + " has no renderers.");
            for (var i = 0; i < renderers.Length; i++)
            {
                var slots = renderers[i].sharedMaterials;
                for (var slot = 0; slot < slots.Length; slot++) slots[slot] = material;
                renderers[i].sharedMaterials = slots;
                renderers[i].SetPropertyBlock(null);
            }
        }

        private void AttachTool(GameObject prefab, Transform socket, Vector3 localPosition, Quaternion localRotation)
        {
            if (socket == null) throw new InvalidOperationException("Character hand socket is missing.");
            var instance = Instantiate(prefab, socket, false);
            instance.name = "P3 Equipped " + prefab.name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            equippedTools.Add(instance);
        }

        private void CreateCampfireEffects()
        {
            var lightObject = new GameObject("P3 Campfire Light");
            lightObject.transform.SetParent(campfireAnchor, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.42f, 0.12f);
            fireLight.intensity = 2.2f;
            fireLight.range = 9f;
            fireLight.shadows = LightShadows.Soft;

            flameParticles = CreateParticles("P3 Campfire Flames", campfireAnchor, new Color(1f, 0.22f, 0.03f, 0.9f),
                0.55f, 0.18f, 0.85f, 42f, 34, ParticleSystemShapeType.Cone, flameParticleMaterial);
            smokeParticles = CreateParticles("P3 Campfire Smoke", campfireAnchor, new Color(0.22f, 0.24f, 0.25f, 0.34f),
                2.8f, 0.5f, 0.7f, 8f, 22, ParticleSystemShapeType.Circle, smokeParticleMaterial);
            var smokeMain = smokeParticles.main;
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
            smokeMain.gravityModifier = -0.025f;

            fireAudio = campfireAnchor.gameObject.AddComponent<AudioSource>();
            ConfigureSpatialAudio(fireAudio, 0.16f, 16f);
            fireAudio.clip = CreateNoiseLoop("P3 Campfire Loop", 3.2f, 0.19f, 11f, 0.68f);
            fireAudio.Play();
        }

        private void CreateAmbientAudio()
        {
            shoreAudio = gameObject.AddComponent<AudioSource>();
            ConfigureSpatialAudio(shoreAudio, 0.09f, 42f);
            shoreAudio.spatialBlend = 0.35f;
            shoreAudio.clip = CreateNoiseLoop("P3 Shore Loop", 5.1f, 0.08f, 2.4f, 0.93f);
            shoreAudio.Play();
        }

        private ParticleSystem CreateParticles(string objectName, Transform parent, Color color, float lifetime,
            float speed, float size, float rate, int maxParticles, ParticleSystemShapeType shapeType,
            Material particleMaterial)
        {
            var particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = shapeType;
            shape.radius = 0.22f;
            shape.angle = 8f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            particles.Play();
            return particles;
        }

        private AudioClip CreateNoiseLoop(string clipName, float seconds, float amplitude, float pulseFrequency,
            float smoothing)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[sampleCount];
            var random = new System.Random(1731 + clipName.GetHashCode());
            var filtered = 0f;
            for (var i = 0; i < sampleCount; i++)
            {
                var noise = (float)(random.NextDouble() * 2d - 1d);
                filtered = smoothing * filtered + (1f - smoothing) * noise;
                var pulse = 0.7f + 0.3f * Mathf.Sin(i * Mathf.PI * 2f * pulseFrequency / sampleRate);
                var edgeFade = Mathf.Clamp01(Mathf.Min(i, sampleCount - 1 - i) / (sampleRate * 0.08f));
                samples[i] = filtered * amplitude * pulse * edgeFade;
            }
            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            runtimeAssets.Add(clip);
            return clip;
        }

        private static void ConfigureSpatialAudio(AudioSource source, float volume, float maxDistance)
        {
            source.loop = true;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = 0.82f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = maxDistance;
        }

        private void Update()
        {
            if (!initialized || fireLight == null) return;
            if (Time.unscaledTime < nextFireLightRefresh) return;
            nextFireLightRefresh = Time.unscaledTime + FireLightRefreshIntervalSeconds;
            fireLight.intensity = 2.05f + Mathf.Sin(Time.unscaledTime * 8.2f) * 0.18f +
                Mathf.Sin(Time.unscaledTime * 13.7f) * 0.08f;
        }

        private void OnDestroy()
        {
            for (var i = 0; i < runtimeAssets.Count; i++)
                if (runtimeAssets[i] != null) Destroy(runtimeAssets[i]);
            runtimeAssets.Clear();
        }
    }
}
