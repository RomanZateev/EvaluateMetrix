using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace EvaluateMetrix
{
    class Program
    {
        class User
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

        class Users
        {
            [JsonProperty("users")]
            public List<User> users { get; set; }
        }

        class UserActivity
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

        class Session
        {
            [JsonProperty("values generated")]
            public List<double> values { get; set; }
        }

        class UserActivities
        {
            [JsonProperty("users")]
            public List<UserActivity> users { get; set; }
        }

        static int generations = 10;

        static void Main()
        {
            Users users = JsonConvert.DeserializeObject<Users>(File.ReadAllText(@"statistics.json"));

            GenerateSessions(users);
        }

        static void GenerateSessions(Users users)
        {
            UserActivities generatedUsers = new UserActivities();

            List<UserActivity> ua = new List<UserActivity>();

            for(int k = 0; k < users.users.Count; k++)
            {
                UserActivity generatedUser = new UserActivity();

                generatedUser.name = users.users[k].name;

                generatedUser.letters = users.users[k].letters;

                List<Session> sessions = new List<Session>();

                for (int i = 0; i < generations; i++)
                {
                    Session session = new Session();

                    List<double> vs = new List<double>();

                    vs.Clear();

                    for (int j = 0; j < users.users[k].expectedValues.Count; j++)
                    {
                        vs.Add(Box_Muller(users.users[k].expectedValues[j], Math.Sqrt(users.users[k].dispersions[j])));
                        //vs.Add(rand.NextDouble() * Math.Sqrt(users.users[k].dispersions[j]) * 6 - Math.Sqrt(users.users[k].dispersions[j]) * 3 + users.users[k].expectedValues[j]);
                    }

                    session.values = vs;

                    sessions.Add(session);
                }

                generatedUser.sessions = sessions;

                CalculateDispersion(ref generatedUser);

                ua.Add(generatedUser);
            }

            generatedUsers.users = ua;

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
            using (StreamWriter file = File.CreateText(@"statisticsGenerated.json"))
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
    }
}
