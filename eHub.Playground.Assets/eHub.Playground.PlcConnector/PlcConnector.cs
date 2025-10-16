using eController.CommonLib.Telegram;
using eController.ProjectToolbox.Tcp.Endpoints;
using eController.TransportInterface.Ati;
using eController.TransportInterface.Ati.Configuration;
using eController.TransportInterface.Ati.Services;
using eController.TransportInterface.Shared.Telegram;
using eMessenger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eHub.Playground.PlcConnector
{
    public class PlcConnector : ATConnectorBase
    {
        public PlcConnector(IOptions<AtConnectorConfig> options, ILogger<PlcConnector> logger, ILoggerFactory loggerFactory, IScopedMessenger messenger) : base(options, logger, loggerFactory, messenger)
        {
            Definitions.Init(loggerFactory);
            Converter = new PacketConverter(MessageConverter);

        }

        public override ITelegramConverter<DefaultTelegramHeader> MessageConverter => Definitions.RecvConverter;
    }
}
