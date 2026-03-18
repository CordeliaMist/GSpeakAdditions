using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Moodles.Data;
using CSObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;


namespace Moodles.GameGuiProcessors;
public unsafe class TargetInfoBuffDebuffProcessor
{
    public int NumVanillaStatuses = 0;
    public TargetInfoBuffDebuffProcessor()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffRequestedUpdate);
        if(LocalPlayer.Available && TryGetAddonByName<AtkUnitBase>("_TargetInfoBuffDebuff", out var addon) && IsAddonReady(addon))
        {
            AddonRequestedUpdate(addon);
        }
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffRequestedUpdate);
    }

    public void HideAll()
    {
        if(TryGetAddonByName<AtkUnitBase>("_TargetInfoBuffDebuff", out var addon) && IsAddonReady(addon))
        {
            UpdateAddon(addon, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private void OnTargetInfoBuffDebuffRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    
    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if(P == null) return;
        if(addonBase == null || !IsAddonReady(addonBase)) return;

        NumVanillaStatuses = 0;
        // Iterate through and identify the target, if a character.
        var ts = TargetSystem.Instance();
        var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
        if(target is null || !target->IsCharacter() || target->ObjectKind is not CSObjectKind.Pc) return;

        // Determine the total vanilla status effects.
        var chara = (Character*)target;
        var statusList = StatusList.CreateStatusListReference((nint)chara->GetStatusManager())!;
        if(statusList != null) NumVanillaStatuses = statusList.Count(x => x.StatusId != 0);

        // Then obtain the number of our statuses via our status manager.
        InternalLog.Verbose($"TargetInfo Requested update: ({NumVanillaStatuses} - {chara->MyStatusManager().Statuses.Count})");
    }

    private void OnTargetInfoBuffDebuffUpdate(AddonEvent type, AddonArgs args)
    {
        if(P == null) return;
        if(!LocalPlayer.Available) return;
        if(!P.CanModifyUI()) return;
        UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    // Didn't really know how to transfer to get the DalamudStatusList from here, so had to use IPlayerCharacter.
    public unsafe void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        var ts = TargetSystem.Instance();
        var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
        if(target is null || !target->IsCharacter() || target->ObjectKind is not CSObjectKind.Pc) return;
        if(addon == null || !IsAddonReady(addon)) return;

        var sm = ((Character*)target)->MyStatusManager();
        // The count to start and end clearing at.
        var baseCnt = 3 + NumVanillaStatuses;
        var endCnt = Math.Min(baseCnt + sm.Statuses.Count + P.CommonProcessor.RemovedThisTick - 1, 32);

        for(var i = baseCnt; i <= endCnt; i++)
        {
            var c = addon->UldManager.SearchNodeById((uint)i);
            if(c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
        }
        if(!hideAll)
        {
            foreach(var x in sm.Statuses)
            {
                if(baseCnt > endCnt) break;
                var rem = x.ExpiresAt - Utils.Time;
                if(rem > 0)
                {
                    SetIcon(addon, baseCnt, x);
                    baseCnt++;
                }
            }
        }
    }

    private void SetIcon(AtkUnitBase* addon, int id, MyStatus status)
    {
        var container = addon->UldManager.SearchNodeById((uint)id);
        P.CommonProcessor.SetIcon(addon, container, status);
    }


}
