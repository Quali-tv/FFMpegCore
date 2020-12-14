using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore.Pipes;

namespace FFMpegCore.Arguments
{
    public class OutputPipeArgument : PipeArgument, IOutputArgument
    {
        public readonly IPipeSink Reader;

        public OutputPipeArgument(IPipeSink reader) : base(true)
        {
            Reader = reader;
        }

        public override string Text => $"\"{PipePath}\" -y";

        protected override async Task ProcessDataAsync(CancellationToken token)
        {
            var stream = await GetStreamAsync();
            await Reader.ReadAsync(stream, token).ConfigureAwait(false);
        }
    }
}
