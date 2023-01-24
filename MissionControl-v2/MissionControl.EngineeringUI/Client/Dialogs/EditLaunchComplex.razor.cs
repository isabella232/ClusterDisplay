using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class EditLaunchComplex
    {
        [Parameter]
        public LaunchComplex ToEdit { get; set; } = new (Guid.Empty);

        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        HttpClient HttpClient { get; set; } = default!;
        [Inject]
        ComplexesService Complexes { get; set; } = default!;

        LaunchComplex Edited { get; set; } = new (Guid.Empty);

        string EditedHangarBayEndpoint {
            get => m_EditedHangarBayEndpoint;
            set
            {
                if (m_EditedHangarBayEndpoint != value)
                {
                    m_EditedHangarBayEndpoint = value;
                    if (Uri.TryCreate(m_EditedHangarBayEndpoint, UriKind.Absolute, out var parsedUri) &&
                        parsedUri.Scheme == "http")
                    {
                        EditedHangarBayEndpointErrorMessage = "";
                    }
                    else
                    {
                        EditedHangarBayEndpointErrorMessage = $"Uri format invalid, must be http://#.#.#.#:#/.";
                    }
                }
            }
        }
        string m_EditedHangarBayEndpoint = "";

        string EditedHangarBayEndpointErrorMessage { get; set; } = "";
        string EditedHangarBayIdentifierErrorMessage { get; set; } = "";
        IList<MissionControl.LaunchPad>? SelectedLaunchPads { get; set; }
        bool IsValid => EditedHangarBayEndpointErrorMessage == "" &&
            EditedHangarBayIdentifierErrorMessage == "" && Edited.HangarBay.Identifier != Guid.Empty;

        RadzenDataGrid<MissionControl.LaunchPad> m_LaunchpadsGrid = default!;

        protected override void OnInitialized()
        {
            Edited = ToEdit.DeepClone();
            EditedHangarBayEndpoint = Edited.HangarBay.Endpoint.ToString();
        }

        Task OnValidateHangarBayEndpoint()
        {
            return DialogService.ShowBusy($"Contacting {EditedHangarBayEndpoint}...", async () =>
            {
                // Contact the hangar bay, get its config and identifier
                try
                {
                    Edited.HangarBay.Identifier = ToEdit.HangarBay.Identifier;

                    var hangarBayConfig = await HttpClient.GetFromJsonAsync<HangarBay.Config>(
                        new Uri(new Uri(EditedHangarBayEndpoint), "api/v1/config"));
                    if (ToEdit.HangarBay.Identifier != Guid.Empty && hangarBayConfig.Identifier != ToEdit.Id)
                    {
                        EditedHangarBayIdentifierErrorMessage = $"Hangar bay identifier changed from " +
                            $"{ToEdit.HangarBay.Identifier} to {hangarBayConfig.Identifier} indicating this is not " +
                            $"the same hangar bay.  Delete the launch complex and create a new one instead.";
                    }
                    else if (ToEdit.HangarBay.Identifier == Guid.Empty &&
                             Complexes.Collection.TryGetValue(hangarBayConfig.Identifier, out var alreadyExist))
                    {
                        EditedHangarBayIdentifierErrorMessage = $"Launch complex \"{alreadyExist.Name}\" is already " +
                            $"connected to this hangar bay.";
                    }
                    else
                    {
                        Edited.HangarBay.Identifier = hangarBayConfig.Identifier;
                        EditedHangarBayIdentifierErrorMessage = "";
                    }
                }
                catch (Exception)
                {
                    EditedHangarBayIdentifierErrorMessage = $"Failed to contact {EditedHangarBayEndpoint}.";
                }
            });
        }

        async Task AddLaunchPad()
        {
            var ret = await DialogService.OpenAsync<EditLaunchPad>($"Add a new Launchpad",
               new Dictionary<string, object>{ {"ParentComplex", Edited} },
               new DialogOptions() { Width = "50%", Height = "50%", Resizable = true, Draggable = true });
            if (ret == null)
            {   // Cancel or dialog closed
                return;
            }

            Edited.LaunchPads = Edited.LaunchPads.Append((MissionControl.LaunchPad)ret).ToList();
            await m_LaunchpadsGrid.Reload();
        }

        async Task EditLaunchPad()
        {
            var toEdit = SelectedLaunchPads?.FirstOrDefault();
            if (toEdit == null)
            {
                return;
            }

            var ret = await DialogService.OpenAsync<EditLaunchPad>($"Edit {toEdit.Name}",
               new Dictionary<string, object>{ {"ToEdit", toEdit}, {"ParentComplex", Edited} },
               new DialogOptions() { Width = "50%", Height = "50%", Resizable = true, Draggable = true });
            if (ret == null)
            {   // Cancel or dialog closed
                return;
            }
            var updated = (MissionControl.LaunchPad)ret;

            Edited.LaunchPads = Edited.LaunchPads
                .Select(lp => lp.Identifier == updated.Identifier ? updated : lp).ToList();
            await m_LaunchpadsGrid.Reload();
        }

        void DeleteLaunchPad()
        {
            var toDelete = SelectedLaunchPads?.FirstOrDefault();
            if (toDelete == null)
            {
                return;
            }

            Edited.LaunchPads = Edited.LaunchPads.Where(lp => lp.Identifier != toDelete.Identifier).ToList();
            m_LaunchpadsGrid.Reload();
        }

        async Task OnOk()
        {
            await OnValidateHangarBayEndpoint();
            if (!IsValid)
            {
                return;
            }

            LaunchComplex toPut = new(Edited.HangarBay.Identifier);
            toPut.DeepCopyFrom(Edited);
            toPut.HangarBay.Endpoint = new Uri(EditedHangarBayEndpoint);

            await DialogService.ShowBusy($"Updating MissionControl...", () => Complexes.PutAsync(toPut));

            DialogService.Close();
        }

        void OnCancel()
        {
            DialogService.Close();
        }
    }
}