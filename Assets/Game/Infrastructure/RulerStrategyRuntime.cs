using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Domain.AI;
using Game.Simulation.AI;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Infrastructure
{
    public static class RulerAiModes
    {
        public const string Deterministic = "deterministic";
        public const string Ollama = "ollama";
        public const string Cloud = "cloud";
        public static readonly string[] All = { Deterministic, Ollama, Cloud };
    }

    public sealed class RulerStrategyRuntime : MonoBehaviour
    {
        private RulerStrategyCoordinator coordinator;
        private AiStrategicScheduler scheduler;
        private Func<long> calendarTick;
        private Func<bool> paused;
        private string mode;
        private RulerStrategyRequest activeRequest;
        private Coroutine activeCoroutine;
        private readonly HashSet<string> dispatched = new HashSet<string>(StringComparer.Ordinal);

        public bool IsRequestInFlight => activeCoroutine != null;
        public string Mode => mode;
        public string LastStatus { get; private set; } = string.Empty;

        public void Initialize(AiStrategicScheduler scheduler, string configuredMode,
            Func<long> calendarTickProvider, Func<bool> pauseProvider)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            coordinator = new RulerStrategyCoordinator(this.scheduler);
            calendarTick = calendarTickProvider ?? throw new ArgumentNullException(nameof(calendarTickProvider));
            paused = pauseProvider ?? throw new ArgumentNullException(nameof(pauseProvider));
            mode = RulerAiModes.All.Contains(configuredMode, StringComparer.Ordinal)
                ? configuredMode : RulerAiModes.Ollama;
            this.scheduler.StrategyTriggerRequested += OnStrategyTriggerRequested;
        }

        public void AdvanceTo(long tick)
        {
            if (coordinator == null || mode == RulerAiModes.Deterministic || paused()) return;
            coordinator.AdvanceTo(tick);
            TryDispatch();
        }

        public bool Request(AiStateId factionId, RulerStrategyTrigger trigger, string subject = null)
        {
            if (coordinator == null || mode == RulerAiModes.Deterministic) return false;
            var accepted = coordinator.Request(factionId, trigger, calendarTick(), subject);
            if (accepted && !paused()) TryDispatch();
            return accepted;
        }

        public bool RequestDialogue(AiStateId factionId, string playerMessage) =>
            Request(factionId, RulerStrategyTrigger.Dialogue, playerMessage);

        public bool RequestTradeReview(AiStateId factionId, string offerSummary) =>
            Request(factionId, RulerStrategyTrigger.Trade, offerSummary);

        public void RebaseAfterLoad()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            activeRequest = null;
            dispatched.Clear();
            if (coordinator != null && mode != RulerAiModes.Deterministic && !paused()) TryDispatch();
        }

        private void Update()
        {
            if (coordinator != null && activeCoroutine == null && !paused()) TryDispatch();
        }

        private void OnDestroy()
        {
            if (scheduler != null) scheduler.StrategyTriggerRequested -= OnStrategyTriggerRequested;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            activeRequest = null;
        }

        private void OnStrategyTriggerRequested(AiStateId factionId, RulerStrategyTrigger trigger, string subject) =>
            Request(factionId, trigger, subject);

        private void TryDispatch()
        {
            if (activeCoroutine != null || mode == RulerAiModes.Deterministic) return;
            activeRequest = coordinator.PendingRequests().FirstOrDefault(request =>
                dispatched.Add(RequestKey(request)));
            if (activeRequest == null) return;
            var providers = mode == RulerAiModes.Cloud
                ? new[] { RulerStrategyProviderKind.CloudApi, RulerStrategyProviderKind.Ollama }
                : new[] { RulerStrategyProviderKind.Ollama };
            activeCoroutine = StartCoroutine(ExecuteProviderChain(activeRequest, providers));
        }

        private IEnumerator ExecuteProviderChain(RulerStrategyRequest request,
            IReadOnlyList<RulerStrategyProviderKind> providers)
        {
            var lastProvider = RulerStrategyProviderKind.Deterministic;
            var lastStatus = RulerStrategyRequestStatus.Unavailable;
            var lastError = "No configured strategy provider is available.";
            foreach (var provider in providers)
            {
                lastProvider = provider;
                RulerProviderResponse response = null;
                yield return Send(provider, request, value => response = value);
                if (response != null && response.Succeeded)
                {
                    try
                    {
                        var plan = RulerStrategyJsonCodec.ParsePlan(response.Content, request,
                            provider, calendarTick());
                        if (coordinator.TryAccept(request.FactionId, plan, calendarTick(), out var rejection))
                        {
                            LastStatus = provider + ": " + plan.Summary;
                            CompleteRequest();
                            yield break;
                        }
                        lastStatus = RulerStrategyRequestStatus.Rejected;
                        lastError = rejection;
                    }
                    catch (Exception exception) when (exception is ArgumentException ||
                                                       exception is InvalidOperationException ||
                                                       exception is FormatException)
                    {
                        lastStatus = RulerStrategyRequestStatus.Rejected;
                        lastError = exception.Message;
                    }
                }
                else
                {
                    lastStatus = response != null && response.TimedOut
                        ? RulerStrategyRequestStatus.TimedOut : RulerStrategyRequestStatus.Unavailable;
                    lastError = response?.Error ?? "Provider returned no response.";
                }
            }
            coordinator.CompleteFailure(request.FactionId, calendarTick(), lastProvider, lastStatus, lastError);
            LastStatus = lastProvider + " unavailable; deterministic strategy remains active.";
            CompleteRequest();
        }

        private IEnumerator Send(RulerStrategyProviderKind provider, RulerStrategyRequest request,
            Action<RulerProviderResponse> complete)
        {
            var url = provider == RulerStrategyProviderKind.Ollama
                ? Environment.GetEnvironmentVariable("SERENITY_OLLAMA_URL") ?? "http://localhost:11434/api/chat"
                : Environment.GetEnvironmentVariable("SERENITY_LLM_GATE_URL") ?? "https://api.llm-gate.tech/v1/chat/completions";
            var apiKey = provider == RulerStrategyProviderKind.CloudApi
                ? Environment.GetEnvironmentVariable("LLM_GATE_API_KEY") : null;
            if (provider == RulerStrategyProviderKind.CloudApi && string.IsNullOrWhiteSpace(apiKey))
            {
                complete(new RulerProviderResponse(false, false, null,
                    "LLM_GATE_API_KEY is not available to the game process."));
                yield break;
            }
            var body = provider == RulerStrategyProviderKind.Ollama
                ? RulerStrategyJsonCodec.CreateOllamaRequest(request,
                    Environment.GetEnvironmentVariable("SERENITY_OLLAMA_MODEL") ?? "qwen3.5:4b")
                : RulerStrategyJsonCodec.CreateCloudRequest(request,
                    Environment.GetEnvironmentVariable("SERENITY_CLOUD_MODEL") ?? "gpt-6-luna");
            using (var web = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                web.downloadHandler = new DownloadHandlerBuffer();
                web.timeout = provider == RulerStrategyProviderKind.Ollama ? 60 : 20;
                web.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                if (provider == RulerStrategyProviderKind.CloudApi)
                    web.SetRequestHeader("Authorization", "Bearer " + apiKey);
                yield return web.SendWebRequest();
                var timedOut = web.result == UnityWebRequest.Result.ConnectionError &&
                    (web.error ?? string.Empty).IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
                if (web.result != UnityWebRequest.Result.Success)
                {
                    complete(new RulerProviderResponse(false, timedOut, null,
                        "HTTP " + web.responseCode + ": " + (web.error ?? "request failed")));
                    yield break;
                }
                try
                {
                    var content = provider == RulerStrategyProviderKind.Ollama
                        ? RulerStrategyJsonCodec.ReadOllamaContent(web.downloadHandler.text)
                        : RulerStrategyJsonCodec.ReadCloudContent(web.downloadHandler.text);
                    complete(new RulerProviderResponse(true, false, content, null));
                }
                catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
                {
                    complete(new RulerProviderResponse(false, false, null, exception.Message));
                }
            }
        }

        private void CompleteRequest()
        {
            activeCoroutine = null;
            activeRequest = null;
        }

        private static string RequestKey(RulerStrategyRequest request) =>
            request.FactionId + ":" + request.Pending.Sequence;
    }

    internal sealed class RulerProviderResponse
    {
        public RulerProviderResponse(bool succeeded, bool timedOut, string content, string error)
        { Succeeded = succeeded; TimedOut = timedOut; Content = content; Error = error; }
        public bool Succeeded { get; }
        public bool TimedOut { get; }
        public string Content { get; }
        public string Error { get; }
    }

    public static class RulerStrategyJsonCodec
    {
        [Serializable] private sealed class Message { public string role; public string content; }
        [Serializable] private sealed class OllamaOptions { public int num_ctx = 2048; public int num_predict = 300; public float temperature = 0.1f; }
        [Serializable] private sealed class OllamaRequest { public string model; public Message[] messages; public bool stream; public bool think; public string format; public OllamaOptions options; }
        [Serializable] private sealed class CloudFormat { public string type = "json_object"; }
        [Serializable] private sealed class CloudRequest { public string model; public Message[] messages; public string reasoning_effort = "none"; public CloudFormat response_format = new CloudFormat(); }
        [Serializable] private sealed class OllamaResponse { public Message message; }
        [Serializable] private sealed class CloudChoice { public Message message; }
        [Serializable] private sealed class CloudResponse { public CloudChoice[] choices; }
        [Serializable] private sealed class Workforce { public int food; public int materials; public int construction; public int research; public int military; }
        [Serializable] private sealed class PlanDto
        {
            public string goal;
            public int food_target;
            public int material_target;
            public int minimum_army_size;
            public int aggression;
            public string diplomatic_posture;
            public Workforce workforce;
            public string[] building_priorities;
            public string summary;
        }
        [Serializable] private sealed class ResourceDto { public string id; public long amount; }
        [Serializable] private sealed class RelationDto { public string side; public int score; }
        [Serializable] private sealed class ContextDto
        {
            public long request_sequence;
            public string trigger;
            public long game_tick;
            public string subject;
            public int population;
            public int settlements;
            public int military_units;
            public string current_goal;
            public ResourceDto[] resources;
            public string[] completed_technologies;
            public string[] available_technologies;
            public string[] allowed_building_priorities;
            public RelationDto[] relations;
        }

        public static string CreateOllamaRequest(RulerStrategyRequest request, string model)
        {
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException(nameof(model));
            return JsonUtility.ToJson(new OllamaRequest
            {
                model = model,
                messages = Messages(request),
                stream = false,
                think = false,
                format = "json",
                options = new OllamaOptions()
            });
        }

        public static string CreateCloudRequest(RulerStrategyRequest request, string model)
        {
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException(nameof(model));
            return JsonUtility.ToJson(new CloudRequest { model = model, messages = Messages(request) });
        }

        public static string ReadOllamaContent(string json)
        {
            var response = JsonUtility.FromJson<OllamaResponse>(json);
            if (response?.message == null || string.IsNullOrWhiteSpace(response.message.content))
                throw new FormatException("Ollama response contains no message content.");
            return response.message.content;
        }

        public static string ReadCloudContent(string json)
        {
            var response = JsonUtility.FromJson<CloudResponse>(json);
            if (response?.choices == null || response.choices.Length == 0 ||
                response.choices[0]?.message == null || string.IsNullOrWhiteSpace(response.choices[0].message.content))
                throw new FormatException("Cloud response contains no message content.");
            return response.choices[0].message.content;
        }

        public static RulerStrategyPlan ParsePlan(string json, RulerStrategyRequest request,
            RulerStrategyProviderKind provider, long completedTick)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Strategy response is empty.");
            var dto = JsonUtility.FromJson<PlanDto>(json);
            if (dto == null || dto.workforce == null) throw new FormatException("Strategy JSON is incomplete.");
            return new RulerStrategyPlan(request.Pending.Sequence, completedTick,
                checked(request.Pending.RequestedTick + RulerStrategyState.AnnualIntervalTicks), provider,
                ParseGoal(dto.goal), dto.food_target, dto.material_target, dto.minimum_army_size,
                dto.aggression, ParsePosture(dto.diplomatic_posture), dto.workforce.food,
                dto.workforce.materials, dto.workforce.construction, dto.workforce.research,
                dto.workforce.military, dto.building_priorities, dto.summary ?? string.Empty);
        }

        private static Message[] Messages(RulerStrategyRequest request)
        {
            var system = "You are the strategic planner for a game settlement. Return one JSON object only, " +
                "without Markdown or commentary. Plan aggregate cohorts; never list individual residents. " +
                "All enum values and content IDs below are machine tokens: copy them exactly in English ASCII " +
                "and NEVER translate, rename, inflect, or invent them. goal must be exactly one of: " +
                "stabilize_economy, advance_research, develop_settlement, form_army, attack_settlement, recover. " +
                "diplomatic_posture must be exactly one of: isolationist, neutral, cooperative, expansionist. " +
                "Required fields: goal, food_target, material_target, minimum_army_size, aggression (0..100), " +
                "diplomatic_posture, workforce {food,materials,construction,research,military} totaling exactly 100, " +
                "building_priorities copied verbatim only from allowed_building_priorities, and summary. " +
                "Write summary in Russian, at most 512 characters.";
            var context = new ContextDto
            {
                request_sequence = request.Pending.Sequence,
                trigger = request.Pending.Trigger.ToString(),
                game_tick = request.Pending.RequestedTick,
                subject = request.Pending.Subject,
                population = request.Context.Population,
                settlements = request.Context.SettlementCount,
                military_units = request.Context.MilitaryUnitCount,
                current_goal = request.Context.CurrentGoal.ToString(),
                resources = request.Context.Resources.Select(value => new ResourceDto
                    { id = value.ResourceId, amount = value.Amount }).ToArray(),
                completed_technologies = request.Context.CompletedTechnologies.ToArray(),
                available_technologies = request.Context.AvailableTechnologies.ToArray(),
                allowed_building_priorities = request.Context.AvailableBuildingPriorities.ToArray(),
                relations = request.Context.Relations.Select(value => new RelationDto
                    { side = value.SideId, score = value.Score }).ToArray()
            };
            return new[] { new Message { role = "system", content = system },
                new Message { role = "user", content = JsonUtility.ToJson(context) } };
        }

        private static AiStrategicGoal ParseGoal(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "stabilize_economy": return AiStrategicGoal.StabilizeEconomy;
                case "advance_research": return AiStrategicGoal.AdvanceResearch;
                case "develop_settlement": return AiStrategicGoal.DevelopSettlement;
                case "form_army": return AiStrategicGoal.FormArmy;
                case "attack_settlement": return AiStrategicGoal.AttackSettlement;
                case "recover": return AiStrategicGoal.Recover;
                default: throw new FormatException("Unknown strategy goal.");
            }
        }

        private static RulerDiplomaticPosture ParsePosture(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "isolationist": return RulerDiplomaticPosture.Isolationist;
                case "neutral": return RulerDiplomaticPosture.Neutral;
                case "cooperative": return RulerDiplomaticPosture.Cooperative;
                case "expansionist": return RulerDiplomaticPosture.Expansionist;
                default: throw new FormatException("Unknown diplomatic posture.");
            }
        }
    }
}
