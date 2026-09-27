using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using Soenneker.Facebook.OpenApiClient.Models;
using Soenneker.Facebook.OpenApiClientUtil.Abstract;
using Soenneker.Facebook.Pages.Abstract;

namespace Soenneker.Facebook.Pages;

public sealed class FacebookPagesUtil : IFacebookPagesUtil
{
    private readonly IFacebookOpenApiClientUtil _clientUtil;

    public FacebookPagesUtil(IFacebookOpenApiClientUtil clientUtil)
    {
        _clientUtil = clientUtil;
    }

    public async ValueTask<GetId200Response?> Get(string pageId, string? fields = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        return await client[pageId].GetAsync(config => config.QueryParameters.Fields = fields, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<GetIdPosts200Response?> GetPosts(string pageId, int limit = 25, string? after = null, string? fields = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        return await client[pageId].Posts.GetAsync(config =>
        {
            config.QueryParameters.Limit = limit;
            config.QueryParameters.After = after;
            config.QueryParameters.Fields = fields;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<string> PublishPost(string pageId, string? message = null, string? link = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(link))
            throw new ArgumentException("A message or link is required.", nameof(message));
        if (link is not null)
            ValidateUrl(link, nameof(link));
        var body = new MultipartBody();
        AddText(body, "message", message);
        AddText(body, "link", link);
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        var response = await client[pageId].Feed.PostAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        return RequireId(response?.Id);
    }

    public async ValueTask<PostIdPhotos200Response> PublishPhoto(string pageId, string imageUrl, string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ValidateUrl(imageUrl, nameof(imageUrl));
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        var response = await client[pageId].Photos.PostAsync(new PostIdPhotosXWwwFormUrlencodedRequest
        {
            Url = imageUrl, Message = caption, Published = true
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
        RequireId(response?.Id);
        return response!;
    }

    public async ValueTask<string> PublishPhotos(string pageId, IReadOnlyList<string> imageUrls, string? message = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ArgumentNullException.ThrowIfNull(imageUrls);
        if (imageUrls.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(imageUrls));
        // Snapshot and validate the entire batch before creating remote media.
        var urls = new string[imageUrls.Count];
        for (int i = 0; i < urls.Length; i++)
        {
            urls[i] = imageUrls[i];
            ValidateUrl(urls[i], nameof(imageUrls));
        }
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        var body = new MultipartBody();
        AddText(body, "message", message);
        for (int i = 0; i < urls.Length; i++)
        {
            var photo = await client[pageId].Photos.PostAsync(new PostIdPhotosXWwwFormUrlencodedRequest
            {
                Url = urls[i], Published = false
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            AddText(body, $"attached_media[{i}]", JsonSerializer.Serialize(new { media_fbid = RequireId(photo?.Id) }));
        }
        var response = await client[pageId].Feed.PostAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        return RequireId(response?.Id);
    }

    public ValueTask<string> PublishVideo(string pageId, string videoUrl, string? description = null, string? title = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ValidateUrl(videoUrl, nameof(videoUrl));
        var body = new MultipartBody();
        AddText(body, "file_url", videoUrl);
        return PublishVideoBody(pageId, body, description, title, cancellationToken);
    }

    public ValueTask<string> PublishVideo(string pageId, Stream video, string fileName, string contentType = "video/mp4",
        string? description = null, string? title = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ArgumentNullException.ThrowIfNull(video);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (!video.CanRead)
            throw new ArgumentException("The video stream must be readable.", nameof(video));
        var body = new MultipartBody();
        body.AddOrReplacePart("source", contentType, video, fileName);
        return PublishVideoBody(pageId, body, description, title, cancellationToken);
    }

    private async ValueTask<string> PublishVideoBody(string pageId, MultipartBody body, string? description, string? title,
        CancellationToken cancellationToken)
    {
        AddText(body, "description", description);
        AddText(body, "title", title);
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        var response = await client[pageId].Videos.PostAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        return RequireId(response?.Id);
    }

    public async ValueTask<PostId200Response?> UpdatePost(string postId, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        return await client[postId].PostAsync(new PostIdXWwwFormUrlencodedRequest { Message = message },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DeleteId200Response?> DeletePost(string postId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);
        var client = await _clientUtil.Get(cancellationToken).ConfigureAwait(false);
        return await client[postId].DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void AddText(MultipartBody body, string name, string? value)
    {
        if (value is not null)
            body.AddOrReplacePart(name, "text/plain", value);
    }

    private static void ValidateUrl(string value, string parameterName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("An absolute HTTP(S) URL is required.", parameterName);
    }

    private static string RequireId(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) ? id : throw new InvalidOperationException("Facebook returned no ID for the published content.");
    }
}
