using DiabloInterface;
using DiabloInterface.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using System.Text;

public class TcpStreamClient
{
    private ClientWebSocket client;
    private BinaryWriter writer;
    private bool connected;
    private string host;
    private string apiKey;

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
        { "name", new SyncableProperty<string>("name") },
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
        
        Connect();
    }

    async public void Connect()
    {
        try
        {
            client = new ClientWebSocket();
            await client.ConnectAsync(new Uri(host), CancellationToken.None);
            connected = true;

            ArraySegment<byte> segment = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"apiKey\": \"" + apiKey + "\"}"));
            await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);

            //byte[] bytes = new byte[1024];
            //int bytesRead = ns.Read(bytes, 0, bytes.Length);
            //Console.WriteLine(Encoding.ASCII.GetString(bytes, 0, bytesRead));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            connected = false;
        }
    }

    public void Send(Character character)
    {
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
            json.Add("\"t\": " + character.Time.ToString());

            try
            {
                ArraySegment<byte> segment = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{" + String.Join(", ", json.ToArray()) + "}"));
                client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Connect();
            }
        }
    }

    public void Close()
    {
        if (connected)
        {
            client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
    }
}