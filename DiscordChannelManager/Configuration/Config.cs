using System.Text.Json;

namespace DiscordChannelManager.Configuration;

public class Config
{
    public string Token { get; set; } = null!;
    public Dictionary<ulong, GuildConfig?>? Guilds { get; set; }

    public static async Task<Config> Load()
    {
        using StreamReader sr = new("config.json");
        return JsonSerializer.Deserialize<Config>(await sr.ReadToEndAsync(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;
    }

    public async Task Save()
    {
        await using StreamWriter sw = new("config.json");
        string serialized = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await sw.WriteAsync(serialized);
    }
}
