using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AliveNpcsPersonalityEditor.Models;

namespace AliveNpcsPersonalityEditor;

public sealed class GalleryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public GalleryService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> CheckConnectivityAsync()
    {
        try
        {
            using var resp = await _client.GetAsync($"{_baseUrl}/api/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<PresetListResponse?> SearchPresetsAsync(string? query = null, int page = 1, int limit = 24, string? npcFilter = null)
    {
        try
        {
            var url = $"{_baseUrl}/api/presets?page={page}&limit={limit}";
            if (!string.IsNullOrWhiteSpace(query))
                url += $"&q={Uri.EscapeDataString(query)}";
            if (!string.IsNullOrWhiteSpace(npcFilter))
                url += $"&npc={Uri.EscapeDataString(npcFilter)}";

            using var resp = await _client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PresetListResponse>(json, JsonOptions);
        }
        catch { return null; }
    }

    public async Task<PresetDownload?> DownloadPresetAsync(string presetId)
    {
        try
        {
            using var resp = await _client.GetAsync($"{_baseUrl}/api/presets/{presetId}");
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PresetDownload>(json, JsonOptions);
        }
        catch { return null; }
    }

    public async Task<bool> UploadPresetAsync(string npcName, NpcOverrideEntry data, string author = "Anonymous")
    {
        try
        {
            var payload = new
            {
                npcName,
                author,
                data
            };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _client.PostAsync($"{_baseUrl}/api/presets", content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeletePresetAsync(string presetId)
    {
        try
        {
            using var resp = await _client.DeleteAsync($"{_baseUrl}/api/presets/{presetId}");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

public sealed class PresetMetadata
{
    public string Id { get; set; } = "";
    public string NpcName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Preview { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public int DownloadCount { get; set; }
    public bool CanDelete { get; set; }
}

public sealed class PresetListResponse
{
    public List<PresetMetadata> Presets { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
}

public sealed class PresetDownload
{
    public string Id { get; set; } = "";
    public string NpcName { get; set; } = "";
    public string Author { get; set; } = "";
    public NpcOverrideEntry Data { get; set; } = new();
    public string CreatedAt { get; set; } = "";
    public int DownloadCount { get; set; }
}
