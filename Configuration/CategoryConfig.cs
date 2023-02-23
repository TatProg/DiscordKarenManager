namespace DiscordChannelManager.Configuration;

public class CategoryConfig
{
    public string? Prefix { get; set; }
    public string? Postfix { get; set; }
    public int? MinCount { get; set; } = 1;
    public int? UserLimit { get; set; } = null;
}
