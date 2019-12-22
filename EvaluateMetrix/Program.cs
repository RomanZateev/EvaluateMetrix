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

        public class LettersFreq
        {
            public Dictionary<string, double> values { get; set; }
        }

        public class User
        {
            public string name { get; set; }

            public Dictionary<string, double> expectedValues { get; set; }

            public Dictionary<string, double> dispersions { get; set; }
        }

        public class Users
        {
            public List<User> users { get; set; }
        }

        public class UserActivity
        {
            public string name { get; set; }

            public List<Session> sessions { get; set; }

            public Dictionary<string, double> dispersions { get; set; }
        }

        public class Session
        {
            public Dictionary<string, double> values { get; set; }
        }

        public class UserActivities
        {
            public List<UserActivity> values { get; set; }
        }

        static int sessions;

        static void Main()
        {
            Console.WriteLine("Количество генерируемых сессий для каждого пользователя: ");

            sessions = Convert.ToInt32(Console.ReadLine());

            try
            {
                GenerateSessions();
                Distances();
                Circle();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.ReadKey();
        }
        
        static void Circle()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DictionaryAsArrayResolver()
            };

            //Паттерны
            Users users = JsonConvert.DeserializeObject<Users>(File.ReadAllText(@"stats/statistics.json"), settings);
            //Сессии
            UserActivities ua = JsonConvert.DeserializeObject<UserActivities>(File.ReadAllText(@"stats/generated.json"));

            bool flag = true;

            while (flag)
            {
                Console.WriteLine("Пользователи системы:");

                foreach(User u in users.users)
                {
                    Console.WriteLine(u.name);
                }

                Console.WriteLine("Введите имя залогиненного пользователя: ");

                string userLogged = Console.ReadLine();

                Console.WriteLine("Введите имя действительного пользователя: ");

                string userActual = Console.ReadLine();

                int iter = 0;

                foreach (Session session in ua.values.FirstOrDefault(x => x.name == userActual).sessions)
                {
                    Console.WriteLine(iter);
                    iter++;
                }

                Console.WriteLine("Введите номер сессии действительного пользователя: ");

                int sessionID = Convert.ToInt32(Console.ReadLine());

                if (Sessions(userLogged, userActual, sessionID))
                    Console.WriteLine("В системе залогинен дайствительный пользователь");
                else
                    Console.WriteLine("Шпион");
            }
        }

        public static UserActivities generatedUsers = new UserActivities();

        static void GenerateSessions()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DictionaryAsArrayResolver()
            };

            Users users = JsonConvert.DeserializeObject<Users>(File.ReadAllText(@"stats/statistics.json"), settings);

            List<UserActivity> ua = new List<UserActivity>();

            for(int k = 0; k < users.users.Count; k++)
            {
                UserActivity generatedUser = new UserActivity();

                generatedUser.name = users.users[k].name;

                List<Session> sessions = new List<Session>();

                for (int i = 0; i < Program.sessions; i++)
                {
                    Session session = new Session();

                    Dictionary<string, double> vs = new Dictionary<string, double>();

                    vs.Clear();

                    for (int j = 0; j < users.users[k].expectedValues.Count; j++)
                    {
                        // первый аргумент - буква, второй - время нажатия
                        vs.Add(users.users[k].expectedValues.ElementAt(j).Key, Box_Muller(users.users[k].expectedValues.ElementAt(j).Value, Math.Sqrt(users.users[k].dispersions.ElementAt(j).Value)));
                    }

                    session.values = vs;

                    sessions.Add(session);
                }

                generatedUser.sessions = sessions;

                CalculateDispersion(ref generatedUser);

                ua.Add(generatedUser);
            }

            generatedUsers.values = ua;

            WriteTOJson(generatedUsers);
        }
        
        static double Box_Muller(double m, double sigma)
        {
            Start:

            Random rand = new Random();

            double x = rand.NextDouble() * 2 - 1;

            double y = rand.NextDouble() * 2 - 1;

            double s = Math.Pow(x, 2) + Math.Pow(y, 2);

            double z0;
            if (s > 0 && s <= 1)
                z0 = x * Math.Sqrt(-2 * Math.Log(s, Math.E) / s);
            else
                goto Start;
            return m + sigma * z0; 
        }

        static void WriteTOJson(UserActivities users)
        {
            using (StreamWriter file = File.CreateText(@"stats/generated.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, users);
            }
        }

        static void CalculateDispersion(ref UserActivity user)
        {
            Dictionary<string, double> dispersions = new Dictionary<string, double>();

            for (int i = 0; i < user.sessions[0].values.Count; i++)
            {
                double averageSumm = 0;

                for (int j = 0; j < user.sessions.Count; j++)
                {
                    averageSumm += user.sessions[j].values.ElementAt(i).Value;
                }

                double average = averageSumm / user.sessions.Count;

                double summ = 0;

                for (int j = 0; j < user.sessions.Count; j++)
                {
                    summ += Math.Pow(user.sessions[j].values.ElementAt(i).Value - average, 2);
                }

                dispersions.Add(user.sessions[0].values.ElementAt(i).Key, summ / user.sessions.Count);
            }

            user.dispersions = dispersions;
        }

        //"Границы", определяющие является ли пользователь тем, за кого себя выдает
        //Вычисляются "динамически"
        static Dictionary<User, double> userBarriers = new Dictionary<User, double>();

        static void Distances()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DictionaryAsArrayResolver()
            };

            //Паттерны
            Users users = JsonConvert.DeserializeObject<Users>(File.ReadAllText(@"stats/statistics.json"), settings);

            // Буквы и вероятности
            LettersFreq letterFreq = JsonConvert.DeserializeObject<LettersFreq>(File.ReadAllText(@"stats/letter frequency.json"), settings);

            // Получение сгенерированных сессий пользователей
            UserActivities ua = JsonConvert.DeserializeObject<UserActivities>(File.ReadAllText(@"stats/generated.json"));

            for (int i = 0; i < ua.values.Count; i++)
            {
                bool first = false;

                User user = users.users.Find(x => x.name == ua.values[i].name);

                for (int j = 0; j < ua.values[i].sessions.Count; j++)
                {
                    double summ = Difference(ua.values[i].sessions[j].values, users.users.FirstOrDefault(x => x.name == ua.values[i].name).expectedValues);

                    double timeXfreq = 0;

                    int counter = 0;

                    //заполняю "барьеры"
                    foreach (KeyValuePair<string, double> value in user.expectedValues)
                    {
                        timeXfreq += value.Value * letterFreq.values.FirstOrDefault(x => x.Key == value.Key).Value;
                        counter++;
                    }

                    if (!first)
                    {
                        first = true;
                        userBarriers.Add(user, summ);// / timeXfreq);
                    }

                    if (userBarriers.Count != 0 && userBarriers.Keys.Last() == user && userBarriers.Values.Last() < summ) ;// / timeXfreq)
                    {
                        userBarriers.Remove(userBarriers.Keys.Last());
                        userBarriers.Add(user, summ);// / timeXfreq);
                    }
                }
            }

            foreach (KeyValuePair<User, double> item in userBarriers)
            {
                Console.WriteLine("User " + item.Key.name + " has barrier " + item.Value + " %");
            }
        }
        //
        static bool Sessions(string userLogged, string userActual, int sessionID)
        {
            UserActivities ua = JsonConvert.DeserializeObject<UserActivities>(File.ReadAllText(@"stats/generated.json"));

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DictionaryAsArrayResolver()
            };

            //Паттерны
            Users users = JsonConvert.DeserializeObject<Users>(File.ReadAllText(@"stats/statistics.json"), settings);

            if (Difference(ua.values.FirstOrDefault(x => x.name == userActual).sessions.ElementAt(sessionID).values, users.users.FirstOrDefault(x => x.name == userLogged).expectedValues) > userBarriers.FirstOrDefault(x => x.Key.name == userLogged).Value)
            {
                return false;
            }

            return true;
        }
        //Разница
        static double Difference(Dictionary<string, double> session, Dictionary<string, double> pattern)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Formatting = Formatting.Indented;
            settings.ContractResolver = new DictionaryAsArrayResolver();

            // Буквы и вероятности
            LettersFreq letters = JsonConvert.DeserializeObject<LettersFreq>(File.ReadAllText(@"stats/letter frequency.json"), settings);

            double summ = 0;

            foreach (KeyValuePair<string, double> kv in session)
            {
                if (pattern.ContainsKey(kv.Key))
                {
                    summ += Math.Abs(kv.Value - pattern.FirstOrDefault(x => x.Key == kv.Key).Value) * letters.values.FirstOrDefault(x => x.Key == kv.Key).Value;
                }
            }

            return summ;
        }
    }
}
