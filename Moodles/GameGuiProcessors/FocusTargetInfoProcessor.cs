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
public unsafe class FocusTargetInfoProcessor
{
    public int NumVanillaStatuses = 0;

    public FocusTargetInfoProcessor()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_FocusTargetInfo", OnFocusTargetInfoUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_FocusTargetInfo", OnFocusTargetInfoRequestedUpdate);
        if(LocalPlayer.Available && TryGetAddonByName<AtkUnitBase>("_FocusTargetInfo", out var addon) && IsAddonReady(addon))
        {
            AddonRequestedUpdate(addon);
        }
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_FocusTargetInfo", OnFocusTargetInfoUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_FocusTargetInfo", OnFocusTargetInfoRequestedUpdate);
    }

    public void HideAll()
    {
        if(TryGetAddonByName<AtkUnitBase>("_FocusTargetInfo", out var addon) && IsAddonReady(addon))
        {
            UpdateAddon(addon, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage 
    private void OnFocusTargetInfoRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if(P == null) return;
        if(addonBase == null || !IsAddonReady(addonBase)) return;

        NumVanillaStatuses = 0;

        var target = TargetSystem.Instance()->FocusTarget;
        if(target is null || !target->IsCharacter() || target->ObjectKind is not CSObjectKind.Pc) return;

        var chara = (Character*)target;
        var statusList = StatusList.CreateStatusListReference((nint)chara->GetStatusManager())!;
        if(statusList != null) NumVanillaStatuses = statusList.Count(x => x.StatusId != 0 && !P.CommonProcessor.SpecialStatuses.Contains(x.StatusId));

        var nonSpecial = chara->MyStatusManager().Statuses.Count(x => x.Type != StatusType.Special);
        InternalLog.Verbose($"FocusTarget Requested update: ({NumVanillaStatuses} - {nonSpecial})");
    }

    private void OnFocusTargetInfoUpdate(AddonEvent type, AddonArgs args)
    {
        if(P == null) return;
        if(!LocalPlayer.Available) return;
        if(!P.CanModifyUI()) return;
        UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    public void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        var target = TargetSystem.Instance()->FocusTarget;
        if(target is null || !target->IsCharacter() || target->ObjectKind is not CSObjectKind.Pc) return;
        if(addon is null || !IsAddonReady(addon)) return;

        var sm = ((Character*)target)->MyStatusManager();
        var nonSpeialCnt = sm.Statuses.Count(x => x.Type != StatusType.Special);
        var baseCnt = 8 - NumVanillaStatuses;
        var endCnt = Math.Max(4, baseCnt - nonSpeialCnt - P.CommonProcessor.RemovedThisTick + 1);

        for(var i = baseCnt; i >= endCnt; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if(c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
        }

        if(!hideAll)
        {
            foreach(var x in sm.Statuses)
            {
                if(x.Type == StatusType.Special) continue;
                if(baseCnt < endCnt) break;
                var rem = x.ExpiresAt - Utils.Time;
                if(rem > 0)
                {
                    SetIcon(addon, baseCnt, x);
                    baseCnt--;
                }
            }
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, MyStatus status)
    {
        var container = addon->UldManager.NodeList[index];
        P.CommonProcessor.SetIcon(addon, container, status);
    }
}
