using SyncSaber.SimpleJSON;
using SyncSaber.NetWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SyncSaber.ScoreSabers
{
    public static class ScoreSaberManager
    {
        public const string BASEURL = "https://scoresaber.com/api.php";

        public static async Task<JSONArray> Ranked(int songcouts)
        {
            var url = $"/api.php?function=get-leaderboards&cat=3&ranked=1&limit={songcouts}&page=1&unique=1";
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
    }
}
