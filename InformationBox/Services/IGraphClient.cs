using System.Threading;
using System.Threading.Tasks;

namespace InformationBox.Services;

/// <summary>
/// Abstraction over minimal Graph operations used by the app.
/// </summary>
public interface IGraphClient
{
    Task<GraphUser?> GetMeAsync(CancellationToken cancellationToken = default);
}
