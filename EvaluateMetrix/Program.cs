using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.Collections;

namespace EvaluateMetrix
{
    class Program
    {
        class DictionaryAsArrayResolver : DefaultContractResolver
        {
            protected override JsonContract CreateContract(Type objectType)
            {
                if (objectType.GetInterfaces().Any(i => i == typeof(IDictionary) ||
                    (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))))
                {
                    return base.CreateArrayContract(objectType);
                }

                return base.CreateContract(objectType);
            }
        }

        public class Letters
        {
            public Dictionary<string, double> Dict { get; set; }
        }

        public class User
        {
            [JsonProperty("name")]
            public string name { get; set; }

            [JsonProperty("letters")]
            public List<string> letters { get; set; }

            [JsonProperty("expected values")]
            public List<double> expectedValues { get; set; }

            [JsonProperty("dispersions")]
            public List<double> dispersions { get; set; }
        }

        public class Users
        {
            [JsonProperty("users")]
            public List<User> users { get; set; }
        }

        public class UserActivity
        {
            [JsonProperty("name")]
            public string name { get; set; }

            [JsonProperty("letters")]
            public List<string> letters { get; set; }

            [JsonProperty("sessions")]
            public List<Session> sessions { get; set; }

            [JsonProperty("dispersions")]
            public List<double> dispersions { get; set; }
        }

        public class Session
        {
            [JsonProperty("values generated")]
            public List<double> values { get; set; }
        }

        public class UserActivities
        {
            [JsonProperty("ua")]
            public List<UserActivity> ua { get; set; }
        }

        static int sessions;

        static void Main()
        {

            Console.WriteLine("Количество генерируемых сессий для каждого пользователя: ");

            sessions = Convert.ToInt32(Console.ReadLine());

            Users users = JsonConvert.DeserializeObject<Users>(File.ReadAllText(@"users stats.json"));

            GenerateSessions(users);

            Distances(users);
        }

        public static UserActivities generatedUsers = new UserActivities();

        static void GenerateSessions(Users users)
        {
            List<UserActivity> ua = new List<UserActivity>();

            for(int k = 0; k < users.users.Count; k++)
            {
                UserActivity generatedUser = new UserActivity();

                generatedUser.name = users.users[k].name;

                generatedUser.letters = users.users[k].letters;

                List<Session> sessions = new List<Session>();

                for (int i = 0; i < Program.sessions; i++)
                {
                    Session session = new Session();

                    List<double> vs = new List<double>();

                    vs.Clear();

                    for (int j = 0; j < users.users[k].expectedValues.Count; j++)
                    {
                        vs.Add(Box_Muller(users.users[k].expectedValues[j], Math.Sqrt(users.users[k].dispersions[j])));
                    }

                    session.values = vs;

                    sessions.Add(session);
                }

                generatedUser.sessions = sessions;

                CalculateDispersion(ref generatedUser);

                ua.Add(generatedUser);
            }

            generatedUsers.ua = ua;

            WriteTOJson(generatedUsers);
        }
        
        static double Box_Muller(double m, double sigma)
        {
            Start:

            Random rand = new Random();

            double x = rand.NextDouble() * 2 - 1;

            double y = rand.NextDouble() * 2 - 1;

            double s = Math.Pow(x, 2) + Math.Pow(y, 2);

            double z0 = 0;

            if (s > 0 && s <= 1)
                z0 = x * Math.Sqrt(-2 * Math.Log(s, Math.E) / s);
            else
                goto Start;
            return m + sigma * z0; 
        }

        static void WriteTOJson(UserActivities users)
        {
            using (StreamWriter file = File.CreateText(@"generated users stats.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, users);
            }
        }

        static void CalculateDispersion(ref UserActivity user)
        {
            List<double> dispersions = new List<double>();

            for (int i = 0; i < user.sessions[0].values.Count; i++)
            {
                double averageSumm = 0;

                for (int j = 0; j < user.sessions.Count; j++)
                {
                    averageSumm += user.sessions[j].values[i];
                }

                double average = averageSumm / user.sessions.Count;

                double summ = 0;

                for (int j = 0; j < user.sessions.Count; j++)
                {
                    summ += Math.Pow(user.sessions[j].values[i] - average, 2);
                }

                dispersions.Add(summ / user.sessions.Count);
            }

            user.dispersions = dispersions;
        }

        //"Границы", определяющие является ли пользователь тем, за кого себя выдает
        static Dictionary<User, double> userBarriers = new Dictionary<User, double>();

        static void Distances(Users users)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Formatting = Formatting.Indented;
            settings.ContractResolver = new DictionaryAsArrayResolver();

            // Буквы и вероятности
            Letters letters = JsonConvert.DeserializeObject<Letters>(File.ReadAllText(@"letter frequency.json"), settings);

            // Получение сгенерированных сессий пользователей
            UserActivities ua = JsonConvert.DeserializeObject<UserActivities>(File.ReadAllText(@"generated users stats.json"));

            for (int i = 0; i < ua.ua.Count; i++ ) 
            {
                bool first = false;

                User user = users.users.Find(x => x.name == ua.ua[i].name);

                for (int j = 0; j < ua.ua[i].sessions.Count; j++)
                {
                    double summ = 0;

                    for (int k = 0; k < ua.ua[i].sessions[j].values.Count; k++)
                    {
                        if (letters.Dict.ContainsKey(generatedUsers.ua[i].letters[k]))
                        {
                            summ += 
                                (ua.ua[i].sessions[j].values[k] -
                                user.expectedValues[user.letters.FindIndex(x => x == ua.ua[i].letters[k])]) * 
                                letters.Dict.FirstOrDefault(x => x.Key == ua.ua[i].letters[k]).Value;
                        }
                    }

                    double timeXfreq = 0;

                    int counter = 0;

                    foreach(double value in user.expectedValues)
                    {
                        timeXfreq += value * letters.Dict.FirstOrDefault(x => x.Key == user.letters[counter]).Value;
                        counter++;
                    }

                    if (!first)
                    {
                        first = true;
                        userBarriers.Add(user, summ / timeXfreq);
                    }

                    if (userBarriers.Count != 0 && userBarriers.Keys.Last() == user && userBarriers.Values.Last() < summ / timeXfreq)
                    {
                        userBarriers.Remove(userBarriers.Keys.Last());
                        userBarriers.Add(user, summ / timeXfreq);
                    }                       
                }
            }

            foreach(KeyValuePair<User, double> item in userBarriers)
            {
                Console.WriteLine("User " + item.Key.name + " has barrier " + item.Value + " %");
            }
        }

        static bool SessionComparisonWithPattern(List<double> session, List<double> pattern)
        {
            string sdf;

            return true;
        }
    }
}
