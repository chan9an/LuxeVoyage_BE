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

    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration config)
        {
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        // Upload an image from a remote URL into Cloudinary
        public async Task<string> UploadFromUrlAsync(string imageUrl, string publicId)
        {
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

        // Extract public_id from a Cloudinary URL and delete it
        public async Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                // Cloudinary URLs look like:
                // https://res.cloudinary.com/{cloud}/image/upload/v123456/luxevoyage/hotels/abc.jpg
                // public_id = everything after /upload/v{version}/ without extension
                var uri = new Uri(imageUrl);
                var segments = uri.AbsolutePath.Split('/');

                // Find "upload" segment index
                int uploadIdx = Array.IndexOf(segments, "upload");
                if (uploadIdx < 0) return;

                // Skip "upload" and the version segment (v12345), join the rest, strip extension
                var publicIdWithExt = string.Join("/", segments.Skip(uploadIdx + 2));
                var publicId = Path.GetFileNameWithoutExtension(publicIdWithExt);
                var folder = Path.GetDirectoryName(publicIdWithExt)?.Replace("\\", "/");
                var fullPublicId = string.IsNullOrEmpty(folder) ? publicId : $"{folder}/{publicId}";

                var deleteParams = new DeletionParams(fullPublicId);
                await _cloudinary.DestroyAsync(deleteParams);
            }
            catch (Exception ex)
            {
                // Log but don't throw — DB record should still be deleted
                Console.WriteLine($"[Cloudinary] Failed to delete image: {ex.Message}");
            }
        }
    }
}
