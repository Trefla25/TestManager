using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eHub.PlugIn;

namespace eHub.Playground.ScriptsMultipleDbContexts;
public class MyConverter : IPacketConverter
{
    public ValueTask<string> BytesToUIDataConverter(ReadOnlyMemory<byte> bytes) => throw new NotImplementedException();
}
