# Imouto.BooruParser

[![NuGet](https://img.shields.io/nuget/v/Imouto.BooruParser.svg?style=flat-square)](https://www.nuget.org/packages/Imouto.BooruParser/)
[![license](https://img.shields.io/github/license/ImoutoChan/ImoutoBooruParser.svg?style=flat-square)](https://github.com/ImoutoChan/Imouto.BooruParser)

The dotnet library for retrieving info from booru sites ([chan.sankakucomplex.com](https://chan.sankakucomplex.com), [danbooru.donmai.us](https://danbooru.donmai.us), [yande.re](https://yande.re), [gelbooru.com](https://gelbooru.com)). You can get individual posts, tags/notes history, and search by tags.  Install as [NuGet package](https://www.nuget.org/packages/Imouto.BooruParser/):

```powershell
Install-Package Imouto.BooruParser
```
```xml
<PackageReference Include="Imouto.BooruParser" Version="3.*" />
```
# Version 3.1
Once again sankaku broke everything. It's now recommended to provide login and pass_hash for sankaku in order to receive all tags from posts. Without it you can only get 20 general tags.

# Version 3.0
Have to increment major version due to breaking change in danbooru api usage. Now you have to provide your own user agent for your bot in danbooru settings, otherwise danbooru requests would fail with 403 (recent change on their side).

# Version 2.0 released!

The new version of this library has been released. It's a complete rewrite and has a lot of **breaking changes**. Please **don't update** unless you're ready to spent some time in refactoring and reading.

The new version has gelbooru as partly supported booru. It doesn't provide a way to get tags or notes history, but you can search and retrieve posts by id or md5. To consume this library you should use `IBooruApiLoader` interface. The library provides following implementations, one for each booru:

```csharp
var loader = new DanbooruApiLoader(...);
var loader = new YandereApiLoader(...);
var loader = new SankakuApiLoader(...);
var loader = new GelbooruApiLoader(...);
```

The recommended way to consume them is to register them in your container:

```csharp
services.AddBooruParsers();

// OPTIONAL
// It'ts nessesary for Sankaku auth management. 
// You can skip it, if you don't plan to access sankaku with authorization.
services.AddMemoryCache();
```

But you can also register them yourself. You also need to provide configuration for booru if you want to have more relaxed limits:

```csharp
services.Configure<DanbooruSettings>(Configuration.GetSection("Danbooru"));
services.Configure<GelbooruSettings>(Configuration.GetSection("Gelbooru"));
services.Configure<SankakuSettings>(Configuration.GetSection("Sankaku"));
services.Configure<YandereSettings>(Configuration.GetSection("Yandere"));
```
Each settings has `PauseBetweenRequestsInMs` param which defines a pause between requests. 0 means you can run your requests in parallel and any positive number means that all requests would be called with the provided pause in ms between them. It's important to have some reserves to not get banned.

! **Sankaku** is a difficult case for the authorization. It requires access and refresh tokens (you can extract them from cookies), but refresh token has to be updated. So `SankakuSettings` has a callback method `SaveTokensCallbackAsync` that will be called, when it's necessary to update refresh and access tokens. It's you responsibility to store them somewhere and pass on the next application start.

## Simplest usage

```C#
var loader = new DanbooruApiLoader(
    new PerBaseUrlFlurlClientFactory(), 
    Options.Create(new DanbooruSettings()))
    
var post = await loader.GetPostAsync(5628013);
Console.WriteLine(post.Md5);
```

# Version 1.x.x

## Simplest usage

```C#
var username = "user";~~~~
var apiKey = "HBArbAk4WcKTSeAfsyBO";
var delayBetweenRequestsInMs = 1240;

var danbooruLoader = new DanbooruLoader(username, apiKey, delayBetweenRequestsInMs);
var post = await danbooruLoader.LoadPostAsync(5628013);
Console.WriteLine(post.Md5);
```
