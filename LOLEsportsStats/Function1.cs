using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Net;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Globalization;

namespace LOLEsportsStats
{
    public class Lolesportsapi
    {
        private HttpClient httpClient = new HttpClient();

        public Lolesportsapi()
        {
        }

        public async Task<JObject> getCompletedEvents(string tournamentId)
        {
            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://esports-api.lolesports.com/persisted/gw/getCompletedEvents?hl=en-US&tournamentId=" + tournamentId),
                Headers = { { "x-api-key", "0TvQnueqKa5mxJntVWt0w4LpLfEkrV1Ta8rQBb9Z" } }
            };

            //Console.WriteLine(request);
            HttpResponseMessage result = await httpClient.SendAsync(request);
            string resultBody = await result.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(resultBody);
            //Console.WriteLine(data.data.schedule.events[0]);
            return data;
        }

        public async Task<JObject> getWindow(string gameId)
        {
            var now = DateTime.UtcNow;
            now = now.Subtract(new TimeSpan(0, 0, 1, now.Second, now.Millisecond));
            var nowString = now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://feed.lolesports.com/livestats/v1/window/" + gameId + "?startingTime=" + nowString),
                Headers = { { "x-api-key", "0TvQnueqKa5mxJntVWt0w4LpLfEkrV1Ta8rQBb9Z" } }
            };

            //Console.WriteLine(request);
            HttpResponseMessage result = await httpClient.SendAsync(request);
            string resultBody = await result.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(resultBody);
            //Console.WriteLine(data);
            return data;
        }
    }

    public class ChampData: IComparable<ChampData>
    {
        public string name;
        public int number;

        public ChampData(string n, int num)
        {
            name = n;
            number = num;
        }

        public int CompareTo(ChampData cd)
        {
            // A null value means that this object is greater.
            if (cd == null)
                return 1;

            else
                return this.number.CompareTo(cd.number);
        }

        public override string ToString()
        {
            return name + ": " + number.ToString();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ChampData))
            {
                return false;
            }

            ChampData other = (ChampData)obj;
            return String.Equals(this.name, other.name);
        }
    }

    public static class GetDeaths
    {
        [FunctionName("GetDeaths")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var api = new Lolesportsapi();
            dynamic matches = await api.getCompletedEvents("110852926142971547");
            
            Dictionary<string,int> champDatas = new Dictionary<string, int>();
            var numMatches = matches.data.schedule.events.Count;
            for(int matchNum = 0; matchNum < numMatches; matchNum++)
            {
                int matchLength = matches.data.schedule.events[matchNum].match.teams[0].result.gameWins + matches.data.schedule.events[matchNum].match.teams[1].result.gameWins;
                //Console.WriteLine(matchLength);
                for(int k = 0; k < matchLength; k++)
                {
                    var gameId = matches.data.schedule.events[matchNum].games[k].id;
                    dynamic gameDetails = await api.getWindow((string)gameId);
                    //Console.WriteLine(gameDetails);
                    for(int j = 0; j < 5; j++)
                    {
                        string champName = (string)gameDetails.gameMetadata.blueTeamMetadata.participantMetadata[j].championId;
                        //Console.WriteLine(gameDetails.frames.Count - 1);
                        //Console.WriteLine(gameDetails.frames[gameDetails.frames.Count - 1]);

                        int gameDeaths = (int)gameDetails.frames[gameDetails.frames.Count - 1].blueTeam.participants[j].deaths;
                        if (champDatas.ContainsKey(champName))
                        {
                            champDatas[champName] = champDatas[champName] + gameDeaths;
                        } else
                        {
                            champDatas[champName] = gameDeaths;
                        }
                    }
                    for (int j = 0; j < 5; j++)
                    {
                        string champName = (string)gameDetails.gameMetadata.redTeamMetadata.participantMetadata[j].championId;
                        //Console.WriteLine(gameDetails.frames.Count - 1);
                        //Console.WriteLine(gameDetails.frames[gameDetails.frames.Count - 1]);

                        int gameDeaths = (int)gameDetails.frames[gameDetails.frames.Count - 1].redTeam.participants[j].deaths;
                        if (champDatas.ContainsKey(champName))
                        {
                            champDatas[champName] = champDatas[champName] + gameDeaths;
                        }
                        else
                        {
                            champDatas[champName] = gameDeaths;
                        }
                    }
                }
            }
            List<ChampData> sortedResults = new List<ChampData>();
            foreach(string key in champDatas.Keys)
            {
                sortedResults.Add(new ChampData(key, champDatas[key]));
            }
            sortedResults.Sort();
            sortedResults.Reverse();
            foreach(ChampData cd in sortedResults)
            {
                Console.WriteLine(cd.ToString());
            }
            //Console.WriteLine("numMatches is "+numMatches);
            return new OkObjectResult(JsonConvert.SerializeObject(sortedResults));
        }
    }
}
