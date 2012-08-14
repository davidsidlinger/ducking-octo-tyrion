using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Twommon
{
    public class Program
    {
        private static int Main(string[] args)
        {
            Contract.Requires(args != null);

            if (args.Length != 2)
            {
                PrintUsage();
            }

            Task.WaitAll(DoIt(args[0], args[1]));

            return 0;
        }

        private static async Task DoIt(string first, string other)
        {
            Contract.Requires(first != null);
            Contract.Requires(other != null);

            using (var client = new HttpClient())
            {
                var lookupFollowers = new Uri("http://api.twitter.com/1/users/lookup.json");

                
                    var firstFollowers = await GetFollowerIds(first, client);
                var otherFollowers = await GetFollowerIds(other, client);
                var inCommon = firstFollowers.Join(otherFollowers, l => l, l => l, (l, l1) => l);

                var lookupUri = lookupFollowers + "?user_id=" +
                                        string.Join(",", inCommon.Select(l => l.ToString(CultureInfo.InvariantCulture)));
                using (var lookupResponse = await client.GetAsync(lookupUri))
                {
                    try
                    {
                        var users = await lookupResponse.Content.ReadAsAsync<List<User>>();
                        users.ToList().ForEach(u => Console.WriteLine(u.ScreenName));
                    }
                    catch (Exception)
                    {
                        var task = lookupResponse.Content.ReadAsStringAsync();
                        Task.WaitAll(task);
                        Console.WriteLine(task.Result);
                        throw;
                    }
                }

            }
        }

        private static async Task<IEnumerable<long>> GetFollowerIds(string screenName, HttpClient client)
        {
            var ids = new List<long>();
            for (long? cursor = -1; cursor != null; )
            {
                var t = await GetFollowerIds(screenName, cursor.Value, client);
                ids.AddRange(t.Item2);
                cursor = t.Item1;
            }
            return ids.AsEnumerable();
        }

        private static async Task<Tuple<long?, IEnumerable<long>>> GetFollowerIds(string screenName,
                                                                     long nextCursor,
                                                                     HttpClient client)
        {
            var getFollowers =
                new Uri("http://api.twitter.com/1/followers/ids.json?screen_name=" + Uri.EscapeDataString(screenName) +
                        "&cursor=" + nextCursor.ToString(CultureInfo.InvariantCulture));
            using(var response = await client.GetAsync(getFollowers))
            {
                try
                {
                    var followers = await response.Content.ReadAsAsync<Followers>();
                    var cursor = followers.NextCursor == followers.PreviousCursor ? (long?)null : followers.NextCursor;
                    return Tuple.Create(cursor, followers.Ids.AsEnumerable());
                }
                catch (Exception)
                {
                    var task = response.Content.ReadAsStringAsync();
                    Task.WaitAll(task);
                    Console.WriteLine(task.Result);
                    throw;
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Help!");
        }
    }

    public class Followers
    {
        private readonly IList<long> _ids = new List<long>();

        public ICollection<long> Ids
        {
            get { return _ids; }
        }

        [JsonProperty("next_cursor")]
        public long? NextCursor { get; set; }

        [JsonProperty("previous_cursor")]
        public long? PreviousCursor { get; set; }
    }

    public class User
    {
        [JsonProperty("screen_name")]
        public string ScreenName { get; set; }
    }
}