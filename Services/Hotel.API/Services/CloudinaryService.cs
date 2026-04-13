using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace Hotel.API.Services
{
    public interface ICloudinaryService
    {
        Task<string> UploadFromUrlAsync(string imageUrl, string publicId);
        Task DeleteImageAsync(string imageUrl);
    }

    /*
     * CloudinaryDotNet is the official .NET SDK for Cloudinary, which is the cloud image storage
     * and CDN service we use for hotel photos. We chose Cloudinary over storing images in the
     * database or a local file system because it handles image optimisation, CDN delivery, and
     * transformations (resizing, cropping, format conversion) automatically. The SDK wraps the
     * Cloudinary REST API and handles authentication, request signing, and response parsing for us.
     */
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration config)
        {
            /*
             * The Account object holds the three credentials that Cloudinary uses to authenticate
             * API calls — the cloud name identifies which Cloudinary account we're talking to,
             * while the API key and secret are used to sign requests. We set Api.Secure = true
             * to ensure all API calls go over HTTPS rather than plain HTTP.
             */
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<string> UploadFromUrlAsync(string imageUrl, string publicId)
        {
            /*
             * When FileDescription is given a URL string rather than a file path or stream, Cloudinary
             * fetches the image from that URL on their servers rather than us uploading the bytes.
             * This is more efficient for our seed script use case where images come from Unsplash —
             * we just pass the Unsplash URL and Cloudinary handles the download and storage.
             */
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(imageUrl),
                PublicId = publicId,
                Overwrite = true,
                Folder = "luxevoyage/hotels"
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl.ToString();
        }

        public async Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                /*
                 * Cloudinary's delete API requires the image's public_id, not the full URL. The public_id
                 * is the path within Cloudinary's storage system, without the domain, version segment,
                 * or file extension. For example, a URL like:
                 * https://res.cloudinary.com/mycloud/image/upload/v1234567/luxevoyage/hotels/abc.jpg
                 * has a public_id of "luxevoyage/hotels/abc".
                 * We parse this out by finding the "upload" segment in the URL path and taking everything
                 * after the version number that follows it.
                 */
                var uri = new Uri(imageUrl);
                var segments = uri.AbsolutePath.Split('/');

                int uploadIdx = Array.IndexOf(segments, "upload");
                if (uploadIdx < 0) return;

                var publicIdWithExt = string.Join("/", segments.Skip(uploadIdx + 2));
                var publicId = Path.GetFileNameWithoutExtension(publicIdWithExt);
                var folder = Path.GetDirectoryName(publicIdWithExt)?.Replace("\\", "/");
                var fullPublicId = string.IsNullOrEmpty(folder) ? publicId : $"{folder}/{publicId}";

                var deleteParams = new DeletionParams(fullPublicId);
                await _cloudinary.DestroyAsync(deleteParams);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cloudinary] Failed to delete image: {ex.Message}");
            }
        }
    }
}
