using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.World
{
    public sealed class AnimalVitalState
    {
        public AnimalVitalState(StableEntityId id, string species, int maximum, int health,
            bool domestic = false, long shotTicks = 0, int shotSequence = 0, bool harvested = false,
            StableEntityId home = default, bool male = false, long bornTick = 0)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(species) || maximum <= 0 || health < 0 ||
                health > maximum || shotTicks < 0 || shotSequence < 0 || harvested && health != 0 ||
                bornTick < 0 || home.IsValid && !domestic)
                throw new ArgumentException("Invalid animal health state.");
            Id = id; Species = species; Maximum = maximum; Health = health; Domestic = domestic;
            ShotTicks = shotTicks; ShotSequence = shotSequence; Harvested = harvested;
            Home = home; Male = male; BornTick = bornTick;
        }
        public StableEntityId Id { get; }
        public string Species { get; }
        public int Maximum { get; }
        public int Health { get; }
        public bool Domestic { get; }
        public long ShotTicks { get; }
        public int ShotSequence { get; }
        public bool Harvested { get; }
        public bool Alive => Health > 0;
        public StableEntityId Home { get; }
        public bool Male { get; }
        public long BornTick { get; }
        public double GrowthFraction(long calendarTick, int maturityDays) =>
            Math.Max(0d, Math.Min(1d, (double)(calendarTick - BornTick) /
                (TimeSpan.TicksPerDay * Math.Max(1, maturityDays))));
        public AnimalVitalState With(int health, long shotTicks, int sequence, bool harvested = false) =>
            new AnimalVitalState(Id, Species, Maximum, health, Domestic, shotTicks, sequence, harvested, Home, Male, BornTick);
    }

    public sealed class AnimalHuntingPolicy
    {
        public AnimalHuntingPolicy(int arrowDamage, long drawTicks, long flightTicks, float range,
            int accuracyPermille, IReadOnlyDictionary<string, int> health, int pastureCapacity = 16,
            int livestockMaturityDays = 3, int livestockBirthDays = 5,
            IEnumerable<string> domesticSpecies = null)
        {
            if (arrowDamage <= 0 || drawTicks <= 0 || flightTicks <= 0 || range <= 0f ||
                accuracyPermille < 1 || accuracyPermille > 1000 || health == null || health.Any(p => p.Value <= 0) ||
                pastureCapacity < 2 || pastureCapacity > 16 || livestockMaturityDays < 1 || livestockBirthDays < 1)
                throw new ArgumentException("Invalid hunting policy.");
            ArrowDamage = arrowDamage; DrawTicks = drawTicks; FlightTicks = flightTicks;
            Range = range; AccuracyPermille = accuracyPermille; Health = health;
            PastureCapacity = pastureCapacity; LivestockMaturityDays = livestockMaturityDays;
            LivestockBirthDays = livestockBirthDays;
            DomesticSpecies = Array.AsReadOnly((domesticSpecies ?? new[] { "sheep", "cow", "pig", "goat", "duck", "chicken", "horse" })
                .Where(health.ContainsKey).Distinct().ToArray());
        }
        public int ArrowDamage { get; }
        public long DrawTicks { get; }
        public long FlightTicks { get; }
        public float Range { get; }
        public int AccuracyPermille { get; }
        public IReadOnlyDictionary<string, int> Health { get; }
        public int PastureCapacity { get; }
        public int LivestockMaturityDays { get; }
        public int LivestockBirthDays { get; }
        public IReadOnlyList<string> DomesticSpecies { get; }
        public static AnimalHuntingPolicy Default => new AnimalHuntingPolicy(30,
            TimeSpan.TicksPerSecond * 60, TimeSpan.TicksPerSecond * 24, 18f, 850,
            new Dictionary<string, int> { ["deer"] = 120, ["boar"] = 150, ["bear"] = 300,
                ["hare"] = 30, ["waterfowl"] = 30, ["sheep"] = 90, ["cow"] = 180,
                ["pig"] = 120, ["goat"] = 100, ["duck"] = 35, ["chicken"] = 25, ["horse"] = 200 });
    }

    public readonly struct HuntingImpact
    {
        public HuntingImpact(StableEntityId animal, StableEntityId hunter, int damage)
        { Animal = animal; Hunter = hunter; Damage = damage; }
        public StableEntityId Animal { get; }
        public StableEntityId Hunter { get; }
        public int Damage { get; }
    }

    // One saved authority for every registered wild/domestic animal, independent of visual tiers.
    public sealed class AnimalHealthRegistry
    {
        private readonly Dictionary<StableEntityId, AnimalVitalState> animals = new();
        private readonly HashSet<StableEntityId> pastures = new();
        public bool IsPasture(StableEntityId id) => pastures.Contains(id);
        public IReadOnlyList<StableEntityId> Pastures => pastures.OrderBy(x => x).ToArray();
        public IReadOnlyList<AnimalVitalState> AtHome(StableEntityId home) =>
            animals.Values.Where(x => x.Home == home).OrderBy(x => x.Id).ToArray();
        public AnimalHuntingPolicy Policy { get; private set; } = AnimalHuntingPolicy.Default;
        public event Action<HuntingImpact> Impact;
        public event Action<HuntingImpact> Damaged;
        public Func<StableEntityId, StableEntityId, bool> CanHit { get; set; }
        public Func<StableEntityId, bool> CanHunt { get; set; }
        public IReadOnlyList<AnimalVitalState> States => animals.Values.OrderBy(x => x.Id).ToArray();
        public void Configure(AnimalHuntingPolicy policy)
        {
            if (animals.Count != 0) throw new InvalidOperationException("Configure before registering animals.");
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }
        public AnimalVitalState Register(StableEntityId id, string species, bool domestic = false)
        {
            if (animals.TryGetValue(id, out var existing)) return existing;
            if (!Policy.Health.TryGetValue(species, out var maximum))
                throw new ArgumentException("Missing animal health definition: " + species);
            var state = new AnimalVitalState(id, species, maximum, maximum, domestic);
            animals.Add(id, state);
            return state;
        }
        public bool TryGet(StableEntityId id, out AnimalVitalState state) => animals.TryGetValue(id, out state);
        public bool IsAlive(StableEntityId id) => !animals.TryGetValue(id, out var state) || state.Alive;

        public AnimalVitalState RegisterDomestic(StableEntityId id, string species, StableEntityId home,
            bool male, long bornTick)
        {
            if (!home.IsValid || animals.ContainsKey(id)) throw new ArgumentException("Invalid livestock identity/home.");
            var maximum = Policy.Health[species];
            var state = new AnimalVitalState(id, species, maximum, maximum, true, home: home, male: male, bornTick: bornTick);
            animals.Add(id, state); pastures.Add(home); return state;
        }

        public void CancelShot(StableEntityId id)
        {
            if (animals.TryGetValue(id, out var state) && state.ShotTicks != 0)
                animals[id] = state.With(state.Health, 0, state.ShotSequence, state.Harvested);
        }

        public int Damage(StableEntityId id, int amount, StableEntityId source = default)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!animals.TryGetValue(id, out var state) || !state.Alive) return 0;
            var damage = Math.Min(amount, state.Health);
            animals[id] = state.With(state.Health - damage, state.ShotTicks, state.ShotSequence);
            if (damage > 0) Damaged?.Invoke(new HuntingImpact(id,source,damage));
            return damage;
        }

        public bool IsAccurateShot(StableEntityId id, int sequence)
        {
            uint roll = unchecked((uint)sequence * 2654435761u);
            foreach (var b in id.ToGuid().ToByteArray()) roll = unchecked((roll ^ b) * 16777619u);
            return roll % 1000 < Policy.AccuracyPermille;
        }

        // Called only by an arrived hunter's production task. Work time never directly becomes damage.
        // A shot must complete drawing and flight, then pass accuracy and range/obstruction checks.
        public void AdvanceHunt(StableEntityId id, StableEntityId hunter, long elapsedTicks)
        {
            if (elapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            if (!animals.TryGetValue(id, out var animal) || !animal.Alive || animal.Harvested) return;
            if (CanHunt != null && !CanHunt(hunter)) return;
            var cycle = checked(Policy.DrawTicks + Policy.FlightTicks);
            var remaining = elapsedTicks;
            while (remaining > 0 && animal.Alive)
            {
                var advance = Math.Min(remaining, cycle - animal.ShotTicks);
                remaining -= advance;
                var progress = animal.ShotTicks + advance;
                if (progress < cycle)
                {
                    animal = animal.With(animal.Health, progress, animal.ShotSequence);
                    break;
                }
                var sequence = checked(animal.ShotSequence + 1);
                var hit = IsAccurateShot(id, sequence) && (CanHit?.Invoke(hunter, id) ?? true);
                var damage = hit ? Math.Min(animal.Health, Policy.ArrowDamage) : 0;
                animal = animal.With(animal.Health - damage, 0, sequence);
                animals[id] = animal;
                Impact?.Invoke(new HuntingImpact(id, hunter, damage));
                if (damage > 0) Damaged?.Invoke(new HuntingImpact(id,hunter,damage));
            }
            animals[id] = animal;
        }

        public void Harvest(StableEntityId id)
        {
            if (!animals.TryGetValue(id, out var animal) || animal.Alive || animal.Harvested)
                throw new InvalidOperationException("Only an unprocessed dead animal can be harvested.");
            animals[id] = animal.With(0, 0, animal.ShotSequence, true);
        }

        public bool RestoreWildPopulation(StableEntityId id)
        {
            if (!animals.TryGetValue(id, out var animal) || animal.Domestic || animal.Alive) return false;
            animals[id] = new AnimalVitalState(animal.Id, animal.Species, animal.Maximum, animal.Maximum);
            return true;
        }
        public void Restore(IEnumerable<AnimalVitalState> states)
        {
            var values = (states ?? Array.Empty<AnimalVitalState>()).ToArray();
            var restored = values.ToDictionary(x => x.Id);
            if (values.Any(x => !Policy.Health.ContainsKey(x.Species) ||
                x.ShotTicks >= Policy.DrawTicks + Policy.FlightTicks))
                throw new ArgumentException("Invalid saved animal health.");
            animals.Clear();
            pastures.Clear();
            foreach (var pair in restored)
            {
                animals.Add(pair.Key, pair.Value);
                if (pair.Value.Home.IsValid) pastures.Add(pair.Value.Home);
            }
        }
    }
}
