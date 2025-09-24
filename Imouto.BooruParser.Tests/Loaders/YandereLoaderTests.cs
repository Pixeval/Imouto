using AwesomeAssertions;
using Imouto.BooruParser.Implementations;
using Imouto.BooruParser.Tests.Loaders.Fixtures;

namespace Imouto.BooruParser.Tests.Loaders;

public class YandereLoaderTests(YandereApiLoaderFixture loaderFixture) : IClassFixture<YandereApiLoaderFixture>
{
    private readonly YandereApiLoaderFixture _loaderFixture = loaderFixture;

    public class GetPostAsyncMethod(YandereApiLoaderFixture loaderFixture) : YandereLoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldReturnPost()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(408517);

            await Verify(post);
        }

        [Fact]
        public async Task ShouldReturnPostWithAuth()
        {
            var loader = _loaderFixture.GetLoaderWithAuth();

            var post = await loader.GetPostAsync(408517);

            await Verify(post);
        }

        [Fact]
        public async Task ShouldReturnPostByMd5()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostByMd5Async("5569d245d4c85921a0da173d87391862");

            await Verify(post);
        }
    }

    public class SearchAsyncMethod(YandereApiLoaderFixture loaderFixture) : YandereLoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldFind()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.SearchAsync("no_bra");
            result.Results.Should().HaveCountGreaterThan(1);
            result.Results.ToList().ForEach(x => x.IsDeleted.Should().BeFalse());
            result.Results.ToList().ForEach(x => x.IsBanned.Should().BeFalse());

            foreach (var preview in result.Results)
            {
                var post = await loader.GetPostAsync(preview.Id);
                post.Tags.Select(x => x.Name).Should().Contain("no bra");
            }
        }

        [Fact]
        public async Task ShouldNavigateSearch()
        {
            var loader = _loaderFixture.GetLoader();

            var searchResult = await loader.SearchAsync("no_bra");
            searchResult.Results.Should().NotBeEmpty();
            searchResult.PageNumber.Should().Be(1);

            var searchResultNext = await loader.GetNextPageAsync(searchResult);
            searchResultNext.Results.Should().NotBeEmpty();
            searchResultNext.PageNumber.Should().Be(2);

            var searchResultPrev = await loader.GetPreviousPageAsync(searchResultNext);
            searchResultPrev.Results.Should().NotBeEmpty();
            searchResultPrev.PageNumber.Should().Be(1);
        }
    }

    public class LoadNotesHistoryAsyncMethod(YandereApiLoaderFixture loaderFixture) : YandereLoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldLoadNotesHistory()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.GetNoteHistoryToDateTimeAsync(DateTime.Now.AddHours(-5)).ToListAsync();

            result.Should().NotBeEmpty();
            result.Should().HaveCountGreaterThanOrEqualTo(25);
            result.ToList().ForEach(x => x.HistoryId.Should().Be(-1));
        }
        
        [Fact]
        public async Task ShouldReturnNextNumberToken()
        {
            var loader = _loaderFixture.GetLoader();

            var firstPage = await loader.GetNoteHistoryPageAsync(null);

            firstPage.NextToken?.Page.Should().Be("2");
        }
        
        [Fact]
        public async Task ShouldReturnNextNumberTokenFor2()
        {
            var loader = _loaderFixture.GetLoader();

            var firstPage = await loader.GetNoteHistoryPageAsync(new("2"));

            firstPage.NextToken?.Page.Should().Be("3");
        }
    }

    public class GetTagHistoryPageAsyncMethod(YandereApiLoaderFixture loaderFixture) : YandereLoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldLoadTagsHistoryToDate()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.GetTagHistoryToDateTimeAsync(DateTimeOffset.Now.AddHours(-1)).ToListAsync();

            result.Should().NotBeEmpty();
            result[0].HistoryId.Should().BeGreaterThan(0);
            result[0].PostId.Should().NotBeEmpty();
            result[0].UpdatedAt.Should().BeAfter(DateTimeOffset.Now.AddHours(-2));
        }

        [Fact]
        public async Task ShouldGetTagHistoryFirstPage()
        {
            var loader = _loaderFixture.GetLoader();

            var firstPage = await loader.GetTagHistoryFirstPageAsync();

            firstPage.Should().NotBeEmpty();
        }
        
        [Fact]
        public async Task ShouldReturnNextNumberToken()
        {
            var loader = _loaderFixture.GetLoader();

            var firstPage = await loader.GetTagHistoryPageAsync(null);

            firstPage.NextToken?.Page.Should().Be("2");
        }
        
        [Fact]
        public async Task ShouldReturnNextNumberTokenFor2()
        {
            var loader = _loaderFixture.GetLoader();

            var firstPage = await loader.GetTagHistoryPageAsync(new("2"));

            firstPage.NextToken?.Page.Should().Be("3");
        }

        [Fact]
        public async Task ShouldGetTagHistoryFromIdToPresent()
        {
            var loader = _loaderFixture.GetLoader();
            var firstTagHistoryPage = await loader.GetTagHistoryFirstPageAsync();

            var result = await loader.GetTagHistoryFromIdToPresentAsync(firstTagHistoryPage[^1].HistoryId)
                .ToListAsync();

            result.Should().NotBeEmpty();
            result.DistinctBy(x => x.HistoryId).Should().HaveCount(result.Count);
        }

        /// <summary>
        /// Bug with loading history after 4952686
        /// </summary>
        [Fact(Skip = "Too long history")]
        public async Task ShouldGetTagHistoryFrom4952686IdToPresent()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.GetTagHistoryFromIdToPresentAsync(4952686).ToListAsync();

            result.Should().NotBeEmpty();
            result.DistinctBy(x => x.HistoryId).Should().HaveCount(result.Count);
        }
    }

    public class GetPopularPostsAsyncMethod(YandereApiLoaderFixture loaderFixture) : YandereLoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldLoadPopularForDay()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.GetPopularPostsAsync(PopularType.Day);
            result.Results.Should().HaveCountGreaterThan(20);
            result.Results.ToList().ForEach(x => x.IsDeleted.Should().BeFalse());
            result.Results.ToList().ForEach(x => x.IsBanned.Should().BeFalse());
        }

        [Fact]
        public async Task ShouldLoadPopularForWeek()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.GetPopularPostsAsync(PopularType.Week);
            result.Results.Should().HaveCountGreaterThan(20);
            result.Results.ToList().ForEach(x => x.IsDeleted.Should().BeFalse());
            result.Results.ToList().ForEach(x => x.IsBanned.Should().BeFalse());
        }

        [Fact]
        public async Task ShouldLoadPopularForMonth()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.GetPopularPostsAsync(PopularType.Month);
            result.Results.Should().HaveCountGreaterThan(20);
            result.Results.ToList().ForEach(x => x.IsDeleted.Should().BeFalse());
            result.Results.ToList().ForEach(x => x.IsBanned.Should().BeFalse());
        }
    }

    public class LoadPostMetadataAsyncMethod(YandereApiLoaderFixture fixture) : YandereLoaderTests(fixture)
    {
        [Fact]
        public async Task ShouldLoadParentsAndChildren()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(507625);

            await Verify(post);
        }
            
        [Fact]
        public async Task ShouldLoadChildrenOf801490()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(801490);

            await Verify(post);
        }

        [Fact]
        public async Task ShouldLoadNotes()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(321139);

            await Verify(post);
        }

        [Fact]
        public async Task ShouldLoadPools()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(572039);

            await Verify(post);
        }
        
        [Fact]
        public async Task ShouldLoadPoolsMetadata()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(1032020);

            await Verify(post);
        }
            
        [Fact]
        public async Task ShouldLoadSampleUrl()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(1021031);

            await Verify(post);
        }
    }

    public class FavoritePostAsyncMethod(YandereApiLoaderFixture loaderFixture) : YandereLoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldFavoritePost()
        {
            var api = _loaderFixture.GetApiAccessorWithAuth();
            await api.FavoritePostAsync(883843);
        }
    }
}
