using AwesomeAssertions;
using Imouto.BooruParser.Extensions;
using Imouto.BooruParser.Implementations;
using Imouto.BooruParser.Tests.Loaders.Fixtures;
using Xunit;

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

            post.Should().NotBeNull();
            post.OriginalUrl.Should().Be("https://files.yande.re/image/5569d245d4c85921a0da173d87391862/yande.re%20408517%20cleavage%20dakimakura%20fate_grand_order%20kimono%20mash_kyrielight%20no_bra%20nopan%20open_shirt%20yuran.png");
            post.Id.GetIntId().Should().Be(408517);
            post.Id.Md5Hash.Should().Be("5569d245d4c85921a0da173d87391862");
            post.Notes.Should().BeEmpty();
            post.Tags.Should().HaveCount(9);

            foreach (var postTag in post.Tags)
            {
                postTag.Name.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().BeOneOf("general", "copyright", "character", "circle", "artist");
            }
            
            post.Parent.Should().BeNull();
            post.Pools.Should().BeNull();
            post.SafeRating.IsQuestionable.Should().Be(true);
            post.Source.Should().Be("https://yurang92.booth.pm/items/621246");
            post.ChildrenIds.Should().BeNull();
            post.ExistState.Should().Be(ExistState.Exist);
            post.FileResolution.Should().Be(new Size(1383, 4393));
            post.CreateDate.Should().Be(new(2017, 09, 06, 5, 38, 17, TimeSpan.Zero));
            post.SampleUrl.Should().Be("https://files.yande.re/sample/5569d245d4c85921a0da173d87391862/yande.re%20408517%20sample%20cleavage%20dakimakura%20fate_grand_order%20kimono%20mash_kyrielight%20no_bra%20nopan%20open_shirt%20yuran.jpg");
            post.Uploader.Id.Should().Be("25882");
            post.Uploader.Name.Should().Be("Mr_GT");
            post.ByteSize.Should().Be(5455985);
            post.UgoiraFrameDelays.Should().BeNull();
        }

        [Fact]
        public async Task ShouldReturnPostByMd5()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostByMd5Async("5569d245d4c85921a0da173d87391862");

            post.Should().NotBeNull();
            post.OriginalUrl.Should().Be("https://files.yande.re/image/5569d245d4c85921a0da173d87391862/yande.re%20408517%20cleavage%20dakimakura%20fate_grand_order%20kimono%20mash_kyrielight%20no_bra%20nopan%20open_shirt%20yuran.png");
            post.Id.GetIntId().Should().Be(408517);
            post.Id.Md5Hash.Should().Be("5569d245d4c85921a0da173d87391862");
            post.Notes.Should().BeEmpty();
            post.Tags.Should().HaveCount(9);

            foreach (var postTag in post.Tags)
            {
                postTag.Name.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().NotBeNullOrWhiteSpace();
            }
            
            post.Parent.Should().BeNull();
            post.Pools.Should().BeNull();
            post.Source.Should().Be("https://yurang92.booth.pm/items/621246");
            post.ChildrenIds.Should().BeNull();
            post.ExistState.Should().Be(ExistState.Exist);
            post.FileResolution.Should().Be(new Size(1383, 4393));
            post.CreateDate.Should().Be(new(2017, 09, 06, 5, 38, 17, TimeSpan.Zero));
            post.SampleUrl.Should().Be("https://files.yande.re/sample/5569d245d4c85921a0da173d87391862/yande.re%20408517%20sample%20cleavage%20dakimakura%20fate_grand_order%20kimono%20mash_kyrielight%20no_bra%20nopan%20open_shirt%20yuran.jpg");
            post.Uploader.Id.Should().Be("25882");
            post.Uploader.Name.Should().Be("Mr_GT");
            post.SafeRating.IsQuestionable.Should().Be(true);
            post.ByteSize.Should().Be(5455985);
            post.UgoiraFrameDelays.Should().BeNull();
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

            post.ChildrenIds?.Select(x => x.Id).Should().BeEquivalentTo("536313", "962046");
            post.ChildrenIds?.Select(x => x.Md5Hash).Should().BeEquivalentTo("9c40357dc4818f82f5d9a16a29f20b2b", "ee0578d4eba0419b2a60d934ddc7a7ac");
            post.Parent.Should().NotBeNull();
            post.Parent.Id.Should().Be("507843");
        }
            
        [Fact]
        public async Task ShouldLoadChildrenOf801490()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(801490);

            var childrenIds = await post.ChildrenIdsGetter!(post);

            childrenIds.Count.Should().Be(3);

            foreach (var childrenId in childrenIds)
            {
                var childPost = await loader.GetPostAsync(childrenId.GetIntId());
                var md5 = childPost.Id.Md5Hash;

                md5.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task ShouldLoadNotes()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(321139);

            post.Notes.Should().HaveCount(2);
            post.Notes[0].Id.Should().Be("4625");
            post.Notes[0].Text.Should().Be("Hmm.....!");
            post.Notes[0].Point.Should().Be(new Position(72, 824));
            post.Notes[0].Point.Top.Should().Be(72);
            post.Notes[0].Point.Left.Should().Be(824);
            post.Notes[0].Size.Should().Be(new Size(109, 274));
        }

        [Fact]
        public async Task ShouldLoadPools()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(572039);

            post.Pools?.Count.Should().BeGreaterThanOrEqualTo(1);
        }
        
        [Fact]
        public async Task ShouldLoadPoolsMetadata()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(1032020);

            post.Pools?.Count.Should().Be(1);
            post.Pools?[0].Id.Should().Be("98410");
            post.Pools?[0].Name.Should().Be("(C100) [Tegone Spike (Senji)] kaimin shojo");
            post.Pools?[0].Position.Should().Be(1);
        }
            
        [Fact]
        public async Task ShouldLoadSampleUrl()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(1021031);

            post.Should().NotBeNull();
            post.SampleUrl.Should().Contain("sample");
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
