namespace TbotUltra.Core.Configuration;

public static class LobbyWorldSelectionDefaults
{
    public const string ServerName = "Choose in lobby";
    public const string ServerUrl = "https://lobby.legends.travian.com";

    public static bool IsChooseInLobby(string? serverName)
        => string.Equals(ServerName, serverName?.Trim(), StringComparison.OrdinalIgnoreCase);
}
