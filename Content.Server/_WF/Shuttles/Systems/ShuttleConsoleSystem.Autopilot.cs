using Content.Server._WF.Shuttles.Components;
using Content.Server._WF.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private readonly AutopilotSystem _autopilot = default!;

    private void InitializeAutopilot()
    {
        SubscribeLocalEvent<ShuttleConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAutopilotVerb);
    }

    private void OnGetAutopilotVerb(EntityUid uid, ShuttleConsoleComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Get the shuttle this console controls
        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = uid,
        };
        RaiseLocalEvent(uid, ref getShuttleEv);
        var shuttleUid = getShuttleEv.Console;

        if (shuttleUid == null)
            return;

        if (!TryComp<TransformComponent>(shuttleUid, out var shuttleXform))
            return;

        var shuttleGridUid = shuttleXform.GridUid;
        if (shuttleGridUid == null)
            return;

        // Check if autopilot component exists and is enabled
        var hasAutopilot = TryComp<AutopilotComponent>(shuttleGridUid.Value, out var autopilotComp);
        var isEnabled = hasAutopilot && autopilotComp!.Enabled;

        AlternativeVerb verb = new()
        {
            Act = () => ToggleAutopilot(args.User, uid, shuttleGridUid.Value),
            Text = isEnabled ? Loc.GetString("shuttle-console-autopilot-disable") : Loc.GetString("shuttle-console-autopilot-enable"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 1,
        };

        args.Verbs.Add(verb);
    }

    private void ToggleAutopilot(EntityUid user, EntityUid consoleUid, EntityUid shuttleUid)
    {
        if (!TryComp<ShuttleConsoleComponent>(consoleUid, out _))
            return;

        if (!TryComp<ShuttleComponent>(shuttleUid, out _))
            return;

        // Check if autopilot is currently enabled
        var hasAutopilot = TryComp<AutopilotComponent>(shuttleUid, out var autopilotComp);
        var isEnabled = hasAutopilot && autopilotComp!.Enabled;
        if (isEnabled)
        {
            _autopilot.DisableAutopilot(shuttleUid);
            _popup.PopupEntity(Loc.GetString("shuttle-console-autopilot-disabled"), user, user);
            return;
        }

        // Try to get the target from the radar console
        if (!TryComp<RadarConsoleComponent>(consoleUid, out var radarConsoleComponent))
        {
            _popup.PopupEntity(Loc.GetString("shuttle-console-autopilot-no-target"), user, user);
            return;
        }

        // First try to use entity target
        var targetEntity = radarConsoleComponent.TargetEntity;
        if (
            targetEntity != null &&
            targetEntity.Value.IsValid() &&
            TryComp<TransformComponent>(targetEntity.Value, out var targetXform)
        )
        {
            var targetCoords = _transform.GetMapCoordinates(targetXform);
            EnableAutopilot(user, shuttleUid, targetCoords);
            return;
        }

        // Otherwise try to use manual coordinate target
        var manualTarget = radarConsoleComponent.Target;
        if (manualTarget != null && TryComp<TransformComponent>(consoleUid, out var consoleXform))
        {
            var targetCoords = new MapCoordinates(manualTarget.Value, consoleXform.MapID);
            EnableAutopilot(user, shuttleUid, targetCoords);
            return;
        }

        _popup.PopupEntity(Loc.GetString("shuttle-console-autopilot-no-target"), user, user);
    }

    private void EnableAutopilot(EntityUid user, EntityUid shuttleUid, MapCoordinates targetCoords)
    {
        _autopilot.EnableAutopilot(shuttleUid, targetCoords);
        _popup.PopupEntity(Loc.GetString("shuttle-console-autopilot-enabled"), user, user);
    }
}
