using System.Linq;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.Objectives;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Shared._DV.CCVars;
using Content.Shared._DV.CustomObjectiveSummary;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._DV.CustomObjectiveSummary;

public sealed class CustomObjectiveSummarySystem : EntitySystem
{
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    // [Dependency] private readonly SharedFeedbackOverwatchSystem _feedback = default!; // Frontier
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Frontier
    [Dependency] private readonly ObjectivesSystem _objectives = default!; // Frontier
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!; // Wayfarer
    [Dependency] private readonly IServerDbManager _db = default!; // Wayfarer
    [Dependency] private readonly PlayTimeTrackingManager _playtime = default!; // Wayfarer
    [Dependency] private readonly SharedJobSystem _jobs = default!; // Wayfarer
    [Dependency] private readonly SharedGameTicker _gameTicker = default!; // Wayfarer

    private int _maxLengthSummaryLength; // Frontier: moved from ObjectiveSystem
    private int _minPlaytimeMinutes; // Wayfarer: minimum playtime to write stories
    private Dictionary<NetUserId, PlayerStory> _stories = new(); // Frontier: store one story per user per round

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacShuttleLeftEvent>(OnEvacShuttleLeft);
        // SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd); // Frontier
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestarted); // Frontier

        _net.RegisterNetMessage<CustomObjectiveClientSetObjective>(OnCustomObjectiveFeedback);

        Subs.CVar(_cfg, DCCVars.MaxObjectiveSummaryLength, len => _maxLengthSummaryLength = len, true); // Frontier: moved from ObjectiveSystem
        Subs.CVar(_cfg, DCCVars.MinPlayerStoryPlaytimeMinutes, minutes => _minPlaytimeMinutes = minutes, true); // Wayfarer
    }

    private async void OnCustomObjectiveFeedback(CustomObjectiveClientSetObjective msg)
    {
        if (!_mind.TryGetMind(msg.MsgChannel.UserId, out var mind) || mind is not { } mindEnt)
            return;

        // Check round playtime requirement
        if (!_player.TryGetSessionById(msg.MsgChannel.UserId, out var session))
            return;

        var roundDuration = _gameTicker.RoundDuration();
        if (roundDuration.TotalMinutes < _minPlaytimeMinutes)
            return;

        // Get plain character name without markup
        var characterName = mind.Value.Comp.CharacterName ?? Loc.GetString("custom-objective-unknown-name");
        
        // Get job/role name
        var roleName = "Unknown";
        if (_jobs.MindTryGetJob(mind.Value, out var job))
            roleName = Loc.GetString(job.Name);
        
        // Get profile ID for this character
        int? profileId = null;
        var prefs = _prefsManager.GetPreferences(msg.MsgChannel.UserId);
        if (prefs != null)
        {
            var characterSlot = prefs.SelectedCharacterIndex;
            profileId = await _db.GetProfileIdAsync(msg.MsgChannel.UserId, characterSlot);
        }
        
        if (_stories.TryGetValue(msg.MsgChannel.UserId, out var story))
        {
            story.CharacterName = characterName;
            story.Story = msg.Summary;
            story.ProfileId = profileId;
            story.RoleName = roleName;
        }
        else
        {
            _stories[msg.MsgChannel.UserId] = new PlayerStory(characterName, msg.Summary, profileId, roleName);
        }

        // Ensure that the current mind has their summary setup (so they can come back to it if disconnected)
        var comp = EnsureComp<CustomObjectiveSummaryComponent>(mind.Value);

        comp.ObjectiveSummary = msg.Summary;
        Dirty(mind.Value.Owner, comp);

        _adminLog.Add(LogType.ObjectiveSummary, $"{ToPrettyString(mind.Value.Comp.OwnedEntity)} wrote objective summary: {msg.Summary}");
    }

    private void OnEvacShuttleLeft(EvacShuttleLeftEvent args)
    {
        var allMinds = _mind.GetAliveHumans();
        var roundDuration = _gameTicker.RoundDuration();

        foreach (var mind in allMinds)
        {
            if (!_player.TryGetSessionById(mind.Comp.UserId, out var session))
                continue;

            // Check round playtime requirement
            if (roundDuration.TotalMinutes < _minPlaytimeMinutes)
                continue;

            RaiseNetworkEvent(new CustomObjectiveSummaryOpenMessage(), session);
        }
    }

    // Frontier: unneeded
    /*
    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        var allMinds = _mind.GetAliveHumans();

        foreach (var mind in allMinds)
        {
            if (mind.Comp.Objectives.Count == 0)
                continue;

            _feedback.SendPopupMind(mind, "RemoveGreentextPopup");
        }
    }
    */
    // End Frontier: unneeded

    // Frontier: custom objective text
    public string GetCustomObjectiveText()
    {
        StringBuilder objectiveText = new();

        foreach (var story in _stories.Values)
        {
            story.Story.Trim();
            if (story.Story.Length > _maxLengthSummaryLength)
                story.Story = story.Story.Substring(0, _maxLengthSummaryLength);

            objectiveText.AppendLine(Loc.GetString("custom-objective-intro", ("title", story.CharacterName)));
            objectiveText.AppendLine(Loc.GetString("custom-objective-format", ("line", FormattedMessage.EscapeText(story.Story))));
            objectiveText.AppendLine("");
        }
        return objectiveText.ToString();
    }

    // Frontier: get raw player stories for database storage
    public IReadOnlyDictionary<NetUserId, (string CharacterName, string Story, int? ProfileId, string RoleName)> GetPlayerStories()
    {
        return _stories.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.CharacterName, kvp.Value.Story, kvp.Value.ProfileId, kvp.Value.RoleName)
        );
    }

    private void OnRoundRestarted(RoundRestartCleanupEvent args)
    {
        _stories.Clear();
    }

    sealed class PlayerStory(string characterName, string story, int? profileId = null, string roleName = "Unknown")
    {
        public string CharacterName = characterName;
        public string Story = story;
        public int? ProfileId = profileId;
        public string RoleName = roleName;
    }
    // End Frontier
}
