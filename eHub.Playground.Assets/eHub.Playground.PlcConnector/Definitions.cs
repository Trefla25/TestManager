using eController.TransportInterface.Shared.Telegram;
using eController.TransportInterface.Shared;
using Microsoft.Extensions.Logging;
using eController.CommonLib.Telegram;

namespace eHub.Playground.PlcConnector
{
    public class Definitions
    {
        public static ITelegramConverter<DefaultTelegramHeader> RecvConverter { get; private set; } = default!;

        // TODO This isn't the nicest way to initialize.
        // Use a static plugin initializer when eHub supports it in the future.
        public static void Init(ILoggerFactory loggerFactory)
        {
            RecvConverter = new TelegramConverterBuilder<DefaultTelegramHeader>(loggerFactory)
                .WithAllowDifferentTelegramSizes(false)
                .WithFixedTelegramSize(100)
                .WithEofSequence([(byte)'~', 3])
                .Register<DefaultTransportTelegramDef>(
                    TransportInterfaceDefinitions.TU_TEL_TYPE_MESSAGE_POINT,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_TRANSPORT_ORDER,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_PLACE_DATA,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_SYSTEM_LEFT,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_SYSTEM_ENTERED,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_TRANSPORT_REQUEST,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_TRANSPORT_MESSAGE)
                .Register<DefaultStatusTelegramDef>(
                    TransportInterfaceDefinitions.TU_TEL_TYPE_STATUS,
                    TransportInterfaceDefinitions.TU_TEL_TYPE_STATUS_REQUEST)
                .Build();
        }
    }
}
