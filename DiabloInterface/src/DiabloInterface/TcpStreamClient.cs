using DiabloInterface;
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;

public class TcpStreamClient
{
    private static readonly HttpClient client = new HttpClient();
    private string host;
    private string apiKey;
    private string status = "connecting";
    private bool sending = false;
    private bool retry = false;
    private string lastJson;

    private interface Syncable
    {
        bool UpdateValue(Character character);
        string ToJson();
    }
    
    private class SyncableProperty<T> : Syncable
    {
        private PropertyInfo property;
        private bool synced;
        protected T value;
        
        public SyncableProperty(string propertyName)
        {
            property = typeof(Character).GetProperty(propertyName);
        }

        public bool UpdateValue(Character character)
        {
            T newValue = (T) property.GetValue(character, null);
            synced = EqualityComparer<T>.Default.Equals(value, newValue);
            value = newValue;

            return !synced;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(value);
        }
    }

    private Dictionary<string, Syncable> syncableProperties = new Dictionary<string, Syncable>() {
        { "level", new SyncableProperty<int>("Level") },
        { "experience", new SyncableProperty<int>("Experience") },
        { "strength", new SyncableProperty<int>("Strength") },
        { "dexterity", new SyncableProperty<int>("Dexterity") },
        { "vitality", new SyncableProperty<int>("Vitality") },
        { "energy", new SyncableProperty<int>("Energy") },
        { "hitpoints", new SyncableProperty<int>("Hitpoints") },
        { "hitpointsMax", new SyncableProperty<int>("HitpointsMax") },
        { "mana", new SyncableProperty<int>("Mana") },
        { "manaMax", new SyncableProperty<int>("ManaMax") },
        { "fireRes", new SyncableProperty<int>("FireResist") },
        { "coldRes", new SyncableProperty<int>("ColdResist") },
        { "lightRes", new SyncableProperty<int>("LightningResist") },
        { "poisonRes", new SyncableProperty<int>("PoisonResist") },
        { "gold", new SyncableProperty<int>("Gold") },
        { "goldStash", new SyncableProperty<int>("GoldStash") },
        { "deaths", new SyncableProperty<short>("Deaths") },
        { "fcr", new SyncableProperty<int>("FasterCastRate") },
        { "frw", new SyncableProperty<int>("FasterRunWalk") },
        { "fhr", new SyncableProperty<int>("FasterHitRecovery") },
        { "ias", new SyncableProperty<int>("IncreasedAttackSpeed") }
    };

    public TcpStreamClient(string host, string apiKey)
    {
        this.host = host;
        this.apiKey = apiKey;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    }

    async private Task PostJson(string json)
    {
#if DEBUG
        Console.WriteLine(json);
#endif

        try
        {
            sending = true;
            var response = await client.PostAsync(host, new StringContent(json, Encoding.UTF8, "application/json"));
            var content = await response.Content.ReadAsStringAsync();

#if DEBUG
            Console.WriteLine(content);
#endif

            if (response.IsSuccessStatusCode)
            {
                status = "ok";
            }
            else
            {
                status = "invalid";
            }

            retry = false;
        }
        catch (HttpRequestException e)
        {
#if DEBUG

            Console.WriteLine(e.Message);
            Console.WriteLine(e.InnerException.Message);
#endif
            status = "lost";
            retry = true;
        }
        finally
        {
            sending = false;
        }
    }

    async public Task Send(Character character)
    {
        // Prevent accumulating queries
        if (sending)
        {
            return;
        }

        List<string> json = new List<string>();

        foreach (KeyValuePair<string, Syncable> stat in syncableProperties)
        {
            if (stat.Value.UpdateValue(character))
            {
                json.Add("\"" + stat.Key + "\": " + stat.Value.ToJson());
            }
        }

        if (json.Count > 0)
        {
            json.Add("\"apiKey\": \"" + apiKey + "\"");
            json.Add("\"name\": \"" + character.name + "\"");
            json.Add("\"playtime\": " + ((int) (character.Time / 1000)).ToString());

            lastJson = "{" + string.Join(", ", json.ToArray()) + "}";
            await PostJson(lastJson);
        } else if (retry)
        {
            await PostJson(lastJson);
        }
    }

    public string GetStatus()
    {
        return status;
    }
}