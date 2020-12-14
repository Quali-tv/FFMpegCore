using System.Threading;
using System.Threading.Tasks;

namespace FFMpegCore.Arguments
{
    public interface IInputOutputArgument : IArgument
    {
        void Pre(CancellationToken cancellationToken = default);
        Task During(CancellationToken cancellationToken = default);
        void Post();
    }
}