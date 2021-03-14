using SyncSaber.NetWorks;
using SyncSaber.SimpleJSON;
using System.Threading;
using System.Threading.Tasks;

namespace SyncSaber.ScoreSabers
{
    public static class ScoreSaberManager
    {
        public const string BASEURL = "https://scoresaber.com/api.php";

        public static async Task<JSONArray> Ranked(int songcouts, RankSort sort)
        {
            var url = $"/api.php?function=get-leaderboards&cat={(int)sort}&ranked=1&limit={songcouts}&page=1&unique=1";
            var buff = await WebClient.GetAsync($"{BASEURL}{url}", new CancellationTokenSource().Token);
            if (buff == null) {
                return null;
            }
            var json = JSON.Parse(buff.ContentToString());
            if (json["songs"] == null) {
                return null;
            }

            return json["songs"] as JSONArray;
        }

        public enum RankSort
        {
            DateRanked = 1,
            Difficurity = 3
        }
    }
}
