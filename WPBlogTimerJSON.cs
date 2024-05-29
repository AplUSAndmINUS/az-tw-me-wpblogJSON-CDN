using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure;

namespace az_tw_me_wpblogJSON_CDN
{
    public class Post
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("content")]
        public Content Content { get; set; }

        [JsonPropertyName("excerpt")]
        public string Excerpt { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("link")]
        public string Link { get; set; }

        public string FeaturedImage
        {
            get
            {
                var match = Regex.Match(Content.Rendered, @"http[^\s]*scaled\.jpeg");
                return match.Success ? match.Value : "";
            }
        }
    }

    public class Content
    {
        [JsonPropertyName("rendered")]
        public string Rendered { get; set; }

        [JsonPropertyName("protected")]
        public bool Protected { get; set; }
    }
    public class WPBlogTimerJSON
    {
        private readonly ILogger _logger;
        private static readonly HttpClient client = new HttpClient();

        public WPBlogTimerJSON(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WPBlogTimerJSON>();
        }

        [Function("WPBlogTimerJSON")]
        public async Task Run([TimerTrigger("0 0 */2 * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "http://blog-terencewaters-com.ibrave.host/wp-json/wp/v2/posts");
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var posts = JsonSerializer.Deserialize<List<Post>>(responseContent);

                    // Environment variables
                    string blobContainer = Environment.GetEnvironmentVariable("BLOB_CONTAINER");5
                    string connectionString = Environment.GetEnvironmentVariable("AZURE_WEB_JOBS_STORAGE");

                    // Connect to Azure Storage
                    BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobContainer);

                    // Upload data to Azure Storage
                    foreach (var post in posts)
                    {
                        var blobClient = containerClient.GetBlobClient($"{post.Id}.json");
                        var postJson = JsonSerializer.Serialize(post);
                        await blobClient.UploadAsync(new BinaryData(postJson));
                    }
                }
                else
                {
                    _logger.LogError($"Failed to fetch data from WordPress: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }
        }
    }
}
