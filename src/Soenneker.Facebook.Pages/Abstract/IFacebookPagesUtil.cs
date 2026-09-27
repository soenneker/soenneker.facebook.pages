using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Facebook.OpenApiClient.Models;

namespace Soenneker.Facebook.Pages.Abstract;

/// <summary>
/// Reads and publishes Page content using Facebook:AccessToken configured with a Page access token.
/// Publishing requires pages_manage_posts; reading requires the applicable Page read permissions.
/// </summary>
public interface IFacebookPagesUtil
{
    /// <summary>Gets a Page with the requested comma-separated Graph API fields.</summary>
    ValueTask<GetId200Response?> Get(string pageId, string? fields = null, CancellationToken cancellationToken = default);

    /// <summary>Gets one page of posts. Pass the returned paging cursor as after to continue.</summary>
    ValueTask<GetIdPosts200Response?> GetPosts(string pageId, int limit = 25, string? after = null, string? fields = null, CancellationToken cancellationToken = default);

    /// <summary>Publishes a text or link post and returns its ID. A message or link is required.</summary>
    ValueTask<string> PublishPost(string pageId, string? message = null, string? link = null, CancellationToken cancellationToken = default);

    /// <summary>Publishes a publicly accessible HTTP(S) image URL with an optional caption. Returns the photo and post IDs.</summary>
    ValueTask<PostIdPhotos200Response> PublishPhoto(string pageId, string imageUrl, string? caption = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads publicly accessible HTTP(S) images as unpublished photos, then attaches them to one published post and returns its ID.
    /// If any request fails, previously uploaded unpublished photos may remain. No automatic retry or deletion is performed.
    /// </summary>
    ValueTask<string> PublishPhotos(string pageId, IReadOnlyList<string> imageUrls, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>Publishes a video from a public HTTP(S) URL. Returns its ID; Facebook may still be processing the video.</summary>
    ValueTask<string> PublishVideo(string pageId, string videoUrl, string? description = null, string? title = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a video from the current position of a readable stream. The caller owns the stream and must keep it open until completion.
    /// Returns its ID; Facebook may still be processing it. Large videos may require Facebook's resumable upload API.
    /// </summary>
    ValueTask<string> PublishVideo(string pageId, Stream video, string fileName, string contentType = "video/mp4", string? description = null, string? title = null, CancellationToken cancellationToken = default);

    /// <summary>Updates the message of an existing Page post.</summary>
    ValueTask<PostId200Response?> UpdatePost(string postId, string message, CancellationToken cancellationToken = default);

    /// <summary>Deletes an existing Page post.</summary>
    ValueTask<DeleteId200Response?> DeletePost(string postId, CancellationToken cancellationToken = default);
}
