using Flurl.Http;
using Misaki;

namespace Imouto.BooruParser.Implementations.Sankaku;

public interface ISankakuAuthManager : IMisakiService
{
    ValueTask<string?> GetTokenAsync();

    Task<IReadOnlyList<FlurlCookie>> GetSankakuChannelSessionAsync();
}
