using eHub.Contracts;
using eHub.Contracts.Manager;
using eHub.Scripting.Connectors;
using eHub.Scripting.Controllers;
using eMessenger;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace eHub.Messaging;

public class MessengerEvents : IHostedService
{
    private readonly IMessenger _messenger;
    private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
    private readonly ConnectorManager _connectorManager;
    private readonly MessagingContext _messagingContext;
    private readonly IConfiguration _configuration;
    private readonly string _ownInstanceString;

    public MessengerEvents(IMessenger messenger, ConnectorManager connectorManager,
        IActionDescriptorCollectionProvider actionDescriptorCollectionProvider, MessagingContext messagingContext,
        IConfiguration configuration)
    {
        _messenger = messenger;
        _connectorManager = connectorManager;
        _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
        _messagingContext = messagingContext;
        _configuration = configuration;
        _ownInstanceString = _messagingContext.OwnId.ToString();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _messenger.AnswerAsync<ConnectorByName, ConnectorCreateResult>(ConnectorContract.EHub_Action_Start_Connector(_ownInstanceString),
            async (message) => await _connectorManager.StartConnectorByTemplate(message.Name));

        await _messenger.ListenAsync<ConnectorByName>(ConnectorContract.EHub_Action_Stop_Connector(_ownInstanceString),
            async (message) => await _connectorManager.StopConnectorByName(message.Name));

        //await _messenger.AnswerAsync(ConnectorContract.EHub_RequestInstance, 
        //    () => new KeyValuePair<string, string?>(_ownInstanceString, _configuration["econfig-displayname"]));

        await _messenger.AnswerAsync(ConnectorContract.EHub_RequestConnectorSummary(_ownInstanceString), GetConnectorSummaryByInstanceName);

        await _messenger.AnswerAsync(ConnectorContract.EHub_RequestControllerRouteModels(_ownInstanceString), GetControllerModelsByInstanceName);

        // TODO 

        //await _messenger.AnswerAsync<ManagerContracts>(ConnectorContract.EHubGetOverview,
        //    async () => new ManagerContracts());

        //await _messenger.AnswerAsync<ManagerContracts>(Topic.Join(ConnectorContract.EHubGetOverview, _ownInstanceString),
        //    async () => new ManagerContracts());
    }

    private ConnectorInstance GetConnectorSummaryByInstanceName()
    {
        return new ConnectorInstance(_ownInstanceString,
            new(
                _connectorManager.ActiveConnectors,
                _connectorManager.AvailableConnectorTypes,
                _connectorManager.AllConnectorTemplates
                )
            );
    }

    private ControllerInstance GetControllerModelsByInstanceName()
    {
        return new ControllerInstance(
            _ownInstanceString, _actionDescriptorCollectionProvider.ActionDescriptors.Items
                .Where(ad => ad.AttributeRouteInfo != null && ad.EndpointMetadata.OfType<DynamicActionAttribute>().Any())
                .Select(ad =>
                {
                    var cad = ad as ControllerActionDescriptor;
                    return new RouteModel(
                        ad.AttributeRouteInfo!.Template,
                        ad.EndpointMetadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods ?? Array.Empty<string>(),
                        ad.DisplayName,
                        cad?.ControllerName,
                        cad?.ActionName);
                }).ToList());
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
