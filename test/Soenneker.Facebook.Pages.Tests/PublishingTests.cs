using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Soenneker.Facebook.OpenApiClientUtil.Abstract;

namespace Soenneker.Facebook.Pages.Tests;

public sealed class PublishingTests
{
    [Test]
    public async ValueTask Photo_contains_url_caption_and_published_flag()
    {
        using var fixture = new Fixture("""{"id":"photo1","post_id":"page_post"}""");
        var result = await fixture.Util.PublishPhoto("page", "https://example.com/image.jpg", "Caption & more");
        Check(result.PostId == "page_post", "Post ID was not deserialized.");
        Check(fixture.Handler.Requests[0].Path == "/v26.0/page/photos", "Incorrect photo endpoint.");
        string body = Uri.UnescapeDataString(fixture.Handler.Requests[0].Body.Replace("+", " "));
        Check(body.Contains("url=https://example.com/image.jpg"), "Missing photo URL.");
        Check(body.Contains("message=Caption & more"), "Caption was not encoded correctly.");
        Check(body.Contains("published=true"), "Photo was not published.");
    }

    [Test]
    public async ValueTask Multiple_photos_are_uploaded_before_one_feed_post()
    {
        using var fixture = new Fixture("""{"id":"photo1"}""", """{"id":"photo2"}""", """{"id":"page_post"}""");
        string id = await fixture.Util.PublishPhotos("page", ["https://example.com/1.jpg", "https://example.com/2.jpg"], "Photos");
        Check(id == "page_post", "Incorrect post ID.");
        var requests = fixture.Handler.Requests;
        Check(requests.Count == 3, "Expected two uploads and one post.");
        Check(requests[0].Body.Contains("published=false") && requests[1].Body.Contains("published=false"), "Uploads must be unpublished.");
        Check(requests[2].Path == "/v26.0/page/feed", "Incorrect feed endpoint.");
        Check(requests[2].Body.Contains("attached_media[0]") && requests[2].Body.Contains("attached_media[1]"), "Missing attachments.");
        Check(requests[2].Body.Contains("""{"media_fbid":"photo1"}""") && requests[2].Body.Contains("""{"media_fbid":"photo2"}"""), "Incorrect attachment IDs.");
    }

    [Test]
    public async ValueTask Missing_photo_id_stops_before_publishing()
    {
        using var fixture = new Fixture("{}");
        try
        {
            await fixture.Util.PublishPhotos("page", ["https://example.com/1.jpg"]);
            throw new Exception("Expected a missing-ID failure.");
        }
        catch (InvalidOperationException)
        {
            Check(fixture.Handler.Requests.Count == 1, "A feed post should not be created after a failed upload.");
        }
    }

    [Test]
    public async ValueTask Invalid_image_batch_makes_no_requests()
    {
        using var fixture = new Fixture();
        try
        {
            await fixture.Util.PublishPhotos("page", ["https://example.com/1.jpg", "file:///invalid.jpg"]);
            throw new Exception("Expected URL validation to fail.");
        }
        catch (ArgumentException)
        {
            Check(fixture.Handler.Requests.Count == 0, "Validation must happen before uploads.");
        }
    }

    [Test]
    public async ValueTask Text_and_link_are_sent_as_multipart_fields()
    {
        using var fixture = new Fixture("""{"id":"post"}""");
        Check(await fixture.Util.PublishPost("page", "Hello", "https://example.com") == "post", "Incorrect post ID.");
        string body = fixture.Handler.Requests[0].Body;
        Check(body.Contains("Hello") && body.Contains("https://example.com"), "Missing post fields.");
    }

    [Test]
    public async ValueTask Video_stream_contains_file_and_remains_open()
    {
        using var fixture = new Fixture("""{"id":"video"}""");
        using var stream = new MemoryStream("video-content"u8.ToArray());
        Check(await fixture.Util.PublishVideo("page", stream, "clip.mp4", description: "Clip") == "video", "Incorrect video ID.");
        Check(stream.CanRead, "The caller-owned stream was closed.");
        var request = fixture.Handler.Requests[0];
        Check(request.Path == "/v26.0/page/videos", "Incorrect video endpoint.");
        Check(request.Body.Contains("video-content") && request.Body.Contains("clip.mp4") && request.Body.Contains("Clip"), "Missing video data.");
    }

    [Test]
    public async ValueTask Cancellation_is_forwarded()
    {
        using var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        try
        {
            await fixture.Util.PublishPost("page", "Hello", cancellationToken: cts.Token);
            throw new Exception("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
            Check(fixture.Handler.Requests.Count == 0, "Cancelled operation made an HTTP request.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class Fixture : IDisposable
    {
        public RecordingHandler Handler { get; }
        public FacebookPagesUtil Util { get; }
        private readonly HttpClient _http;
        private readonly HttpClientRequestAdapter _adapter;

        public Fixture(params string[] responses)
        {
            Handler = new RecordingHandler(responses);
            _http = new HttpClient(Handler);
            _adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: _http);
            Util = new FacebookPagesUtil(new ClientUtil(new OpenApiClient.FacebookOpenApiClient(_adapter)));
        }

        public void Dispose()
        {
            _adapter.Dispose();
            _http.Dispose();
        }
    }

    private sealed class ClientUtil(OpenApiClient.FacebookOpenApiClient client) : IFacebookOpenApiClientUtil
    {
        public ValueTask<OpenApiClient.FacebookOpenApiClient> Get(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(client);
        }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.AbsolutePath, body));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[Requests.Count - 1], System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
