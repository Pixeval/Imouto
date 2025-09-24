using AwesomeAssertions;
using Imouto.BooruParser.Tests.Loaders.Fixtures;

namespace Imouto.BooruParser.Tests.Loaders;

public class Rule34LoaderTests(Rule34ApiLoaderFixture loaderFixture) : IClassFixture<Rule34ApiLoaderFixture>
{
    private readonly Rule34ApiLoaderFixture _loaderFixture = loaderFixture;

    public class GetPostAsyncMethod(Rule34ApiLoaderFixture loaderFixture) : Rule34LoaderTests(loaderFixture)
    {
        [Fact]
        public async Task ShouldReturnPost()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(8548333);

            post.Should().NotBeNull();
            post.OriginalUrl.Should().Be("https://api-cdn-mp4.rule34.xxx/images/7492/42936037bc650b4d38bc9f6df355b0f1.mp4");
            post.Id.GetIntId().Should().Be(8548333);
            post.Id.Md5Hash.Should().Be("42936037bc650b4d38bc9f6df355b0f1");
            post.Notes.Should().BeEmpty();
            post.Tags.Should().HaveCount(110);

            foreach (var postTag in post.Tags)
            {
                postTag.Name.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().BeOneOf("general", "copyright", "character", "circle", "artist", "metadata");
            }
            
            post.Parent.Should().BeNull();
            post.Pools.Should().BeNull();
            post.SafeRating.IsExplicit.Should().Be(true);
            post.Source.Should().Be("");
            post.ChildrenIds.Should().BeNull();
            post.ExistState.Should().Be(ExistState.Exist);
            post.FileResolution.Should().Be(new Size(1920, 1440));
            post.CreateDate.Should().Be(new DateTimeOffset(2025, 4, 8, 16, 34, 42, TimeSpan.Zero));
            post.SampleUrl.Should().Be("https://api-cdn.rule34.xxx/images/7492/42936037bc650b4d38bc9f6df355b0f1.jpg");
            post.Uploader.Name.Should().Be("nebushad");
            
            // isn't supported in gelbooru
            post.ByteSize.Should().Be(0);
            post.Uploader.Id.Should().Be("-1");
            post.UgoiraFrameDelays.Should().BeNull();
        }

        [Fact]
        public async Task ShouldReturnPostByMd5()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostByMd5Async("42936037bc650b4d38bc9f6df355b0f1");

            post.Should().NotBeNull();
            post.OriginalUrl.Should().Be("https://api-cdn-mp4.rule34.xxx/images/7492/42936037bc650b4d38bc9f6df355b0f1.mp4");
            post.Id.GetIntId().Should().Be(8548333);
            post.Id.Md5Hash.Should().Be("42936037bc650b4d38bc9f6df355b0f1");
            post.Notes.Should().BeEmpty();
            post.Tags.Should().HaveCount(110);

            foreach (var postTag in post.Tags)
            {
                postTag.Name.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().NotBeNullOrWhiteSpace();
                postTag.Type.Should().BeOneOf("general", "copyright", "character", "circle", "artist", "metadata");
            }
            
            post.Parent.Should().BeNull();
            post.Pools.Should().BeNull();
            post.SafeRating.IsExplicit.Should().Be(true);
            post.Source.Should().Be("");
            post.ChildrenIds.Should().BeNull();
            post.ExistState.Should().Be(ExistState.Exist);
            post.FileResolution.Should().Be(new Size(1920, 1440));
            post.CreateDate.Should().Be(new DateTimeOffset(2025, 4, 8, 16, 34, 42, TimeSpan.Zero));
            post.SampleUrl.Should().Be("https://api-cdn.rule34.xxx/images/7492/42936037bc650b4d38bc9f6df355b0f1.jpg");
            post.Uploader.Name.Should().Be("nebushad");
            
            // isn't supported in gelbooru
            post.ByteSize.Should().Be(0);
            post.Uploader.Id.Should().Be("-1");
            post.UgoiraFrameDelays.Should().BeNull();
        }
    }

    public class SearchAsyncMethod(Rule34ApiLoaderFixture loaderFixture) : Rule34LoaderTests(loaderFixture)
    {
        [Fact]
        public async Task SearchAsyncShouldFind()
        {
            var loader = _loaderFixture.GetLoader();

            var result = await loader.SearchAsync("no_bra");
            result.Results.Should().HaveCount(20);
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
            searchResult.PageNumber.Should().Be(0);

            var searchResultNext = await loader.GetNextPageAsync(searchResult);
            searchResultNext.Results.Should().NotBeEmpty();
            searchResultNext.PageNumber.Should().Be(1);

            var searchResultPrev = await loader.GetPreviousPageAsync(searchResultNext);
            searchResultPrev.Results.Should().NotBeEmpty();
            searchResultPrev.PageNumber.Should().Be(0);
        }
    }

    public class GetPostMetadataMethod(Rule34ApiLoaderFixture fixture) : Rule34LoaderTests(fixture)
    {
        [Fact]
        public async Task ShouldLoadNotes()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(6204314);

            post.Notes.Should().HaveCount(2);
            
            post.Notes[0].Id.Should().Be("93525");
            post.Notes[0].Text.Should().Be("Slap");
            post.Notes[0].Point.Should().Be(new Position(8, 77));
            post.Notes[0].Size.Should().Be(new Size(257, 378));
            
            post.Notes.ElementAt(1).Id.Should().Be("93526");
            post.Notes.ElementAt(1).Text.Should().Be("Slap");
            post.Notes.ElementAt(1).Point.Should().Be(new Position(33, 1131));
            post.Notes.ElementAt(1).Size.Should().Be(new Size(178, 524));
        }
            
        [Fact]
        public async Task ShouldLoadSampleUrl()
        {
            var loader = _loaderFixture.GetLoader();

            var post = await loader.GetPostAsync(8548333);

            post.Should().NotBeNull();
            post.SampleUrl.Should().NotBeNull();
        }
    }
}
