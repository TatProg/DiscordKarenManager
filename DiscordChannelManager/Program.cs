using Discord;
using Discord.WebSocket;

using DiscordChannelManager.Configuration;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

Config config = await Config.Load();

ConcurrentDictionary<ulong, CancellationTokenSource> activeUpdates = new();

DiscordSocketClient bot = new(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.Guilds
});

bot.Log += message =>
{
    Console.WriteLine(message);
    return Task.CompletedTask;
};

bot.UserVoiceStateUpdated += UserVoiceStateUpdated;

await bot.LoginAsync(TokenType.Bot, config.Token);
await bot.StartAsync();

await Task.Delay(-1);

async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
{
    if (user is not SocketGuildUser)
        return;

    await HandleVoiceState(oldState);

    if (oldState.VoiceChannel?.CategoryId != newState.VoiceChannel?.CategoryId)
        await HandleVoiceState(newState);
}

async Task HandleVoiceState(SocketVoiceState state)
{
    SocketVoiceChannel? vc = state.VoiceChannel;

    if (vc?.CategoryId is null)
        return;

    if (activeUpdates.TryGetValue((ulong)vc.CategoryId, out _))
    {
        return;
    }

    if (config.Guilds?.TryGetValue(vc.Guild.Id, out GuildConfig? guildConfig) != true)
        return;

    if (guildConfig!.Categories?.TryGetValue((ulong)vc.CategoryId, out CategoryConfig? categoryConfig) != true)
        return;

    if (categoryConfig is null)
        return;

    CancellationTokenSource cts = new();
    if (!activeUpdates.TryAdd((ulong)vc.CategoryId, cts))
    {
        cts.Dispose();
        return;
    }

    try
    {
        await UpdateCategory((SocketCategoryChannel)vc.Category, categoryConfig, cts.Token);
    }
    catch (OperationCanceledException ex) { }
    finally
    {
        activeUpdates.Remove((ulong)vc.CategoryId, out _);
        cts.Dispose();
    }
}

async Task UpdateCategory(SocketCategoryChannel category, CategoryConfig categoryConfig, CancellationToken cancellationToken = default)
{
    Regex channelNameRegex = new($@"^{categoryConfig.Prefix}(\d+){categoryConfig.Postfix}$");

    List<SocketVoiceChannel> vcs = category.Channels
        .OfType<SocketVoiceChannel>()
        .OrderBy(x => x.Name)
        .ToList();

    int emptyChannelsCount = vcs.Count(x => x.ConnectedUsers.Count == 0);

    int requestedChannelsAmount = (categoryConfig.MinCount ?? 1) - emptyChannelsCount;

    switch (requestedChannelsAmount)
    {
        case 0:
            return;
        case > 0:
        {
            // Add channels

            int lastChannelNumber;

            if (vcs.Count > 0)
            {
                Match m = channelNameRegex.Match(vcs[^1].Name);
                lastChannelNumber = m.Success ? int.Parse(m.Groups[1].Value) : 1;
            }
            else
                lastChannelNumber = 0;

            for (int i = 0; i < requestedChannelsAmount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await category.Guild.CreateVoiceChannelAsync($"{categoryConfig.Prefix}{++lastChannelNumber}{categoryConfig.Postfix}", options =>
                {
                    options.CategoryId = category.Id;
                    options.UserLimit = categoryConfig.UserLimit;
                });
            }

            break;
        }
        case < 0:
        {
            // Remove channels

            for (int i = vcs.Count - 1;
                 i >= 0 && requestedChannelsAmount < 0;
                 i--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (vcs[i].ConnectedUsers.Count != 0)
                    continue;

                await vcs[i].DeleteAsync();
                requestedChannelsAmount++;
            }

            break;
        }
    }
}
