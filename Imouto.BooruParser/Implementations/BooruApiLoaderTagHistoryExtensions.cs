using System.Runtime.CompilerServices;
using Imouto.BooruParser.Implementations.Danbooru;
using Imouto.BooruParser.Implementations.Sankaku;
using Imouto.BooruParser.Implementations.Yandere;

namespace Imouto.BooruParser.Implementations;

public static class BooruApiLoaderTagHistoryExtensions
{
    public static async Task<IReadOnlyList<TagHistoryEntry>> GetTagHistoryFirstPageAsync(
        this IBooruApiLoader loader,
        int limit = 100,
        CancellationToken ct = default)
    {
        var page = await loader.GetTagHistoryPageAsync(null, limit, ct);
        return page.Results;
    }

    public static async IAsyncEnumerable<TagHistoryEntry> GetTagHistoryFromIdToPresentAsync(
        this IBooruApiLoader loader, 
        long afterHistoryId,
        int limit = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (loader is DanbooruApiLoader)
        {
            var searchToken = new SearchToken($"a{afterHistoryId}");
            do
            {
                var page = await loader.GetTagHistoryPageAsync(searchToken, limit, ct);
                searchToken = page.NextToken;

                foreach (var tagsHistoryEntry in page.Results)
                    yield return tagsHistoryEntry;

            } while (searchToken != null);
        }
        else if (loader is YandereApiLoader)
        {
            var firstPage = await loader.GetTagHistoryFirstPageAsync(ct: ct);

            // prediction
            var currentId = firstPage.Max(x => x.HistoryId);
            var predictedPage = (currentId - afterHistoryId) / 20 + 2;
            
            // validation
            HistorySearchResult<TagHistoryEntry> page;
            var tries = 10;
            do
            {
                page = await loader.GetTagHistoryPageAsync(new($"{predictedPage++}"), limit, ct);
                tries--;
            } while (page.Results.Count is 0 && tries > 0);
            
            if (page.Results.All(x => x.HistoryId > afterHistoryId))
                throw new("Prediction failed");

            // execution
            var searchToken = new SearchToken($"{predictedPage--}");
            do
            {
                page = await loader.GetTagHistoryPageAsync(searchToken, limit, ct);
                searchToken = new($"{predictedPage--}");

                foreach (var tagsHistoryEntry in page.Results)
                    yield return tagsHistoryEntry;

            } while (searchToken.Page != "0");
        }
        else if (loader is SankakuApiLoader)
        {
            
            HistorySearchResult<TagHistoryEntry> page;
            var cont = false;
            SearchToken? searchToken = null;
            do
            {
                page = await loader.GetTagHistoryPageAsync(searchToken, limit, ct);
                searchToken = page.NextToken;

                foreach (var tagsHistoryEntry in page.Results)
                    yield return tagsHistoryEntry;

                cont = !page.Results.Any(x => x.HistoryId < afterHistoryId);
            } while (cont);
        }
    }

    public static async IAsyncEnumerable<TagHistoryEntry> GetTagHistoryToDateTimeAsync(
        this IBooruApiLoader loader, 
        DateTimeOffset upToDateTime,
        int limit = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        SearchToken? searchToken = null;
        HistorySearchResult<TagHistoryEntry> page;
        while (true)
        {
            page = await loader.GetTagHistoryPageAsync(searchToken, limit, ct);
            searchToken = page.NextToken;

            foreach (var tagsHistoryEntry in page.Results)
                if (tagsHistoryEntry.UpdatedAt >= upToDateTime)
                    yield return tagsHistoryEntry;
                else
                    yield break;

            if (page.Results.Count is 0)
                break;
        }
    }
}
